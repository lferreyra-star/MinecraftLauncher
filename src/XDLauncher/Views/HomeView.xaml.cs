using System.Windows;
using System.Windows.Controls;
using XdLauncher.Core;

namespace XdLauncher.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => AnimateDashboard();
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
