using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using XdLauncher.Core;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) =>
        {
            UpdateCategoryArrows();
            CategoryList.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler((_, __) => UpdateCategoryArrows()));
        };
        Unloaded += (_, _) => Detach();
    }

    private void CategoryLeft_Click(object sender, RoutedEventArgs e)
        => FindVisualChild<ScrollViewer>(CategoryList)?.PageLeft();

    private void CategoryRight_Click(object sender, RoutedEventArgs e)
        => FindVisualChild<ScrollViewer>(CategoryList)?.PageRight();

    private void UpdateCategoryArrows()
    {
        var scroller = FindVisualChild<ScrollViewer>(CategoryList);
        if (scroller is null)
            return;

        CategoryLeft.IsEnabled = scroller.HorizontalOffset > 0.5;
        CategoryRight.IsEnabled = scroller.HorizontalOffset + scroller.ViewportWidth < scroller.ExtentWidth - 0.5;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as SettingsViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void Detach()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.SelectedCategory))
            return;

        if (SettingsContentRoot is null)
            return;

        AnimationHelper.AnimateEntrance(SettingsContentRoot, 18, AnimationHelper.PageEnterDuration);
    }
}
