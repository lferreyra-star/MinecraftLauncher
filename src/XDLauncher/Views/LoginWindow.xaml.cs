using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using XdLauncher.Core;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;
    private readonly Random _random = new();
    private bool _brandVisible = true;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += async (_, _) =>
        {
            ApplyResponsiveLayout();
            await _viewModel.CheckServerAsync();
            StartParticles();
        };
        Closing += (_, _) =>
        {
            _viewModel.CancelPendingOperations();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    public bool IsAuthenticated { get; private set; }

    public void CompleteExternalLogin(XdAccount account) => _viewModel.CompleteExternalLogin(account);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LoginViewModel.IsRegisterMode) && FormContent.IsLoaded)
            AnimationHelper.AnimateEntrance(FormContent, 12, AnimationHelper.PageEnterDuration);
    }

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (RootLayout is null || RootLayout.ActualWidth <= 0)
            return;

        var visible = RootLayout.ActualWidth >= 760;
        if (visible == _brandVisible)
            return;

        _brandVisible = visible;
        BrandPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        BrandColumn.Width = visible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        FormColumn.Width = new GridLength(1.2, GridUnitType.Star);
    }

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
        if (_viewModel.ShowPassword)
        {
            VisibleSecretBox.Text = SecretBox.Password;
            SecretBox.Visibility = Visibility.Collapsed;
            VisibleSecretBox.Visibility = Visibility.Visible;
            VisibleSecretBox.Focus();
            VisibleSecretBox.CaretIndex = VisibleSecretBox.Text.Length;
        }
        else
        {
            SecretBox.Password = VisibleSecretBox.Text;
            VisibleSecretBox.Visibility = Visibility.Collapsed;
            SecretBox.Visibility = Visibility.Visible;
            SecretBox.Focus();
        }

        if (_viewModel.TogglePasswordCommand.CanExecute(null))
            _viewModel.TogglePasswordCommand.Execute(null);
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        SecretBox.Tag = SecretBox.Password;
        _viewModel.Password = SecretBox.Password;
    }

    private void VisibleSecretBox_TextChanged(object sender, TextChangedEventArgs e)
        => _viewModel.Password = VisibleSecretBox.Text;

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ConfirmBox.Tag = ConfirmBox.Password;
        _viewModel.ConfirmPassword = ConfirmBox.Password;
    }

    private void ContinueWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ContinueWithGoogleCommand.CanExecute(null))
            _viewModel.ContinueWithGoogleCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void FormContent_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel.IsBusy)
            return;
        Primary_Click(sender, e);
        e.Handled = true;
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRegisterMode)
            _viewModel.RegisterCommand.Execute(null);
        else
            _viewModel.SignInCommand.Execute(null);
    }
}
