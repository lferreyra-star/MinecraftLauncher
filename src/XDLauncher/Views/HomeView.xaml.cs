using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using XdLauncher.Core;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _home;

    public HomeView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_home is not null)
                _home.PropertyChanged -= OnHomePropertyChanged;
            _home = DataContext as HomeViewModel;
            if (_home is not null)
                _home.PropertyChanged += OnHomePropertyChanged;
        };
        Loaded += (_, _) => AnimateDashboard();
    }

    private void OnHomePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.CurrentBannerPath) ||
            !AnimationHelper.CardAnimationsEnabled ||
            AnimationHelper.ReduceMotion)
            return;

        BannerRoot?.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0.55,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(480),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        if (BannerContent is not null)
            AnimationHelper.AnimateEntrance(BannerContent, 18, TimeSpan.FromMilliseconds(420));
    }

    /// <summary>
    /// Aparición escalonada de las tarjetas del dashboard. Solo usa opacidad y
    /// desplazamiento, así que no bloquea el hilo de la interfaz.
    /// </summary>
    private void AnimateDashboard()
    {
        if (DashboardRoot is null)
            return;

        var index = 0;
        foreach (var child in AnimationHelper.EnumerateChildren(DashboardRoot))
        {
            if (child is not FrameworkElement element)
                continue;

            AnimationHelper.AnimateEntrance(element, 26, TimeSpan.FromMilliseconds(320), Math.Min(index * 60, 420));
            index++;
        }
    }
}
