using System.Windows.Controls;

namespace RushRacing.Kiosk.Views;

public partial class LaunchingView : UserControl
{
    public LaunchingView()
    {
        InitializeComponent();
    }

    public void SetGameName(string gameName)
    {
        GameNameText.Text = $"Loading {gameName}...";
    }
}