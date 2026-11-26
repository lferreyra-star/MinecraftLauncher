import io

p = 'src/XDLauncher/Core/Converters.cs'
s = io.open(p, encoding='utf-8-sig').read()
s += '''
public sealed class PercentToGaugeOffsetConverter : System.Windows.Data.IValueConverter
{
    private const double Circumference = 349;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var percent = value is double number ? number : 0;
        percent = Math.Clamp(percent, 0, 100);
        return Circumference * (1 - percent / 100d);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
'''
io.open(p, 'w', encoding='utf-8').write(s)

p = 'src/XDLauncher/Services/SystemInfoService.cs'
s = io.open(p, encoding='utf-8-sig').read()
s = s.replace(
    '''    public string RamUsageText => TotalRamBytes == 0
        ? "—"
        : $"{Math.Round((TotalRamBytes - AvailableRamBytes) * 100d / TotalRamBytes)}% en uso";''',
    '''    public string RamUsageText => TotalRamBytes == 0
        ? "—"
        : $"{Math.Round(RamUsagePercent)}% en uso";

    public double RamUsagePercent => TotalRamBytes == 0
        ? 0
        : Math.Clamp((TotalRamBytes - AvailableRamBytes) * 100d / TotalRamBytes, 0, 100);''',
    1,
)
io.open(p, 'w', encoding='utf-8').write(s)

p = 'src/XDLauncher/ViewModels/SettingsState.cs'
s = io.open(p, encoding='utf-8-sig').read()
s = s.replace(
    '    public string LauncherMemoryText => $"{LauncherMemoryBytes / 1024d / 1024d:0.#} MB";\n',
    '''    public string LauncherMemoryText => $"{LauncherMemoryBytes / 1024d / 1024d:0.#} MB";

    public double LauncherMemoryPercent => SystemInfo.TotalRamBytes == 0
        ? 0
        : Math.Clamp(LauncherMemoryBytes * 100d / SystemInfo.TotalRamBytes, 0, 100);
''',
    1,
)
s = s.replace(
    '        OnPropertyChanged(nameof(LauncherMemoryText));\n    }',
    '''        OnPropertyChanged(nameof(LauncherMemoryText));
        OnPropertyChanged(nameof(LauncherMemoryPercent));
    }''',
    1,
)
io.open(p, 'w', encoding='utf-8').write(s)
