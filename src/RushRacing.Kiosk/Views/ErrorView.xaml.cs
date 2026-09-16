using System.Windows.Controls;

namespace RushRacing.Kiosk.Views;

public partial class ErrorView : UserControl
{
    public ErrorView()
    {
        InitializeComponent();
    }

    public void SetErrorMessage(string message)
    {
        ErrorMessageText.Text = message;
    }
}