using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XdLauncher.Views.Dialogs;

public partial class OnboardingWindow : Window
{
    private int _step;

    public OnboardingWindow(string displayName)
    {
        InitializeComponent();
        AccountName.Text = string.IsNullOrWhiteSpace(displayName) ? "Jugador" : displayName;
        UpdateStep();
    }

    public bool Completed { get; private set; }

    public string Language { get; private set; } = "Español";

    public string InitialSection { get; private set; } = "java";

    public static OnboardingWindow Ask(string? displayName)
    {
        var dialog = new OnboardingWindow(displayName ?? string.Empty);
        dialog.ShowDialog();
        return dialog;
    }

    private void UpdateStep()
    {
        LanguageStep.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        AccountStep.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PlatformStep.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepCounter.Text = $"PASO {_step + 1} DE 3";
        StepLabel.Text = _step switch
        {
            0 => "Empecemos con lo básico",
            1 => "Tu identidad XD",
            _ => "Listo para jugar"
        };
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == 2 ? "Empezar" : "Continuar";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 0)
            return;
        _step--;
        UpdateStep();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 2)
        {
            _step++;
            UpdateStep();
            return;
        }

        Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Español";
        InitialSection = (InitialPlatformCombo.SelectedIndex) switch
        {
            1 => "game",
            2 => "mods",
            _ => "java"
        };
        Completed = true;
        DialogResult = true;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
