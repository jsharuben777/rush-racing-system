using RushRacing.Kiosk.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace RushRacing.Kiosk.Views;

public partial class GameSelectView : UserControl
{
    private List<GameDefinition> _games = new();
    private List<Border> _gameCards = new();
    private int _selectedIndex = 0;
    private bool _confirmShowing = false;

    public event Action<GameDefinition>? OnGameConfirmed;
    public event Action? OnSelectionTimeout;

    public GameSelectView()
    {
        InitializeComponent();
    }

    public void SetGames(List<GameDefinition> games, int durationMinutes)
    {
        _games = games;
        _selectedIndex = 0;
        _confirmShowing = false;
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
        TimeoutWarningOverlay.Visibility = Visibility.Collapsed;
        SessionInfoText.Text = $"Your session: {durationMinutes} minutes";

        BuildGameCards();
        UpdateSelection();
    }

    private void BuildGameCards()
    {
        GameCardsPanel.Children.Clear();
        _gameCards.Clear();

        foreach (var game in _games)
        {
            var card = new Border
            {
                Width = 300,
                Height = 400,
                Margin = new Thickness(15, 0, 15, 0),
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(4),
                BorderBrush = Brushes.Transparent,
                Background = new SolidColorBrush(
                    Color.FromRgb(0x2A, 0x2A, 0x4A)),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "🏎️",
                            FontSize = 64,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new TextBlock
                        {
                            Text = game.DisplayName,
                            Foreground = Brushes.White,
                            FontSize = 24,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 260
                        }
                    }
                }
            };

            _gameCards.Add(card);
            GameCardsPanel.Children.Add(card);
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _gameCards.Count; i++)
        {
            if (i == _selectedIndex)
            {
                _gameCards[i].BorderBrush = new SolidColorBrush(
                    Color.FromRgb(0xFF, 0x33, 0x33));
                _gameCards[i].RenderTransform = new ScaleTransform(1.05, 1.05);
                _gameCards[i].RenderTransformOrigin = new Point(0.5, 0.5);
                _gameCards[i].Effect = new DropShadowEffect
                {
                    Color = Colors.Red,
                    BlurRadius = 30,
                    ShadowDepth = 0
                };
            }
            else
            {
                _gameCards[i].BorderBrush = Brushes.Transparent;
                _gameCards[i].RenderTransform = new ScaleTransform(1.0, 1.0);
                _gameCards[i].Effect = null;
            }
        }
    }

    public void NavigateLeft()
    {
        if (_confirmShowing) return;
        if (_selectedIndex > 0)
        {
            _selectedIndex--;
            UpdateSelection();
        }
    }

    public void NavigateRight()
    {
        if (_confirmShowing) return;
        if (_selectedIndex < _games.Count - 1)
        {
            _selectedIndex++;
            UpdateSelection();
        }
    }

    public void PressConfirm()
    {
        if (_confirmShowing)
        {
            // Already showing confirmation — this is the final confirm
            ConfirmationOverlay.Visibility = Visibility.Collapsed;
            _confirmShowing = false;
            OnGameConfirmed?.Invoke(_games[_selectedIndex]);
        }
        else
        {
            // Show confirmation
            ConfirmGameName.Text = _games[_selectedIndex].DisplayName;
            ConfirmationOverlay.Visibility = Visibility.Visible;
            _confirmShowing = true;
        }
    }

    public void PressBack()
    {
        if (_confirmShowing)
        {
            ConfirmationOverlay.Visibility = Visibility.Collapsed;
            _confirmShowing = false;
        }
    }

    public void ShowTimeoutWarning(int secondsRemaining)
    {
        TimeoutWarningOverlay.Visibility = Visibility.Visible;
        TimeoutCountdownText.Text = $"Session will end in {secondsRemaining}s...";
    }

    public void HideTimeoutWarning()
    {
        TimeoutWarningOverlay.Visibility = Visibility.Collapsed;
    }
}