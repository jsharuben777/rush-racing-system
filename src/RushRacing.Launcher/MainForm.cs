using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RushRacing.Launcher;

public class MainForm : Form
{
    private readonly TextBox _logBox;
    private readonly TextBox _urlBox;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly Label _statusLabel;

    private Process? _cloudflaredProcess;
    private Process? _webProcess;
    private Process? _kioskProcess;

    private LauncherConfig _config = new();

    public MainForm()
    {
        Text = "Rush Racing Launcher";
        Width = 760;
        Height = 500;
        StartPosition = FormStartPosition.CenterScreen;

        _statusLabel = new Label
        {
            Text = "Idle.",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };

        _urlBox = new TextBox
        {
            Dock = DockStyle.Top,
            ReadOnly = true,
            Font = new Font(Font.FontFamily, 10),
            Height = 28
        };

        var buttonPanel = new Panel { Dock = DockStyle.Top, Height = 44 };
        _startButton = new Button { Text = "Start Everything", Left = 8, Top = 8, Width = 160, Height = 28 };
        _stopButton = new Button { Text = "Stop All", Left = 176, Top = 8, Width = 110, Height = 28, Enabled = false };
        _startButton.Click += async (s, e) => await RunPipelineAsync();
        _stopButton.Click += (s, e) => StopAll();
        buttonPanel.Controls.Add(_startButton);
        buttonPanel.Controls.Add(_stopButton);

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9)
        };

        // Dock order matters: Fill must be added first among docked controls for it to fill remaining space correctly.
        Controls.Add(_logBox);
        Controls.Add(buttonPanel);
        Controls.Add(_urlBox);
        Controls.Add(_statusLabel);

        Load += async (s, e) => await LoadConfigAndAutoStartAsync();
        FormClosing += (s, e) => StopAll();
    }

    private async Task LoadConfigAndAutoStartAsync()
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "launcher-config.json");
            if (!File.Exists(configPath))
            {
                Log($"ERROR: launcher-config.json not found next to the exe at: {configPath}");
                MessageBox.Show(this, $"launcher-config.json not found at:\n{configPath}",
                    "Rush Racing Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var json = await File.ReadAllTextAsync(configPath);
            _config = JsonSerializer.Deserialize<LauncherConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LauncherConfig();

            await RunPipelineAsync();
        }
        catch (Exception ex)
        {
            Log($"ERROR loading config: {ex.Message}");
        }
    }

    private async Task RunPipelineAsync()
    {
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        SetStatus("Starting Cloudflare tunnel...");

        try
        {
            var tunnelUrl = await StartCloudflaredAsync();
            if (tunnelUrl == null)
            {
                SetStatus("Failed: no tunnel URL detected.");
                MessageBox.Show(this,
                    "Could not detect a cloudflared tunnel URL in time. Check the log panel for details.",
                    "Rush Racing Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _startButton.Enabled = true;
                return;
            }

            Log($"Tunnel is live at: {tunnelUrl}");
            _urlBox.Text = tunnelUrl;

            SetStatus("Updating Kiosk configuration...");
            var newPublicUrl = PatchKioskConfig(tunnelUrl);
            Log($"PublicBookingUrl set to: {newPublicUrl}");
            _urlBox.Text = newPublicUrl;

            SetStatus("Updating Web configuration...");
            PatchWebConfig(tunnelUrl);
            Log($"Web PublicBaseUrl set to: {tunnelUrl}");

            SetStatus("Starting RushRacing.Web...");
            _webProcess = StartDotnetRun(GetFullPath(_config.WebProjectRelativePath), "RushRacing.Web");

            Log($"Waiting {_config.SecondsToWaitBeforeStartingKiosk}s before starting Kiosk...");
            await Task.Delay(TimeSpan.FromSeconds(_config.SecondsToWaitBeforeStartingKiosk));

            SetStatus("Starting RushRacing.Kiosk...");
            _kioskProcess = StartDotnetRun(GetFullPath(_config.KioskProjectRelativePath), "RushRacing.Kiosk");

            SetStatus("All set. Scan the QR shown in the Kiosk window.");
            Log("All processes launched. Keep this launcher window open - closing it stops the tunnel.");
        }
        catch (Exception ex)
        {
            SetStatus("Failed - see log.");
            Log($"ERROR: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Rush Racing Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _startButton.Enabled = true;
        }
    }

    private Task<string?> StartCloudflaredAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var urlPattern = new Regex(@"https://[a-zA-Z0-9\-]+\.trycloudflare\.com", RegexOptions.Compiled);
        var found = false;

        var psi = new ProcessStartInfo
        {
            FileName = _config.CloudflaredExePath,
            Arguments = $"tunnel --url http://localhost:{_config.LocalPort}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _cloudflaredProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

        void HandleLine(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Log($"[cloudflared] {line}");
            if (!found)
            {
                var match = urlPattern.Match(line);
                if (match.Success)
                {
                    found = true;
                    tcs.TrySetResult(match.Value);
                }
            }
        }

        _cloudflaredProcess.OutputDataReceived += (s, e) => HandleLine(e.Data);
        _cloudflaredProcess.ErrorDataReceived += (s, e) => HandleLine(e.Data);
        _cloudflaredProcess.Exited += (s, e) =>
        {
            if (!found) tcs.TrySetResult(null);
        };

        _cloudflaredProcess.Start();
        _cloudflaredProcess.BeginOutputReadLine();
        _cloudflaredProcess.BeginErrorReadLine();

        // Timeout guard - only takes effect if nothing was found by then.
        _ = Task.Delay(TimeSpan.FromSeconds(_config.SecondsToWaitForTunnelUrl))
            .ContinueWith(_ => tcs.TrySetResult(null));

        return tcs.Task;
    }

    private string PatchKioskConfig(string tunnelUrl)
    {
        var kioskDir = GetFullPath(_config.KioskProjectRelativePath);
        var settingsPath = Path.Combine(kioskDir, "appsettings.json");

        if (!File.Exists(settingsPath))
            throw new FileNotFoundException($"Could not find Kiosk appsettings.json at: {settingsPath}");

        var jsonText = File.ReadAllText(settingsPath);
        var root = JsonNode.Parse(jsonText)!.AsObject();
        var rushRacing = root["RushRacing"]!.AsObject();

        var oldUrlString = rushRacing["PublicBookingUrl"]?.GetValue<string>()
                            ?? "https://placeholder.trycloudflare.com/race/ck01";
        var oldUri = new Uri(oldUrlString);
        var newUrl = tunnelUrl.TrimEnd('/') + oldUri.PathAndQuery;

        rushRacing["PublicBookingUrl"] = newUrl;

        File.WriteAllText(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return newUrl;
    }

    private void PatchWebConfig(string tunnelUrl)
    {
        var webDir = GetFullPath(_config.WebProjectRelativePath);
        var settingsPath = Path.Combine(webDir, "appsettings.json");

        if (!File.Exists(settingsPath))
            throw new FileNotFoundException($"Could not find Web appsettings.json at: {settingsPath}");

        var jsonText = File.ReadAllText(settingsPath);
        var root = JsonNode.Parse(jsonText)!.AsObject();

        if (root["RushRacing"] is not JsonObject rushRacingSection)
        {
            rushRacingSection = new JsonObject();
            root["RushRacing"] = rushRacingSection;
        }

        rushRacingSection["PublicBaseUrl"] = tunnelUrl.TrimEnd('/');

        File.WriteAllText(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private Process StartDotnetRun(string workingDirectory, string label)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run",
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };

        var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {label}");
        Log($"Started {label} (PID {process.Id}) in {workingDirectory}");
        return process;
    }

    private string GetFullPath(string relativePath) => Path.Combine(_config.SolutionRoot, relativePath);

    private void StopAll()
    {
        TryKill(_cloudflaredProcess, "cloudflared");
        TryKill(_webProcess, "RushRacing.Web");
        TryKill(_kioskProcess, "RushRacing.Kiosk");
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        SetStatus("Stopped.");
    }

    private void TryKill(Process? process, string label)
    {
        try
        {
            if (process != null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Log($"Stopped {label}.");
            }
        }
        catch (Exception ex)
        {
            Log($"Could not stop {label}: {ex.Message}");
        }
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) Invoke(() => _statusLabel.Text = text);
        else _statusLabel.Text = text;
    }

    private void Log(string text)
    {
        if (InvokeRequired) Invoke(() => AppendLog(text));
        else AppendLog(text);
    }

    private void AppendLog(string text)
    {
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }
}

public class LauncherConfig
{
    public string SolutionRoot { get; set; } = "";
    public string WebProjectRelativePath { get; set; } = "";
    public string KioskProjectRelativePath { get; set; } = "";
    public int LocalPort { get; set; } = 5233;
    public string CloudflaredExePath { get; set; } = "cloudflared";
    public int SecondsToWaitForTunnelUrl { get; set; } = 30;
    public int SecondsToWaitBeforeStartingKiosk { get; set; } = 8;
}
