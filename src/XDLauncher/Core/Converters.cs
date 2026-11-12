using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XdLauncher.Models;

namespace XdLauncher.Core;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is not null && (value is not string s || !string.IsNullOrWhiteSpace(s));
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = !string.IsNullOrWhiteSpace(value as string);
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is int count && count > 0;
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class UpperTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => (value as string)?.ToUpper(culture) ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class BytesToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes || bytes <= 0)
            return "—";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var order = (int)Math.Floor(Math.Log(bytes, 1024));
        order = Math.Clamp(order, 0, units.Length - 1);
        var number = bytes / Math.Pow(1024, order);
        return $"{number:0.#} {units[order]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => ImageLoader.Load(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public static class ImageLoader
{
    public static ImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                return LoadEmbeddedPath(path);

            if (!File.Exists(path))
                return null;

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                return null;

            var frame = decoder.Frames[0];
            var width = frame.PixelWidth;
            var height = frame.PixelHeight;
            if (width <= 0 || height <= 0)
                return null;

            const int maxWidth = 1600;
            if (width > maxWidth)
            {
                var scale = (double)maxWidth / width;
                var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                scaled.Freeze();
                return scaled;
            }

            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            Services.LogService.Warn($"No se pudo cargar la imagen '{path}': {ex.Message}");
            return null;
        }
    }

    public static BitmapImage? LoadEmbedded(Uri uri)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadEmbeddedPath(string path)
    {
        try
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
                return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Services.LogService.Warn($"No se pudo cargar el recurso '{path}': {ex.Message}");
            return null;
        }
    }
}

public sealed class StateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DownloadState state)
            return Brushes.Gray;

        return state switch
        {
            DownloadState.Downloading or DownloadState.Queued => Brushes.Gold,
            DownloadState.Completed or DownloadState.ReadyToInstall or DownloadState.Installed => Brushes.Green,
            DownloadState.Failed or DownloadState.Cancelled => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class DownloadStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DownloadState state || parameter is not string param)
            return Visibility.Collapsed;

        var show = param switch
        {
            "Downloading" => state == DownloadState.Downloading,
            "FailedOrCancelled" => state == DownloadState.Failed || state == DownloadState.Cancelled,
            "CompletedOrReady" => state == DownloadState.Completed || state == DownloadState.ReadyToInstall,
            "NotDownloading" => state != DownloadState.Downloading && state != DownloadState.Queued,
            _ => false
        };

        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
