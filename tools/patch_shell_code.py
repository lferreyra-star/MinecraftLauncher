import io

p = 'src/XDLauncher/MainWindow.xaml.cs'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '''    private bool _autoSidebarCollapsed;
''',
    '''    private bool _autoSidebarCollapsed;
    private object? _currentPage;
    private string? _lastBackgroundPath;
''',
    1,
)

s = s.replace(
    '''        DataContext = viewModel;
        PageHost.Margin = new Thickness(24, 24, 24, 14);
''',
    '''        DataContext = viewModel;
        PageStage.Margin = new Thickness(24, 24, 24, 14);
        _currentPage = viewModel.CurrentPage;
        PageHost.Content = _currentPage;
''',
    1,
)

s = s.replace(
    '''        SizeChanged += MainWindow_SizeChanged;
    }
''',
    '''        SizeChanged += MainWindow_SizeChanged;
        NavList.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(UpdateNavIndicator), DispatcherPriority.Loaded);
        NavStage.Loaded += (_, _) => UpdateNavIndicator();
        NavList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => UpdateNavIndicator()));
    }
''',
    1,
)

s = s.replace(
    '''        _initialSizeApplied = true;
        ClampToWorkArea();
        UpdateMaximizeGlyph();
        AnimatePage();
        await _viewModel.InitializeAsync();''',
    '''        _initialSizeApplied = true;
        ClampToWorkArea();
        UpdateMaximizeGlyph();
        UpdateNavIndicator();
        AnimatePage(initial: true);
        _lastBackgroundPath = _viewModel.CurrentBackgroundPath;
        AnimationHelper.AnimateBackground(BackdropImage, 0.55, 0);
        await _viewModel.InitializeAsync();''',
    1,
)

old_changed = '''    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.CurrentPage) or nameof(ShellViewModel.CurrentNavigation)
            or nameof(ShellViewModel.CurrentGroup) or nameof(ShellViewModel.TopBarStatusText) or nameof(ShellViewModel.IsFullscreen))
        {
            AnimatePage();
        }

        if (e.PropertyName == nameof(ShellViewModel.IsFullscreen) && PageHost is not null)
            PageHost.Margin = _viewModel.IsFullscreen ? new Thickness(0, 0, 0, 14) : new Thickness(24, 24, 24, 14);
    }
'''
new_changed = '''    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
            TransitionToPage(_viewModel.CurrentPage);

        if (e.PropertyName is nameof(ShellViewModel.CurrentNavigation)
            or nameof(ShellViewModel.CurrentGroup) or nameof(ShellViewModel.TopBarStatusText))
        {
            AnimateHeaderOnly();
            UpdateNavIndicator();
        }

        if (e.PropertyName == nameof(ShellViewModel.CurrentBackgroundPath))
            AnimateBackdrop();

        if (e.PropertyName == nameof(ShellViewModel.IsFullscreen) && PageStage is not null)
            PageStage.Margin = _viewModel.IsFullscreen ? new Thickness(0, 0, 0, 14) : new Thickness(24, 24, 24, 14);
    }

    private void TransitionToPage(object newPage)
    {
        if (PageHost is null || ReferenceEquals(newPage, _currentPage))
            return;

        var direction = _viewModel.NavigationDirection >= 0 ? 1 : -1;
        var outgoing = PageHost.Content as UIElement;

        if (outgoing is not null)
        {
            PreviousPageHost.Content = outgoing;
            PreviousPageHost.Opacity = AnimationHelper.MotionOff ? 0 : 1;
            AnimationHelper.AnimateTransitionExit(PreviousPageHost, direction, () =>
            {
                PreviousPageHost.Content = null;
                PreviousPageHost.Opacity = 0;
            });
        }

        PageHost.Content = newPage;
        _currentPage = newPage;
        AnimatePage();
    }

    private void AnimateBackdrop()
    {
        var path = _viewModel.CurrentBackgroundPath;
        if (string.Equals(path, _lastBackgroundPath, StringComparison.Ordinal))
            return;

        _lastBackgroundPath = path;
        var direction = _viewModel.NavigationDirection >= 0 ? 1 : -1;
        AnimationHelper.AnimateBackground(BackdropImage, 0.55, direction);
    }

    private void AnimateHeaderOnly()
    {
        if (HeaderPanel is null)
            return;
        var direction = _viewModel.NavigationDirection >= 0 ? 1 : -1;
        AnimationHelper.AnimateHeader(HeaderPanel, direction);
    }

    private void UpdateNavIndicator()
    {
        if (NavList?.SelectedItem is null || NavStage is null || NavIndicator is null || NavIndicatorTranslate is null)
            return;

        if (NavList.ItemContainerGenerator.ContainerFromItem(NavList.SelectedItem) is not FrameworkElement container)
        {
            Dispatcher.BeginInvoke(new Action(UpdateNavIndicator), DispatcherPriority.Loaded);
            return;
        }

        var point = container.TransformToVisual(NavStage).Transform(new Point(0, 0));
        var target = point.Y + Math.Max(0, (container.ActualHeight - 40) / 2);

        if (AnimationHelper.MotionOff)
        {
            NavIndicatorTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            NavIndicatorTranslate.Y = target;
            NavIndicator.Opacity = 1;
            return;
        }

        NavIndicator.Opacity = 1;
        NavIndicatorTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        NavIndicatorTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = NavIndicatorTranslate.Y,
            To = target,
            Duration = AnimationHelper.PageEnterDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
'''
assert old_changed in s
s = s.replace(old_changed, new_changed, 1)

old_animate = s[s.index('    private void AnimatePage()'):s.index('    private void ClampToWorkArea()')]
new_animate = '''    private void AnimatePage(bool initial = false)
    {
        if (PageHost is null)
            return;

        var direction = _viewModel.NavigationDirection >= 0 ? 1 : -1;
        AnimationHelper.AnimateTransitionEnter(PageHost, direction, initial);
        AnimateHeaderOnly();

        if (AnimationHelper.CardAnimationsEnabled && !initial && PageHost.Content is FrameworkElement content)
            Dispatcher.BeginInvoke(new Action(() => AnimationHelper.AnimateStagger(content)), DispatcherPriority.ContextIdle);
    }

'''
s = s.replace(old_animate, new_animate, 1)

io.open(p, 'w', encoding='utf-8').write(s)
