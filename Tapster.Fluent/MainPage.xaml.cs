using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Tapster_Fluent;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void TabTyper_Click(object sender, RoutedEventArgs e)
    {
        TyperPanel.Visibility = Visibility.Visible;
        HolderPanel.Visibility = Visibility.Collapsed;
        ClickerPanel.Visibility = Visibility.Collapsed;
        UpdateTabButtons("Typer");
    }

    private void TabHolder_Click(object sender, RoutedEventArgs e)
    {
        TyperPanel.Visibility = Visibility.Collapsed;
        HolderPanel.Visibility = Visibility.Visible;
        ClickerPanel.Visibility = Visibility.Collapsed;
        UpdateTabButtons("Holder");
    }

    private void TabClicker_Click(object sender, RoutedEventArgs e)
    {
        TyperPanel.Visibility = Visibility.Collapsed;
        HolderPanel.Visibility = Visibility.Collapsed;
        ClickerPanel.Visibility = Visibility.Visible;
        UpdateTabButtons("Clicker");
    }

    private void UpdateTabButtons(string active)
    {
        // Reset all buttons
        TabTyper.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        TabHolder.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        TabClicker.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];

        // Highlight active button
        switch (active)
        {
            case "Typer":
                TabTyper.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                break;
            case "Holder":
                TabHolder.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                break;
            case "Clicker":
                TabClicker.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                break;
        }
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement clipboard paste
        TypeTextBox.Text = "Clipboard paste not implemented yet";
    }

    private void ClearText_Click(object sender, RoutedEventArgs e)
    {
        TypeTextBox.Text = "";
    }

    private void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Starting...";
        ProgressBar.Value = 50;

        // TODO: Implement actual functionality
        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.Tick += (s, args) =>
        {
            timer.Stop();
            StatusText.Text = "Ready";
            ProgressBar.Value = 0;
        };
        timer.Start();
    }
}
