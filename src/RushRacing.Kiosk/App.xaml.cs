using System.Windows;

namespace RushRacing.Kiosk;

public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show(
                $"Unhandled Error:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Rush Racing Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}