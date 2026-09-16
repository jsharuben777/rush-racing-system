using Microsoft.Extensions.Configuration;
using RushRacing.Kiosk.Enums;
using RushRacing.Kiosk.Models;
using RushRacing.Kiosk.Services;
using RushRacing.Kiosk.Views;
using RushRacing.Kiosk.Windows;
using System.IO;
using System.Windows;
using System.Windows.Input;


namespace RushRacing.Kiosk;

public partial class MainWindow : Window
{
    // Config
    private KioskConfig _config = new();
    private List<GameDefinition> _games = new();

    // Services
    private KioskStateManager _stateManager = new();
    private ServerConnection _serverConnection = null!;
    private GameLauncher _gameLauncher = null!;
    private GameWatchdog _gameWatchdog = null!;
    private HeartbeatService _heartbeatService = null!;
    private KeyboardBlocker _keyboardBlocker = new();

    // Windows
    private TimerOverlayWindow _timerOverlay = new();

    // Views
    private IdleView _idleView = new();
    private GameSelectView _gameSelectView = new();
    private LaunchingView _launchingView = new();
    private CompletingView _completingView = new();
    private ErrorView _errorView = new();

    // Session state
    private SessionInfo? _currentSession;
    private string? _selectedGameId;
    private CancellationTokenSource _sessionCts = new();
    private CancellationTokenSource _appCts = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        KeyDown += MainWindow_KeyDown;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load configuration
            LoadConfig();
            _idleView.SetBookingUrl(_config.PublicBookingUrl);
            Log($"Server URL: {_config.ServerUrl}");
            Log($"[DEBUG] Hub URL: {_config.HubUrl}");
            Log($"[DEBUG] Machine Code: {_config.MachineCode}");
            Log($"[DEBUG] API Key: {_config.ApiKey}");
            Log($"[DEBUG] Games loaded: {_games.Count}");

            // Initialize services
            _serverConnection = new ServerConnection(_config);
            _gameLauncher = new GameLauncher(_games);
            _gameWatchdog = new GameWatchdog(
                _gameLauncher,
                _config.MaxGameCrashesBeforeError,
                _config.CrashWindowMinutes);
            _heartbeatService = new HeartbeatService(
                _serverConnection,
                _config.HeartbeatIntervalSeconds);

            _keyboardBlocker.Install();
            _stateManager.StateChanged += OnStateChanged;

            _serverConnection.OnStartSession += OnStartSession;
            _serverConnection.OnResumeSession += OnResumeSession;
            _serverConnection.OnSessionConfirmed += OnSessionConfirmed;
            _serverConnection.OnConnected += () =>
                Dispatcher.Invoke(() => Console.WriteLine("[DEBUG] Server connected event fired"));
            _serverConnection.OnDisconnected += () =>
                Dispatcher.Invoke(() =>
                {
                    Log("[DEBUG] Server disconnected event fired");
                    if (_stateManager.CurrentState == KioskState.Idle)
                        _stateManager.TransitionTo(KioskState.Offline);
                });
            _serverConnection.OnAuthenticationFailed += (msg) =>
                Log($"[DEBUG] Authentication FAILED: {msg}");

            _gameSelectView.OnGameConfirmed += OnGameConfirmed;
            _gameWatchdog.OnGameCrashed += (count) =>
                Log($"[DEBUG] Game crashed #{count}");
            _gameWatchdog.OnMaxCrashesReached += (msg) =>
                Dispatcher.Invoke(() => HandleMachineError(msg));
            _timerOverlay.OnTimerExpired += () =>
                Dispatcher.Invoke(() => HandleSessionExpired());

            _stateManager.TransitionTo(KioskState.Initializing);

            Log("[DEBUG] Connecting to server...");
            await _serverConnection.ConnectAsync();
            Log($"[DEBUG] IsConnected: {_serverConnection.IsConnected}");

            _ = Task.Run(() => _heartbeatService.StartAsync(_appCts.Token));
            _ = Task.Run(() => _serverConnection.StartReconnectLoop(_appCts.Token));

