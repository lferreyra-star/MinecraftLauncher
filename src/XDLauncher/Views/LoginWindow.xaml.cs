using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly Random _random = new();

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Authenticated += _ => { };
        Loaded += async (_, _) =>
        {
            await _viewModel.CheckServerAsync();
            StartParticles();
        };
        Closing += (_, _) =>
        {
            _viewModel.CancelPendingOperations();
            _viewModel.Authenticated -= _ => { };
        };
    }

    public bool IsAuthenticated { get; private set; }

    public void CompleteExternalLogin(XdAccount account) => _viewModel.CompleteExternalLogin(account);

    private void StartParticles()
    {
        var accent = Application.Current?.TryFindResource("AccentColor") is Color color
            ? color
            : Color.FromRgb(0x5C, 0xE0, 0x7A);

        var width = Math.Max(ActualWidth, 960);
        var height = Math.Max(ActualHeight, 640);

        for (var i = 0; i < AnimationHelper.ParticleCount; i++)
        {
            var size = _random.NextDouble() * 1.8 + 0.6;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(18 + _random.Next(28)), accent.R, accent.G, accent.B)),
                IsHitTestVisible = false,
                Opacity = 0.55
            };

            var startTop = _random.NextDouble() * height;
            Canvas.SetLeft(dot, _random.NextDouble() * width);
            Canvas.SetTop(dot, startTop);
            ParticleLayer.Children.Add(dot);

            var duration = TimeSpan.FromSeconds(14 + _random.NextDouble() * 14);
            var animation = new DoubleAnimation
            {
                From = startTop,
                To = -20,
                Duration = duration,
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            dot.BeginAnimation(Canvas.TopProperty, animation);
        }
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TogglePasswordCommand.CanExecute(null))
            _viewModel.TogglePasswordCommand.Execute(null);
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.Password = SecretBox.Password;

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmBox.Password;

    private void ContinueWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ContinueWithGoogleCommand.CanExecute(null))
            _viewModel.ContinueWithGoogleCommand.Execute(null);
    }

    private void OpenFirebaseConsole_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.OpenFirebaseConsoleCommand.CanExecute(null))
            _viewModel.OpenFirebaseConsoleCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRegisterMode)
        {
            _viewModel.RegisterCommand.Execute(null);
        }
        else
        {
            _viewModel.SignInCommand.Execute(null);
        }
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRegisterMode)
            _viewModel.SwitchModeCommand.Execute(null);
        else
            _viewModel.RegisterCommand.Execute(null);
    }
}
