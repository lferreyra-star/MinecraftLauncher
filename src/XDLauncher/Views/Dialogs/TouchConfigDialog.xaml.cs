using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Views.Dialogs;

public sealed class TouchConfigResult
{
    public required Dictionary<string, TouchPointSetting> Targets { get; init; }
    public required double JoystickRadius { get; init; }
}

/// <summary>
/// Calibrates the on-screen buttons of Minecraft using a real screenshot taken from the phone
/// with <c>adb exec-out screencap</c>. Positions are stored as normalized coordinates so they
/// survive resolution and orientation changes.
/// </summary>
public partial class TouchConfigDialog : Window
{
    private readonly List<TouchTargetInfo> _targets;
    private TouchTargetInfo? _selected;
    private bool _suppressRadius;

    public TouchConfigDialog(List<TouchTargetInfo> targets, (int Width, int Height, byte[] Data)? screenshot,
        double joystickRadius)
    {
        InitializeComponent();
        _targets = targets;

        _suppressRadius = true;
        RadiusSlider.Value = Math.Clamp(joystickRadius, 0.04, 0.30);
        _suppressRadius = false;
        RadiusText.Text = $"Radio del joystick: {(int)Math.Round(RadiusSlider.Value * 100)} % del ancho";

        TargetsList.ItemsSource = _targets;

        if (screenshot is { } shot && shot.Data.Length > 0)
        {
            try
            {
                var image = new BitmapImage();
                using var stream = new MemoryStream(shot.Data);
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                ScreenshotImage.Source = image;
                EmptyState.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogService.Warn("No se pudo mostrar la captura del teléfono: " + ex.Message);
            }
        }

        _selected = _targets.FirstOrDefault();
        Loaded += (_, _) => DrawMarkers();
        SizeChanged += (_, _) => DrawMarkers();
    }

    /// <summary>Shows the dialog and returns the calibrated positions, or null when cancelled.</summary>
    public static TouchConfigResult? Show(List<TouchTargetInfo> targets,
        (int Width, int Height, byte[] Data)? screenshot, double joystickRadius)
    {
        var dialog = new TouchConfigDialog(targets, screenshot, joystickRadius);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;

        if (dialog.ShowDialog() != true)
            return null;

        var map = new Dictionary<string, TouchPointSetting>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in dialog._targets)
            map[target.Id] = new TouchPointSetting { X = target.X, Y = target.Y };

        return new TouchConfigResult
        {
            Targets = map,
            JoystickRadius = dialog.RadiusSlider.Value
        };
    }

    private void Target_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string id)
            return;

        _selected = _targets.FirstOrDefault(t => t.Id == id);
        DrawMarkers();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selected is null || ScreenshotImage.Source is null)
            return;

        var position = e.GetPosition(MarkerCanvas);
        var width = MarkerCanvas.ActualWidth;
        var height = MarkerCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        _selected.X = Math.Clamp(position.X / width, 0, 1);
        _selected.Y = Math.Clamp(position.Y / height, 0, 1);
        TargetsList.Items.Refresh();
        DrawMarkers();
    }

    private void Radius_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressRadius)
            return;
        RadiusText.Text = $"Radio del joystick: {(int)Math.Round(e.NewValue * 100)} % del ancho";
    }

    private void DrawMarkers()
    {
        MarkerCanvas.Children.Clear();
        if (ScreenshotImage.Source is null)
            return;

        var width = MarkerCanvas.ActualWidth;
        var height = MarkerCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        foreach (var target in _targets)
        {
            if (target.X <= 0.001 || target.Y <= 0.001)
                continue;

            var isSelected = ReferenceEquals(target, _selected);
            var marker = new Ellipse
            {
                Width = isSelected ? 26 : 20,
                Height = isSelected ? 26 : 20,
                Fill = new SolidColorBrush(Color.FromArgb(isSelected ? (byte)230 : (byte)160, 0x5C, 0xE0, 0x7A)),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = isSelected ? 2.5 : 1.5,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(marker, target.X * width - marker.Width / 2);
            Canvas.SetTop(marker, target.Y * height - marker.Height / 2);
            MarkerCanvas.Children.Add(marker);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
