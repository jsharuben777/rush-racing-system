using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace RushRacing.Kiosk.Services;

public class KeyboardBlocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isEnabled = false;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public void Install()
    {
        _proc = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
            GetModuleHandle(module.ModuleName!), 0);
        _isEnabled = true;

        System.Diagnostics.Debug.WriteLine("[KEYBOARD] Blocker installed.");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (!_isEnabled || nCode < 0)
            return CallNextHookEx(_hookId, nCode, wParam, lParam);

        int vkCode = Marshal.ReadInt32(lParam);

        // Block Left Windows key
        if (vkCode == 0x5B) return (IntPtr)1;

        // Block Right Windows key
        if (vkCode == 0x5C) return (IntPtr)1;

        // Block Alt+Tab
        if (vkCode == 0x09 && IsKeyDown(0x12)) return (IntPtr)1; // Tab + Alt

        // Block Alt+F4
        if (vkCode == 0x73 && IsKeyDown(0x12)) return (IntPtr)1; // F4 + Alt

        // Block Ctrl+Esc
        if (vkCode == 0x1B && IsKeyDown(0x11)) return (IntPtr)1; // Esc + Ctrl

        // Block Alt+Esc
        if (vkCode == 0x1B && IsKeyDown(0x12)) return (IntPtr)1; // Esc + Alt

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsKeyDown(int vkCode)
    {
        return (GetAsyncKeyState(vkCode) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            System.Diagnostics.Debug.WriteLine("[KEYBOARD] Blocker removed.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}