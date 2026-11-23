import io
p = 'src/XDLauncher/ViewModels/SettingsState.cs'
s = io.open(p, encoding='utf-8-sig').read()

old = '''        Backgrounds = new ObservableCollection<BackgroundOption>
        {
            new() { Id = "bosque", Name = "Bosque profundo", Path = Paths.GetBuiltInBackground("bosque") ?? string.Empty },
            new() { Id = "aurora", Name = "Aurora boreal", Path = Paths.GetBuiltInBackground("aurora") ?? string.Empty },
            new() { Id = "amanecer", Name = "Amanecer", Path = Paths.GetBuiltInBackground("amanecer") ?? string.Empty },
            new() { Id = "cueva", Name = "Cueva de cristal", Path = Paths.GetBuiltInBackground("cueva") ?? string.Empty }
        };'''
new = '''        Backgrounds = new ObservableCollection<BackgroundOption>
        {
            new() { Id = "bosque", Name = "Bosque cinematográfico", Path = Paths.GetBuiltInBackground("bosque") ?? string.Empty },
            new() { Id = "aurora", Name = "Aurora boreal", Path = Paths.GetBuiltInBackground("aurora") ?? string.Empty },
            new() { Id = "amanecer", Name = "Amanecer en las montañas", Path = Paths.GetBuiltInBackground("amanecer") ?? string.Empty },
            new() { Id = "cueva", Name = "Cueva de cristal", Path = Paths.GetBuiltInBackground("cueva") ?? string.Empty },
            new() { Id = "nether", Name = "Nether cinematográfico", Path = Paths.GetBuiltInBackground("nether") ?? string.Empty },
            new() { Id = "end", Name = "El End", Path = Paths.GetBuiltInBackground("end") ?? string.Empty },
            new() { Id = "cerezo", Name = "Bioma de cerezo", Path = Paths.GetBuiltInBackground("cerezo") ?? string.Empty },
            new() { Id = "nevado", Name = "Montañas nevadas", Path = Paths.GetBuiltInBackground("nevado") ?? string.Empty },
            new() { Id = "ciudad", Name = "Ciudad nocturna", Path = Paths.GetBuiltInBackground("ciudad") ?? string.Empty },
            new() { Id = "nocturno", Name = "Bosque nocturno", Path = Paths.GetBuiltInBackground("nocturno") ?? string.Empty },
            new() { Id = "magico", Name = "Bosque mágico", Path = Paths.GetBuiltInBackground("magico") ?? string.Empty },
            new() { Id = "fantastico", Name = "Mundos fantásticos", Path = Paths.GetBuiltInBackground("fantastico") ?? string.Empty }
        };'''
assert old in s
s = s.replace(old, new)

old = '''    public string Theme
    {
        get => Settings.Theme;
        set
        {
            if (value is not ("Dark" or "Light") || Settings.Theme == value)
                return;
            Settings.Theme = value;
            SaveAndNotify(nameof(Theme), nameof(IsDarkTheme), nameof(IsLightTheme), nameof(ThemeLabel));
            ApplyTheme();
            AppearanceChanged?.Invoke();
        }
    }

    public bool IsDarkTheme
    {
        get => Theme == "Dark";
        set { if (value) Theme = "Dark"; }
    }

    public bool IsLightTheme
    {
        get => Theme == "Light";
        set { if (value) Theme = "Light"; }
    }

    public string ThemeLabel => Theme == "Light" ? "Claro" : "Oscuro";'''
new = '''    public string Theme
    {
        get => Settings.Theme;
        set
        {
            if (!ThemeManager.ThemeNames.Contains(value, StringComparer.OrdinalIgnoreCase) || Settings.Theme == value)
                return;
            Settings.Theme = value;
            SaveAndNotify(nameof(Theme), nameof(IsDarkTheme), nameof(IsLightTheme), nameof(ThemeLabel));
            ApplyTheme();
            AppearanceChanged?.Invoke();
        }
    }

    public bool IsDarkTheme => !ThemeManager.IsLight(Theme);

    public bool IsLightTheme => ThemeManager.IsLight(Theme);

    public string ThemeLabel => Theme;'''
assert old in s
s = s.replace(old, new)