            if (_serverConnection.IsConnected)
            {
                Log("[DEBUG] Sending MachineReady...");
                await _serverConnection.SendMachineReady();
                _stateManager.TransitionTo(KioskState.Idle);
                Log("[DEBUG] State: IDLE");
            }
            else
            {
                Log("[DEBUG] NOT connected. State: OFFLINE");
                _stateManager.TransitionTo(KioskState.Offline);
            }
        }
        catch (Exception ex)
        {
            Log($"[DEBUG] STARTUP ERROR: {ex}");
            MessageBox.Show(
                $"Startup Error:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Rush Racing Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Log(string message)
    {
        var logLine = $"{DateTime.Now:HH:mm:ss} | {message}";
        var logPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "kiosk_debug.log");
        File.AppendAllText(logPath, logLine + Environment.NewLine);
    }


    private void LoadConfig()
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false);

        var configuration = configBuilder.Build();

        _config = new KioskConfig();
        configuration.GetSection("RushRacing").Bind(_config);

        _games = new List<GameDefinition>();
        configuration.GetSection("Games").Bind(_games);

        System.Diagnostics.Debug.WriteLine(
            $"[CONFIG] Machine: {_config.MachineCode}, Games: {_games.Count}");
    }

    private void OnStateChanged(KioskState oldState, KioskState newState)
    {
        Dispatcher.Invoke(() =>
        {
            switch (newState)
            {
                case KioskState.Initializing:
                    MainContent.Content = null;
                    break;

                case KioskState.Idle:
                    MainContent.Content = _idleView;
                    _timerOverlay.StopCountdown();
                    _currentSession = null;
                    _selectedGameId = null;
                    break;

                case KioskState.GameSelect:
                    MainContent.Content = _gameSelectView;
                    break;

                case KioskState.Launching:
                    MainContent.Content = _launchingView;
                    break;

                case KioskState.Active:
                    // Hide kiosk UI — game is in foreground
                    this.WindowState = WindowState.Minimized;
                    break;

                case KioskState.Completing:
                    this.WindowState = WindowState.Maximized;
                    MainContent.Content = _completingView;
                    _timerOverlay.StopCountdown();
                    break;

                case KioskState.Error:
                    this.WindowState = WindowState.Maximized;
                    MainContent.Content = _errorView;
                    _timerOverlay.StopCountdown();
                    break;

                case KioskState.Offline:
                    _errorView.SetErrorMessage(
                        "Cannot connect to server. Waiting for connection...");
                    MainContent.Content = _errorView;
                    break;
            }
        });
    }

    // --- Server Event Handlers ---

    private void OnStartSession(SessionInfo session)
    {
        Dispatcher.Invoke(() =>
        {
            _currentSession = session;
            _gameSelectView.SetGames(_games, session.DurationMinutes);
            _stateManager.TransitionTo(KioskState.GameSelect);
        });
    }

    private void OnResumeSession(SessionInfo session)
    {
        Dispatcher.Invoke(async () =>
        {
            _currentSession = session;

            if (session.Status == "Active" && session.SessionExpiresAt.HasValue)
            {
                var remaining = session.SessionExpiresAt.Value - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero && !string.IsNullOrEmpty(session.SelectedGameId))
                {
                    // Resume active session
                    _selectedGameId = session.SelectedGameId;
                    await LaunchGameAndStartSession();
                    return;
                }
            }

            // Session already expired or invalid — go to idle
            await _serverConnection.SendMachineReady();
            _stateManager.TransitionTo(KioskState.Idle);
        });
    }

    private void OnSessionConfirmed(SessionInfo session)
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentSession != null)
            {
                _currentSession.SessionExpiresAt = session.SessionExpiresAt;

                // Start the timer overlay
                if (session.SessionExpiresAt.HasValue)
                {
                    _timerOverlay.StartCountdown(session.SessionExpiresAt.Value);
                }

                // Start watchdog
                if (!string.IsNullOrEmpty(_selectedGameId))
                {
                    _sessionCts = new CancellationTokenSource();
                    _ = Task.Run(() =>
                        _gameWatchdog.StartWatching(_selectedGameId, _sessionCts.Token));

                    _heartbeatService.UpdateSessionInfo(
                        _currentSession.SessionCode, _selectedGameId, true);
                }

                _stateManager.TransitionTo(KioskState.Active);
            }
        });
    }

    // --- Game Selection Handler ---

    private async void OnGameConfirmed(GameDefinition game)
    {
        _selectedGameId = game.GameId;

        // Notify server
        if (_currentSession != null)
        {
            await _serverConnection.SendGameSelected(
                _currentSession.SessionCode, game.GameId);
        }

        await LaunchGameAndStartSession();
    }

    private async Task LaunchGameAndStartSession()
    {
        if (string.IsNullOrEmpty(_selectedGameId) || _currentSession == null) return;

        var game = _gameLauncher.GetGame(_selectedGameId);
        if (game == null)
        {
            HandleMachineError("Game definition not found.");
            return;
        }

        // Show launching screen
        _launchingView.SetGameName(game.DisplayName);
        _stateManager.TransitionTo(KioskState.Launching);

        // Launch the game
        var launched = await _gameLauncher.LaunchGame(_selectedGameId);

        if (!launched)
        {
            HandleMachineError($"Failed to launch {game.DisplayName}.");
            return;
        }

        // Tell server the game is running
        await _serverConnection.SendSessionAcknowledged(_currentSession.SessionCode);

        // SessionConfirmed callback from server will handle the rest
    }

    // --- Session Expiry ---

    private async void HandleSessionExpired()
    {
        System.Diagnostics.Debug.WriteLine("[APP] Session expired!");

        _sessionCts.Cancel();

        // Kill the game
        if (!string.IsNullOrEmpty(_selectedGameId))
        {
            _gameLauncher.KillGame(_selectedGameId);
        }

        _heartbeatService.UpdateSessionInfo(null, null, false);

        // Show completing screen
        _stateManager.TransitionTo(KioskState.Completing);

        // Notify server
        if (_currentSession != null)
        {
            await _serverConnection.SendSessionCompleted(_currentSession.SessionCode);
        }

        // Wait 15 seconds, then return to idle
        await Task.Delay(15000);

        await _serverConnection.SendMachineReset();
        _stateManager.TransitionTo(KioskState.Idle);
    }

    // --- Error Handling ---

    private async void HandleMachineError(string errorMessage)
    {
        System.Diagnostics.Debug.WriteLine($"[APP] Machine error: {errorMessage}");

        _sessionCts.Cancel();

        if (!string.IsNullOrEmpty(_selectedGameId))
        {
            _gameLauncher.KillGame(_selectedGameId);
        }

        _heartbeatService.UpdateSessionInfo(null, null, false);

        _errorView.SetErrorMessage(errorMessage);
        _stateManager.TransitionTo(KioskState.Error);

        if (_currentSession != null)
        {
            await _serverConnection.SendError(
                _currentSession.SessionCode, errorMessage);
        }
    }

    // --- Keyboard Input for Game Selection ---

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (_stateManager.CurrentState == KioskState.GameSelect)
        {
            switch (e.Key)
            {
                case Key.Left:
                    _gameSelectView.NavigateLeft();
                    break;
                case Key.Right:
                    _gameSelectView.NavigateRight();
                    break;
                case Key.Enter:
                case Key.A:
                    _gameSelectView.PressConfirm();
                    break;
                case Key.Escape:
                case Key.B:
                    _gameSelectView.PressBack();
                    break;
            }

            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _appCts.Cancel();
        _sessionCts.Cancel();
        _keyboardBlocker.Dispose();
        _timerOverlay.Close();
        _ = _serverConnection.DisposeAsync();
        base.OnClosed(e);
    }
}
