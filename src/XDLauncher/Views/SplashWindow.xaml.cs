using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace XdLauncher.Views;

/// <summary>
/// Pantalla de presentación animada del launcher. Muestra el progreso real de
/// inicialización (carpetas, tema, servidor y sesión) antes de pasar al acceso
/// o directamente al dashboard.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly Random _random = new();
    private readonly List<(Shape Shape, double Speed, double Drift)> _particles = [];

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => StartParticles();
    }

    /// <summary>Actualiza el mensaje y el progreso mostrado durante el arranque.</summary>
    public void Report(string message, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            var clamped = Math.Clamp(progress, 0, 1);
            var target = clamped * 260;
            var animation = new DoubleAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, animation);
            ProgressTrack.BeginAnimation(FrameworkElement.OpacityProperty,
                new DoubleAnimation(1, 0.75, TimeSpan.FromMilliseconds(240)) { AutoReverse = true });
        });
    }

    private void StartParticles()
    {
        var width = Math.Max(ActualWidth, 900);
        var height = Math.Max(ActualHeight, 600);

        for (var i = 0; i < 26; i++)
        {
            var size = _random.NextDouble() * 2.6 + 0.8;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(40 + _random.Next(60)), 92, 224, 122)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(dot, _random.NextDouble() * width);
            Canvas.SetTop(dot, _random.NextDouble() * height);
            ParticleLayer.Children.Add(dot);

            var speed = _random.NextDouble() * 0.25 + 0.08;
            var drift = (_random.NextDouble() - 0.5) * 0.18;
            _particles.Add((dot, speed, drift));

            var duration = TimeSpan.FromSeconds(14 / speed);
            var animation = new DoubleAnimation
            {
                From = 0,
                To = height + 20,
                Duration = duration,
                RepeatBehavior = RepeatBehavior.Forever
            };
            dot.BeginAnimation(Canvas.TopProperty, animation);
        }
    }
}