visual = '''
    public double AmbientIntensity
    {
        get => Settings.AmbientIntensity;
        set
        {
            var v = Math.Clamp(value, 0, 1);
            if (Math.Abs(Settings.AmbientIntensity - v) < 0.001) return;
            Settings.AmbientIntensity = v;
            ApplyVisualSettings();
            SaveAndNotify(nameof(AmbientIntensity));
        }
    }

    public double TransparencyLevel
    {
        get => Settings.TransparencyLevel;
        set
        {
            var v = Math.Clamp(value, 0, 1);
            if (Math.Abs(Settings.TransparencyLevel - v) < 0.001) return;
            Settings.TransparencyLevel = v;
            ApplyVisualSettings();
            SaveAndNotify(nameof(TransparencyLevel));
        }
    }

    public bool NavTransitionsEnabled
    {
        get => Settings.NavTransitionsEnabled;
        set { Settings.NavTransitionsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(NavTransitionsEnabled)); }
    }

    public bool ParticleEffectsEnabled
    {
        get => Settings.ParticleEffectsEnabled;
        set { Settings.ParticleEffectsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(ParticleEffectsEnabled)); }
    }

    public bool HoverEffectsEnabled
    {
        get => Settings.HoverEffectsEnabled;
        set { Settings.HoverEffectsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(HoverEffectsEnabled)); }
    }

    public bool ParallaxEnabled
    {
        get => Settings.ParallaxEnabled;
        set { Settings.ParallaxEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(ParallaxEnabled)); }
    }

    public bool CardAnimationsEnabled
    {
        get => Settings.CardAnimationsEnabled;
        set { Settings.CardAnimationsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(CardAnimationsEnabled)); }
    }

    public bool LoadingAnimationsEnabled
    {
        get => Settings.LoadingAnimationsEnabled;
        set { Settings.LoadingAnimationsEnabled = value; ApplyVisualSettings(); SaveAndNotify(nameof(LoadingAnimationsEnabled)); }
    }

    public bool PerformanceMode
    {
        get => Settings.PerformanceMode;
        set
        {
            Settings.PerformanceMode = value;
            if (value)
            {
                Settings.ParticleEffectsEnabled = false;
                Settings.ParallaxEnabled = false;
                Settings.CardAnimationsEnabled = false;
                Settings.AmbientEffectsEnabled = false;
            }
            ApplyVisualSettings();
            SaveAndNotify(nameof(PerformanceMode), nameof(ParticleEffectsEnabled),
                nameof(ParallaxEnabled), nameof(CardAnimationsEnabled), nameof(AmbientEffectsEnabled));
        }
    }

    public string? GetBackgroundPathForSection(string section)
    {
        if (Settings.BackgroundId == "custom" && !string.IsNullOrWhiteSpace(Settings.CustomBackgroundPath))
            return Settings.CustomBackgroundPath;

        var id = section switch
        {
            "java" => "cueva",
            "game" => "ciudad",
            "versions" => "amanecer",
            "addons" => "cerezo",
            "mods" => "magico",
            "downloads" => "aurora",
            "settings" => "fantastico",
            "profile" => "end",
            "mobile" => "nocturno",
            _ => Settings.BackgroundId
        };
        return Paths.GetBuiltInBackground(id) ?? Paths.GetBuiltInBackground("bosque");
    }
'''
anchor = '    public string AmbientEffectsEnabledLabel => Settings.AmbientEffectsEnabled ? "Activados" : "Desactivados";\n'
assert anchor in s
s = s.replace(anchor, anchor + visual)

apply_visual = '''
    private void ApplyVisualSettings()
    {
        AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled && !Settings.PerformanceMode;
        AnimationHelper.AmbientIntensity = Settings.AmbientIntensity;
        AnimationHelper.TransparencyLevel = Settings.TransparencyLevel;
        AnimationHelper.NavTransitionsEnabled = Settings.NavTransitionsEnabled && !Settings.PerformanceMode;
        AnimationHelper.ParticleEffectsEnabled = Settings.ParticleEffectsEnabled && !Settings.PerformanceMode;
        AnimationHelper.HoverEffectsEnabled = Settings.HoverEffectsEnabled;
        AnimationHelper.ParallaxEnabled = Settings.ParallaxEnabled && !Settings.PerformanceMode;
        AnimationHelper.CardAnimationsEnabled = Settings.CardAnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.LoadingAnimationsEnabled = Settings.LoadingAnimationsEnabled && !Settings.PerformanceMode;
        AnimationHelper.PerformanceMode = Settings.PerformanceMode;
    }

'''
anchor = '    private void ApplyTheme()\n'
assert anchor in s
s = s.replace(anchor, apply_visual + anchor, 1)

assert '            AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled;\n            AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled;\n            ThemeManager.SetCustomAccent(Settings.CustomAccentHex);' in s
s = s.replace('            AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled;\n            AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled;\n            ThemeManager.SetCustomAccent(Settings.CustomAccentHex);',
              '            ApplyVisualSettings();\n            ThemeManager.SetCustomAccent(Settings.CustomAccentHex);')

assert '        AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled;\n        AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled;\n        OnPropertyChanged(string.Empty);' in s
s = s.replace('        AnimationHelper.AnimationsEnabled = Settings.AnimationsEnabled;\n        AnimationHelper.AmbientEffectsEnabled = Settings.AmbientEffectsEnabled;\n        OnPropertyChanged(string.Empty);',
              '        ApplyVisualSettings();\n        ThemeManager.Apply(Settings.Theme, Settings.Accent);\n        OnPropertyChanged(string.Empty);')

io.open(p, 'w', encoding='utf-8').write(s)
print('patched settings state')
