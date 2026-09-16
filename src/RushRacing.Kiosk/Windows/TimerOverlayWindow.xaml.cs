using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RushRacing.Kiosk.Windows;

public partial class TimerOverlayWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int GWL_EXSTYLE = -20;

    private DispatcherTimer? _timer;
    private DispatcherTimer? _topmostTimer;
    private DateTimeOffset _expiresAt;

    public event Action? OnTimerExpired;

    public TimerOverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Make click-through
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);

        // Position at top-right
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        this.Left = screenWidth - this.Width - 30;
        this.Top = 30;
    }

    public void StartCountdown(DateTimeOffset expiresAt)
    {
        _expiresAt = expiresAt;

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += Timer_Tick;
        _timer.Start();

        // Force topmost every 2 seconds
        _topmostTimer = new DispatcherTimer();
        _topmostTimer.Interval = TimeSpan.FromSeconds(2);
        _topmostTimer.Tick += (s, e) => ForceTopmost();
        _topmostTimer.Start();

        this.Show();
        ForceTopmost();
    }

    public void StopCountdown()
    {
        _timer?.Stop();
        _timer = null;
        _topmostTimer?.Stop();
        _topmostTimer = null;
        this.Hide();
    }

    private void ForceTopmost()
    {
        this.Topmost = false;
        this.Topmost = true;
        SetWindowPos(new WindowInteropHelper(this).Handle,
            HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var remaining = _expiresAt - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            TimerText.Text = "00:00";
            _timer?.Stop();
            _topmostTimer?.Stop();
            OnTimerExpired?.Invoke();
            return;
        }

        TimerText.Text = remaining.ToString(@"mm\:ss");

        // Flash when under 60 seconds
        if (remaining.TotalSeconds <= 60)
        {
            TimerText.Foreground = DateTime.UtcNow.Second % 2 == 0
                ? Brushes.Red
                : Brushes.DarkRed;
        }
        else
        {
            TimerText.Foreground = Brushes.Red;
        }
    }

    // Win32 imports
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}