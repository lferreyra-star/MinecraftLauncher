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

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

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
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
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
        if (e.PropertyName is nameof(ShellViewModel.CurrentPage) or nameof(ShellViewModel.CurrentNavigation))
            AnimatePage();
        else if (e.PropertyName is nameof(ShellViewModel.IsSidebarCollapsed) or nameof(ShellViewModel.SidebarWidth))
            AnimateSidebar();
    }

    private void AnimatePage()
    {
        if (PageHost is null)
            return;

        // Las animaciones previas se cancelan para no acumular transformaciones.
        PageHost.BeginAnimation(OpacityProperty, null);
        if (PageHost.RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.YProperty, null);

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation
        {
            From = 16,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        PageHost.BeginAnimation(OpacityProperty, fade);
        if (PageHost.RenderTransform is TranslateTransform tt)
            tt.BeginAnimation(TranslateTransform.YProperty, slide);

        // Aparición escalonada de las tarjetas del dashboard recién cargado.
        if (PageHost.Content is FrameworkElement content)
            Dispatcher.BeginInvoke(new Action(() => AnimationHelper.AnimateStagger(content)), DispatcherPriority.ContextIdle);
    }

    private void AnimateSidebar()
    {
        if (SidebarBorder is null)
            return;

        var target = _viewModel.SidebarWidth;
        SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        SidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
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
