import io

p = 'src/XDLauncher/ViewModels/ShellViewModel.cs'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '    public IReadOnlyList<NavigationItem> Navigation { get; }\n',
    '''    public IReadOnlyList<NavigationItem> Navigation { get; }
    public IReadOnlyList<NavigationItem> QuickNavigation { get; private set; } = [];
''',
    1,
)

s = s.replace(
    '        _currentNavigation = Navigation[0];\n',
    '''        var quickKeys = new[] { "home", "java", "bedrock", "versions", "mods", "downloads", "settings" };
        QuickNavigation = Navigation.Where(item => quickKeys.Contains(item.Key)).ToList();
        _currentNavigation = Navigation[0];
''',
    1,
)

s = s.replace(
    '                OnPropertyChanged(nameof(CurrentGroup));\n',
    '                OnPropertyChanged(nameof(CurrentGroup));\n                OnPropertyChanged(nameof(CurrentNavigationKey));\n',
    1,
)

s = s.replace(
    '    public object CurrentPage => CurrentNavigation.Page;\n',
    '''    public string CurrentNavigationKey
    {
        get => CurrentNavigation?.Key ?? "home";
        set
        {
            if (!string.Equals(value, CurrentNavigation?.Key, StringComparison.OrdinalIgnoreCase))
                Navigate(value);
        }
    }

    public object CurrentPage => CurrentNavigation.Page;
''',
    1,
)

io.open(p, 'w', encoding='utf-8').write(s)
