using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.ViewModels;

namespace XdLauncher;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private bool _initialSizeApplied;
    private double _appliedScale = 1.0;
    private readonly List<System.Windows.Shapes.Ellipse> _particles = [];
    private readonly Random _particleRandom = new();
    private bool _autoSidebarCollapsed;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        PageHost.Margin = new Thickness(24, 24, 24, 14);

        var settings = viewModel.Settings.Settings;
        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;

        ApplyScale(initial: true);

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        viewModel.Settings.WindowPlacementChanged += ApplyWindowPlacement;
        viewModel.Settings.ScaleChanged += () => ApplyScale(initial: false);
        viewModel.Settings.AppearanceChanged += ApplyAmbientVisuals;
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += (_, _) => ApplyAmbientVisuals();
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ActualWidth < 1280 && !_viewModel.IsSidebarCollapsed)
        {
            _viewModel.IsSidebarCollapsed = true;
            _autoSidebarCollapsed = true;
        }
        else if (ActualWidth >= 1360 && _autoSidebarCollapsed && _viewModel.IsSidebarCollapsed)
        {
            _viewModel.IsSidebarCollapsed = false;
            _autoSidebarCollapsed = false;
        }
    }

    private void ApplyAmbientVisuals()
    {
        if (AmbientOrbA is not null) AmbientOrbA.Opacity = AnimationHelper.AmbientOpacity;
        if (AmbientOrbB is not null) AmbientOrbB.Opacity = AnimationHelper.AmbientOpacity * 0.75;
        if (AmbientOrbC is not null) AmbientOrbC.Opacity = AnimationHelper.AmbientOpacity * 0.45;
        RebuildParticles();
    }

    private void RebuildParticles()
    {
        if (ParticleCanvas is null)
            return;

        ParticleCanvas.Children.Clear();
        _particles.Clear();
        var count = AnimationHelper.ParticleCount;
        for (var i = 0; i < count; i++)
        {
            var size = _particleRandom.NextDouble() * 2.4 + 0.8;
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(40 + _particleRandom.Next(80)), 180, 240, 220)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, _particleRandom.NextDouble() * Math.Max(Width, 1200));
            Canvas.SetTop(dot, _particleRandom.NextDouble() * Math.Max(Height, 760));
            ParticleCanvas.Children.Add(dot);
            _particles.Add(dot);
        }
    }

    private void RootLayout_MouseMove(object sender, MouseEventArgs e)
    {
        if (!AnimationHelper.ParallaxEnabled || BackdropTranslate is null)
            return;

        var pos = e.GetPosition(RootLayout);
        var nx = (pos.X / Math.Max(RootLayout.ActualWidth, 1)) - 0.5;
        var ny = (pos.Y / Math.Max(RootLayout.ActualHeight, 1)) - 0.5;
        BackdropTranslate.X = nx * -24;
        BackdropTranslate.Y = ny * -18;
    }

    private void RootLayout_MouseLeave(object sender, MouseEventArgs e)
    {
        if (BackdropTranslate is null)
            return;
        BackdropTranslate.X = 0;
        BackdropTranslate.Y = 0;
    }

    /// <summary>Scales the whole interface and, on live changes, keeps the window filled.</summary>
    private void ApplyScale(bool initial)
    {
        var scale = Math.Clamp(_viewModel.Settings.UiScale, 0.85, 1.15);
        if (!initial && Math.Abs(scale - _appliedScale) > 0.001)
        {
            Width = Math.Max(MinWidth, Width * scale / _appliedScale);
            Height = Math.Max(MinHeight, Height * scale / _appliedScale);
        }

        _appliedScale = scale;
        RootLayout.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        if (!initial)
            ClampToWorkArea();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _initialSizeApplied = true;
        ClampToWorkArea();
        UpdateMaximizeGlyph();
        AnimatePage();
        await _viewModel.InitializeAsync();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.CurrentPage) or nameof(ShellViewModel.CurrentNavigation)
            or nameof(ShellViewModel.CurrentGroup) or nameof(ShellViewModel.TopBarStatusText) or nameof(ShellViewModel.IsFullscreen))
        {
            AnimatePage();
        }

        if (e.PropertyName == nameof(ShellViewModel.IsFullscreen) && PageHost is not null)
            PageHost.Margin = _viewModel.IsFullscreen ? new Thickness(0, 0, 0, 14) : new Thickness(24, 24, 24, 14);
    }

    /// <summary>
    /// Ejercita el pipeline de transformaciones del shell sin abrir la ventana.
    /// Lo usa el autodiagnóstico para detectar rutas RenderTransform inválidas.
    /// </summary>
    internal void RunVisualSmokeTest()
    {
        AnimatePage();
        if (PageHost.Content is FrameworkElement content)
            AnimationHelper.AnimateStagger(content);
    }

    private void AnimatePage()
    {
        if (PageHost is null)
            return;

        // Las animaciones previas se cancelan para no acumular transformaciones.
        PageHost.BeginAnimation(OpacityProperty, null);
        var scale = PageHost.RenderTransform is TransformGroup group && group.Children.Count > 0
            ? group.Children[0] as ScaleTransform
            : null;
        var translate = PageHost.RenderTransform is TransformGroup tg && tg.Children.Count > 1
            ? tg.Children[1] as TranslateTransform
            : PageHost.RenderTransform as TranslateTransform;

        scale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate?.BeginAnimation(TranslateTransform.XProperty, null);
        translate?.BeginAnimation(TranslateTransform.YProperty, null);

        if (AnimationHelper.ReduceMotion)
        {
            PageHost.Opacity = 1;
            return;
        }

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var direction = _viewModel.NavigationDirection >= 0 ? 1 : -1;
        var slide = new DoubleAnimation
        {
            From = 36 * direction,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(320),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleIn = new DoubleAnimation
        {
            From = 0.988,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(340),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        PageHost.BeginAnimation(OpacityProperty, fade);
        if (scale is not null)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
        }
        if (translate is not null && AnimationHelper.NavTransitionsEnabled)
            translate.BeginAnimation(TranslateTransform.XProperty, slide);

        // Aparición escalonada de las tarjetas del dashboard recién cargado.
        if (AnimationHelper.CardAnimationsEnabled && PageHost.Content is FrameworkElement content)
            Dispatcher.BeginInvoke(new Action(() => AnimationHelper.AnimateStagger(content)), DispatcherPriority.ContextIdle);
    }

    private void ClampToWorkArea()
    {
        if (!_initialSizeApplied || WindowState == WindowState.Maximized)
            return;

        var work = SystemParameters.WorkArea;
        var maxWidth = Math.Max(MinWidth, work.Width - 24);
        var maxHeight = Math.Max(MinHeight, work.Height - 24);
        if (Width > maxWidth)
            Width = maxWidth;
        if (Height > maxHeight)
            Height = maxHeight;

        if (Left + Width > work.Right)
            Left = work.Right - Width;
        if (Top + Height > work.Bottom)
            Top = work.Bottom - Height;
        if (Left < work.Left)
            Left = work.Left;
        if (Top < work.Top)
            Top = work.Top;
    }

    private void ApplyWindowPlacement()
    {
        var settings = _viewModel.Settings.Settings;
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;

        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);
        ClampToWorkArea();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = _viewModel.Settings.Settings;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = (int)Math.Round(Width);
            settings.WindowHeight = (int)Math.Round(Height);
        }
        _viewModel.Settings.Save();
        _viewModel.Shutdown();
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeButton is not null)
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void ExitFullscreenClick(object sender, RoutedEventArgs e)
        => _viewModel.IsFullscreen = false;

    private void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove can throw if the mouse button was released before the call.
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
