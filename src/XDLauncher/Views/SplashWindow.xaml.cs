using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.Views;

/// <summary>
/// Pantalla de presentación animada del launcher. Muestra el progreso real de
/// inicialización (carpetas, tema, servidor y sesión) antes de pasar al acceso
/// o directamente al dashboard.
/// </summary>
public partial class SplashWindow : Window
{
    private const double ProgressWidth = 320;

    private readonly Random _random = new();
    private readonly List<(Shape Shape, double Speed)> _particles = [];

    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{AppInfo.Version}";
        Loaded += (_, _) =>
        {
            StartParticles();
            Dispatcher.BeginInvoke(new Action(ApplyMotionPreference), DispatcherPriority.Background);
        };
    }

    /// <summary>Actualiza el mensaje y el progreso mostrado durante el arranque.</summary>
    public void Report(string message, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            var clamped = Math.Clamp(progress, 0, 1);
            PercentText.Text = $"{clamped * 100:0} %";
            StageText.Text = clamped switch
            {
                < 0.3 => "Preparando",
                < 0.6 => "Conectando",
                < 0.9 => "Restaurando",
                < 1 => "Cargando",
                _ => "Listo"
            };

            var widthAnimation = new DoubleAnimation
            {
                To = clamped * ProgressWidth,
                Duration = TimeSpan.FromMilliseconds(AnimationHelper.ReduceMotion ? 0 : 380),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);

            if (!AnimationHelper.ReduceMotion)
            {
                ProgressTrack.BeginAnimation(FrameworkElement.OpacityProperty,
                    new DoubleAnimation(1, 0.78, TimeSpan.FromMilliseconds(220)) { AutoReverse = true });
            }
        });
    }

    private void StartParticles()
    {
        if (!AnimationHelper.AmbientMotionEnabled)
            return;

        var accent = Application.Current?.TryFindResource("AccentColor") is Color color
            ? color
            : Color.FromRgb(0x5C, 0xE0, 0x7A);

        var width = Math.Max(ActualWidth, 900);
        var height = Math.Max(ActualHeight, 600);

        for (var i = 0; i < AnimationHelper.ParticleCount; i++)
        {
            var size = _random.NextDouble() * 2.4 + 0.8;
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)(28 + _random.Next(54)), accent.R, accent.G, accent.B)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(dot, _random.NextDouble() * width);
            Canvas.SetTop(dot, _random.NextDouble() * height);
            ParticleLayer.Children.Add(dot);

            var speed = _random.NextDouble() * 0.22 + 0.07;
            _particles.Add((dot, speed));

            var animation = new DoubleAnimation
            {
                From = 0,
                To = height + 20,
                Duration = TimeSpan.FromSeconds(14 / speed),
                RepeatBehavior = RepeatBehavior.Forever
            };
            dot.BeginAnimation(Canvas.TopProperty, animation);
        }
    }

    /// <summary>Detiene los efectos ambientales si el usuario o Windows piden menos movimiento.</summary>
    private void ApplyMotionPreference()
    {
        if (!AnimationHelper.AmbientMotionEnabled)
        {
            AmbientGlowA.Visibility = Visibility.Collapsed;
            AmbientGlowB.Visibility = Visibility.Collapsed;
            GlowATranslate.BeginAnimation(TranslateTransform.XProperty, null);
            GlowATranslate.BeginAnimation(TranslateTransform.YProperty, null);
            GlowBTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            GlowBTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        }

        if (AnimationHelper.ReduceMotion)
        {
            OrbitRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
        }
    }
}
