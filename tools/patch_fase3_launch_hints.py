import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/ViewModels/JavaViewModel.cs'
s = read(p)
anchor = '''    public int MemoryMb
'''
addition = '''    public bool CanLaunch => PrimaryCommand.CanExecute(null);

    public string LaunchHint
    {
        get
        {
            if (IsBusy)
                return "Hay una operación en curso. Espera a que termine.";
            if (IsSelectedRunning)
                return "El juego ya está en ejecución. Usa Cerrar juego para volver a lanzarlo.";
            if (SelectedVersion is null)
                return "Selecciona una versión instalada para habilitar JUGAR.";
            if (!SelectedVersion.IsInstalled)
                return "Instala la versión seleccionada para habilitar JUGAR.";
            if (IsSelectedFabricPending)
                return "Instala Fabric para este perfil antes de jugar.";
            if (AccountWarningVisible)
                return "Inicia sesión con Microsoft para usar el workspace Premium.";
            return "Listo para jugar.";
        }
    }

'''
assert anchor in s
s = s.replace(anchor, addition + anchor, 1)
anchor = '        DetectJavaCommand.RaiseCanExecuteChanged();\n    }\n'
replacement = '''        DetectJavaCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanLaunch));
        OnPropertyChanged(nameof(LaunchHint));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }
'''
assert anchor in s
s = s.replace(anchor, replacement, 1)
write(p, s)

p = 'src/XDLauncher/Views/JavaView.xaml'
s = read(p)
old = '<Setter Property="ToolTip" Value="Selecciona e instala una versión" />'
new = '<Setter Property="ToolTip" Value="{Binding LaunchHint}" />'
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/Views/JavaView.xaml.cs'
s = read(p)
s = s.replace('using System.Windows.Controls;\n', 'using System.Windows.Controls;\nusing XdLauncher.ViewModels;\n', 1)
s = s.replace('    private bool? _compact;\n', '    private bool? _compact;\n    private bool _loadedOnce;\n', 1)
old = '''        ApplyResponsiveLayout();
        LayoutRoot.SizeChanged += OnLayoutRootSizeChanged;
'''
new = '''        ApplyResponsiveLayout();
        LayoutRoot.SizeChanged += OnLayoutRootSizeChanged;
        if (!_loadedOnce && DataContext is JavaViewModel viewModel)
        {
            _loadedOnce = true;
            _ = viewModel.EnsureLoadedAsync();
        }
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/ViewModels/HomeViewModel.cs'
s = read(p)
anchor = '''    public string MainActionGlyph => SelectedEditionId switch
'''
addition = '''    public string MainActionHint => SelectedEditionId switch
    {
        "java" => JavaPage?.SelectedVersion is { IsInstalled: true, IsBusy: false } && !JavaPage.IsBusy
            ? "Abre Minecraft Java con la versión seleccionada."
            : "Elige una versión instalada en Java Edition para habilitar JUGAR.",
        "android" => "Conecta el teléfono por USB o WiFi para jugar en streaming.",
        _ => IsInstalled ? "Abre Minecraft Bedrock." : "Abre Microsoft Store para instalar Minecraft Bedrock."
    };

'''
assert anchor in s
s = s.replace(anchor, addition + anchor, 1)
s = s.replace('            OnPropertyChanged(nameof(MainActionGlyph));\n', '            OnPropertyChanged(nameof(MainActionGlyph));\n            OnPropertyChanged(nameof(MainActionHint));\n')
s = s.replace('        OnPropertyChanged(nameof(MainActionGlyph));\n        OnPropertyChanged(nameof(ShowVerify));', '        OnPropertyChanged(nameof(MainActionGlyph));\n        OnPropertyChanged(nameof(MainActionHint));\n        OnPropertyChanged(nameof(ShowVerify));')
write(p, s)

p = 'src/XDLauncher/Views/HomeView.xaml'
s = read(p)
old = '<Button Style="{StaticResource PrimaryButton}" Height="48" MinWidth="210"\n                                        Content="{Binding MainActionLabel}" Command="{Binding MainActionCommand}" />'
new = '<Button Style="{StaticResource PrimaryButton}" Height="48" MinWidth="210"\n                                        Content="{Binding MainActionLabel}" Command="{Binding MainActionCommand}"\n                                        ToolTip="{Binding MainActionHint}" ToolTipService.ShowOnDisabled="True" />'
assert old in s
s = s.replace(old, new, 1)
write(p, s)
