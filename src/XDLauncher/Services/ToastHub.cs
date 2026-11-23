using System.Windows;
using System.Windows.Media;
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

/// <summary>Switches complete theme dictionaries and applies accent palettes at runtime.</summary>
public static class ThemeManager
{
    private static readonly Dictionary<string, string> Themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Eclipse Dark"] = "EclipseDark",
        ["Aurora"] = "Aurora",
        ["Nebula"] = "Nebula",
        ["Cyberpunk"] = "Cyberpunk",
        ["Emerald"] = "Emerald",
        ["Ocean"] = "Ocean",
        ["Crimson"] = "Crimson",
        ["Purple Galaxy"] = "PurpleGalaxy",
        ["Minecraft Classic"] = "MinecraftClassic",
        ["Light Premium"] = "LightPremium"
    };

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dark"] = "Eclipse Dark",
        ["Light"] = "Light Premium",
        ["Eclipse"] = "Eclipse Dark",
        ["Purple"] = "Purple Galaxy",
        ["Classic"] = "Minecraft Classic"
    };

    /// <summary>Curated accent catalog. Keep in sync with SettingsState.Accents.</summary>
    private static readonly Dictionary<string, string> Accents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Verde"] = "#5CE07A",
        ["Esmeralda"] = "#2DD4A7",
        ["Lima"] = "#B4E64B",
        ["Cian"] = "#45C8F5",
        ["Océano"] = "#4F8DFD",
        ["Violeta"] = "#A78BFA",
        ["Rosa"] = "#F472B6",
        ["Ámbar"] = "#F5B942",
        ["Coral"] = "#F87171",
        ["XD Emerald"] = "#5CE07A",
        ["Obsidian"] = "#64748B",
        ["Cyberpunk"] = "#F472B6",
        ["Aurora"] = "#34D399",
        ["Midnight"] = "#6366F1",
        ["Arctic"] = "#38BDF8",
        ["Neon Pulse"] = "#A855F7",
        ["Crimson"] = "#EF4444",
        ["Personalizado"] = "#5CE07A"
    };

    private static string? _appliedAccent;
    private static string? _appliedTheme;

    public static IReadOnlyList<string> ThemeNames => Themes.Keys.ToArray();

    public static string CurrentTheme { get; private set; } = "Eclipse Dark";

    public static bool IsLight(string theme)
        => NormalizeTheme(theme).Equals("Light Premium", StringComparison.OrdinalIgnoreCase);

    public static void SetCustomAccent(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;
        try
        {
            ColorConverter.ConvertFromString(hex);
            Accents["Personalizado"] = hex;
        }
        catch
        {
            // Mantén el último color válido.
        }
    }

    public static void ApplyTransparency(double level)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;
        var value = Math.Clamp(level, 0, 1);
        var weak = (byte)(18 + value * 34);
        var strong = (byte)(28 + value * 52);
        app.Resources["GlassBrush"] = Brush(Color.FromArgb(weak, 255, 255, 255));
        app.Resources["GlassStrongBrush"] = Brush(Color.FromArgb(strong, 255, 255, 255));
        app.Resources["SurfaceGlassBrush"] = Brush(Color.FromArgb(strong, 255, 255, 255));
    }

    private static string NormalizeTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
            return "Eclipse Dark";
        if (Aliases.TryGetValue(theme, out var alias))
            return alias;
        if (Themes.ContainsKey(theme))
            return Themes.First(x => string.Equals(x.Key, theme, StringComparison.OrdinalIgnoreCase)).Key;
        return "Eclipse Dark";
    }

    private static string ThemeFile(string theme)
        => Themes.TryGetValue(theme, out var file) ? file : "EclipseDark";

    public static void Apply(string theme, string accent)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        var normalized = NormalizeTheme(theme);

        // Only swap the theme dictionary when the theme really changes: replacing it forces
        // every DynamicResource in the interface to be resolved again.
        if (!string.Equals(CurrentTheme, normalized, StringComparison.Ordinal))
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var themeDictionary = dictionaries.FirstOrDefault(d =>
            {
                var source = d.Source?.OriginalString;
                return source is not null &&
                       source.Contains("/Themes/", StringComparison.OrdinalIgnoreCase) &&
                       !source.Contains("Shared.xaml", StringComparison.OrdinalIgnoreCase);
            });

            var assemblyName = typeof(ThemeManager).Assembly.GetName().Name;
            var newTheme = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/{assemblyName};component/Themes/{ThemeFile(normalized)}.xaml",
                    UriKind.Absolute)
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

        var accentChanged = !string.Equals(_appliedAccent, accent, StringComparison.OrdinalIgnoreCase);
        if (accentChanged || !string.Equals(_appliedTheme, normalized, StringComparison.Ordinal))
        {
            ApplyAccent(accent, normalized);
            _appliedAccent = accent;
            _appliedTheme = normalized;
        }

        CurrentTheme = normalized;
    }

    private static void ApplyAccent(string accent, string theme)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
            return;

        if (!Accents.TryGetValue(accent, out var hex))
            hex = Accents["Verde"];

        var baseColor = Parse(hex);
        var strong = Mix(baseColor, Colors.Black, 0.2);
        var light = Mix(baseColor, Colors.White, 0.45);
        var soft = IsLight(theme)
            ? Mix(baseColor, Colors.White, 0.84)
            : Mix(baseColor, Color.FromRgb(0x08, 0x0E, 0x15), 0.8);
        var text = Luminance(baseColor) > 0.6 ? Color.FromRgb(0x05, 0x12, 0x09) : Colors.White;

        app.Resources["AccentBrush"] = Brush(baseColor);
        app.Resources["AccentStrongBrush"] = Brush(strong);
        app.Resources["AccentLightBrush"] = Brush(light);
        app.Resources["AccentSoftBrush"] = Brush(soft);
        app.Resources["AccentTextBrush"] = Brush(text);
        app.Resources["AccentColor"] = baseColor;
        app.Resources["AccentGradientBrush"] = Gradient(light, baseColor, strong);
        app.Resources["AccentGradientSoftBrush"] = Gradient(WithAlpha(light, 0x66), WithAlpha(baseColor, 0x33), WithAlpha(strong, 0x14));

        var glow = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(WithAlpha(baseColor, 0x5A), 0),
                new GradientStop(WithAlpha(baseColor, 0x24), 0.55),
                new GradientStop(WithAlpha(baseColor, 0x00), 1)
            }
        };
        glow.Freeze();
        app.Resources["AccentGlowBrush"] = glow;

        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = baseColor,
            BlurRadius = 26,
            ShadowDepth = 0,
            Opacity = 0.4
        };
        glowEffect.Freeze();
        app.Resources["AccentGlowEffect"] = glowEffect;
    }

    private static Color Parse(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Gradient(params Color[] colors)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(colors[i], colors.Length == 1 ? 0 : i / (double)(colors.Length - 1)));
        brush.Freeze();
        return brush;
    }

    private static Color Mix(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * amount),
            (byte)(a.R + (b.R - a.R) * amount),
            (byte)(a.G + (b.G - a.G) * amount),
            (byte)(a.B + (b.B - a.B) * amount));
    }

    private static Color WithAlpha(Color color, byte alpha)
        => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double Luminance(Color color)
        => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
}
