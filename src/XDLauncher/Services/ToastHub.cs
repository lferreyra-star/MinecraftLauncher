using System.Windows;
using XdLauncher.Models;

namespace XdLauncher.Services;

/// <summary>Lightweight app-wide notification bus.</summary>
public static class ToastHub
{
    public static event Action<ToastMessage>? ToastRequested;

    public static void Success(string message) => Raise(new ToastMessage { Message = message, IconGlyph = "\uE73E" });
    public static void Info(string message) => Raise(new ToastMessage { Message = message, IconGlyph = "\uE946" });
    public static void Warning(string message) => Raise(new ToastMessage { Message = message, IconGlyph = "\uE7BA" });
    public static void Error(string message) => Raise(new ToastMessage { Message = message, IconGlyph = "\uEA39", IsError = true });

    private static void Raise(ToastMessage toast)
    {
        LogService.Info($"Notificación: {toast.Message}");
        ToastRequested?.Invoke(toast);
    }
}

/// <summary>Switches theme dictionaries and applies accent palettes at runtime.</summary>
public static class ThemeManager
{
    private static readonly Dictionary<string, (string Accent, string Strong, string Light, string Soft, string Text)> Accents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Verde"] = ("#5CE07A", "#35C25B", "#9CF2AC", "#1D3A26", "#07130A"),
        ["Esmeralda"] = ("#2DD4A7", "#18B78D", "#7BEBCB", "#153A33", "#041612"),
        ["Lima"] = ("#B4E64B", "#94C62E", "#D3F58C", "#303B17", "#121703"),
        ["Cian"] = ("#45C8F5", "#22A7D6", "#96E2FA", "#12323C", "#031219")
    };

    public static string CurrentTheme { get; private set; } = "Dark";

    public static void Apply(string theme, string accent)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var normalized = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

        // Only swap the theme dictionary when the theme really changes: replacing it forces
        // every DynamicResource in the interface to be resolved again.
        if (!string.Equals(CurrentTheme, normalized, StringComparison.Ordinal))
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var themeDictionary = dictionaries.FirstOrDefault(d =>
                d.Source?.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase) == true &&
                !d.Source.OriginalString.Contains("Shared.xaml", StringComparison.OrdinalIgnoreCase));

            var newTheme = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{normalized}.xaml", UriKind.Absolute)
            };

            if (themeDictionary is not null)
            {
                var index = dictionaries.IndexOf(themeDictionary);
                dictionaries[index] = newTheme;
            }
            else
            {
                dictionaries.Add(newTheme);
            }
        }

        ApplyAccent(accent);
        CurrentTheme = normalized;
    }

    public static void ApplyAccent(string accent)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        if (!Accents.TryGetValue(accent, out var palette))
            palette = Accents["Verde"];

        app.Resources["AccentBrush"] = Brush(palette.Accent);
        app.Resources["AccentStrongBrush"] = Brush(palette.Strong);
        app.Resources["AccentLightBrush"] = Brush(palette.Light);
        app.Resources["AccentSoftBrush"] = Brush(palette.Soft);
        app.Resources["AccentTextBrush"] = Brush(palette.Text);
        app.Resources["AccentColor"] = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(palette.Accent);
    }

    private static System.Windows.Media.SolidColorBrush Brush(string hex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
