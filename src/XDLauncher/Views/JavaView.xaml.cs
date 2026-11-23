using System.Windows;
using System.Windows.Controls;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class JavaView : UserControl
{
    private bool? _compact;
    private bool _loadedOnce;

    public JavaView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        LayoutRoot.SizeChanged += OnLayoutRootSizeChanged;
        if (!_loadedOnce && DataContext is JavaViewModel viewModel)
        {
            _loadedOnce = true;
            _ = viewModel.EnsureLoadedAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayoutRoot.SizeChanged -= OnLayoutRootSizeChanged;
    }

    private void OnLayoutRootSizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (LayoutRoot is null || LaunchPanel is null || LayoutRoot.ActualWidth <= 0)
            return;

        var compact = LayoutRoot.ActualWidth < 1080;
        if (_compact == compact)
            return;

        _compact = compact;
        LaunchColumn.Width = new GridLength(compact ? 0 : 380);
        MainRow.Height = GridLength.Auto;
        LaunchRow.Height = compact ? GridLength.Auto : new GridLength(0);
        Grid.SetRow(LaunchPanel, 0);
        Grid.SetColumn(LaunchPanel, compact ? 0 : 1);
        Grid.SetRow(LeftColumn, compact ? 1 : 0);
        Grid.SetColumn(LeftColumn, 0);
        LaunchPanel.Margin = compact ? new Thickness(0, 0, 0, 16) : new Thickness(16, 0, 0, 0);
        LaunchPanel.MaxWidth = compact ? 560 : 420;
        LaunchPanel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
    }
}
