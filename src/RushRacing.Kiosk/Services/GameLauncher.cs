using RushRacing.Kiosk.Models;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RushRacing.Kiosk.Services;

public class GameLauncher
{
    private readonly List<GameDefinition> _games;

    public GameLauncher(List<GameDefinition> games)
    {
        _games = games;
    }

    public List<GameDefinition> GetAvailableGames()
    {
        return _games.ToList();
    }

    public GameDefinition? GetGame(string gameId)
    {
        return _games.FirstOrDefault(g => g.GameId == gameId);
    }

    public async Task<bool> LaunchGame(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null) return false;

        System.Diagnostics.Debug.WriteLine($"[GAME] Launching {game.DisplayName}...");

        ResetGameConfig(game);

        switch (game.LaunchMethod.ToLower())
        {
            case "steam":
                return await LaunchViaSteam(game);
            case "direct":
                return await LaunchDirect(game);
            default:
                System.Diagnostics.Debug.WriteLine($"[GAME] Unknown launch method: {game.LaunchMethod}");
                return false;
        }
    }

    private async Task<bool> LaunchViaSteam(GameDefinition game)
    {
        try
        {
            var steamRunning = Process.GetProcessesByName("steam").Any();
            if (!steamRunning)
            {
                System.Diagnostics.Debug.WriteLine("[GAME] Starting Steam...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = @"C:\Program Files (x86)\Steam\steam.exe",
                    Arguments = "-silent",
                    UseShellExecute = true
                });
                await Task.Delay(8000);
            }

            System.Diagnostics.Debug.WriteLine($"[GAME] Launching Steam app {game.SteamAppId}...");
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{game.SteamAppId}",
                UseShellExecute = true
            });

            return await WaitForProcess(game.ProcessName, game.LaunchTimeoutSeconds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GAME] Launch failed: {ex.Message}");
            return false;
        }
    }

    private Task<bool> LaunchDirect(GameDefinition game)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[GAME] Direct launching: {game.ProcessName}");

            Process.Start(new ProcessStartInfo
            {
                FileName = game.ProcessName,
                UseShellExecute = true
            });

            return WaitForProcess(game.ProcessName, game.LaunchTimeoutSeconds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GAME] Direct launch failed: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    private async Task<bool> WaitForProcess(string processName, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        System.Diagnostics.Debug.WriteLine($"[GAME] Waiting for process: {processName}...");

        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(processName).Any())
            {
                System.Diagnostics.Debug.WriteLine($"[GAME] Process {processName} found!");
                return true;
            }

            await Task.Delay(2000);
        }

        System.Diagnostics.Debug.WriteLine($"[GAME] Timeout waiting for {processName}");
        return false;
    }

    public bool IsGameRunning(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null) return false;

        return Process.GetProcessesByName(game.ProcessName).Any();
    }

    public void KillGame(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null) return;

        System.Diagnostics.Debug.WriteLine($"[GAME] Killing {game.DisplayName}...");

        foreach (var process in Process.GetProcessesByName(game.ProcessName))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                System.Diagnostics.Debug.WriteLine($"[GAME] Process killed.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GAME] Kill failed: {ex.Message}");
            }
        }
    }

    public void HideGameTitleBar(string gameId)
    {
        var game = GetGame(gameId);
        if (game == null) return;

        var processes = Process.GetProcessesByName(game.ProcessName);
        foreach (var process in processes)
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    int style = GetWindowLong(process.MainWindowHandle, GWL_STYLE);
                    style &= ~WS_CAPTION;
                    style &= ~WS_SYSMENU;
                    style &= ~WS_THICKFRAME;
                    SetWindowLong(process.MainWindowHandle, GWL_STYLE, style);

                    var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
                    SetWindowPos(process.MainWindowHandle, IntPtr.Zero,
                        0, 0, screenWidth, screenHeight,
                        SWP_NOZORDER | SWP_FRAMECHANGED);

                    System.Diagnostics.Debug.WriteLine($"[GAME] Title bar hidden for {game.ProcessName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GAME] Failed to hide title bar: {ex.Message}");
            }
        }
    }

    private void ResetGameConfig(GameDefinition game)
    {
        if (game.ConfigResetPaths == null || game.ConfigResetPaths.Length == 0)
            return;

        var goldenDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "GameConfigs", game.GameId);

        foreach (var targetPath in game.ConfigResetPaths)
        {
            var fileName = Path.GetFileName(targetPath);
            var goldenPath = Path.Combine(goldenDir, fileName);

            if (File.Exists(goldenPath))
            {
                try
                {
                    File.Copy(goldenPath, targetPath, overwrite: true);
                    System.Diagnostics.Debug.WriteLine($"[GAME] Config reset: {fileName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GAME] Config reset failed: {ex.Message}");
                }
            }
        }
    }

    // Win32 constants
    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}