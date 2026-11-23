using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XdLauncher.Core;

/// <summary>
/// Utilidades de animación del shell. Todas usan Opacity y RenderTransform,
/// cancelan la animación anterior antes de empezar la nueva y se desactivan
/// si Windows pide reducir el movimiento.
/// </summary>
public static class AnimationHelper
{
    private static readonly Duration Fast = TimeSpan.FromMilliseconds(170);
    private static readonly Duration Normal = TimeSpan.FromMilliseconds(250);
    private static readonly Duration Slow = TimeSpan.FromMilliseconds(400);

    /// <summary>Preferencia del usuario para las animaciones de la interfaz.</summary>
    public static bool AnimationsEnabled { get; set; } = true;

    /// <summary>Preferencia del usuario para los fondos ambientales animados.</summary>
    public static bool AmbientEffectsEnabled { get; set; } = true;

    /// <summary>Intensidad de la iluminación y las partículas ambientales (0..1).</summary>
    public static double AmbientIntensity { get; set; } = 0.75;

    /// <summary>Nivel de transparencia de paneles y cristales (0..1).</summary>
    public static double TransparencyLevel { get; set; } = 0.85;

    /// <summary>Transiciones laterales entre secciones.</summary>
    public static bool NavTransitionsEnabled { get; set; } = true;

    /// <summary>Partículas ambientales.</summary>
    public static bool ParticleEffectsEnabled { get; set; } = true;

    /// <summary>Iluminación y elevación al pasar el cursor.</summary>
    public static bool HoverEffectsEnabled { get; set; } = true;

    /// <summary>Parallax suave del fondo.</summary>
    public static bool ParallaxEnabled { get; set; } = true;

    /// <summary>Aparición escalonada de tarjetas.</summary>
    public static bool CardAnimationsEnabled { get; set; } = true;

    /// <summary>Animaciones de carga y skeletons.</summary>
    public static bool LoadingAnimationsEnabled { get; set; } = true;

    /// <summary>Modo visual de alto rendimiento: desactiva efectos costosos.</summary>
    public static bool PerformanceMode { get; set; }

    /// <summary>True cuando se pueden reproducir efectos ambientales.</summary>
    public static bool AmbientMotionEnabled => AmbientEffectsEnabled && !ReduceMotion && !PerformanceMode;

    /// <summary>Partículas activas según intensidad y modo rendimiento.</summary>
    public static bool ParticlesActive => AmbientMotionEnabled && ParticleEffectsEnabled;

    /// <summary>Número seguro de partículas para el equipo actual.</summary>
    public static int ParticleCount => ParticlesActive ? (int)(30 * AmbientIntensity) : 0;

    /// <summary>Opacidad de los orbes ambientales.</summary>
    public static double AmbientOpacity => AmbientMotionEnabled ? 0.25 + (AmbientIntensity * 0.55) : 0;

    /// <summary>True cuando el usuario o el sistema piden reducir las animaciones.</summary>
    public static bool ReduceMotion
    {
        get
        {
            if (!AnimationsEnabled)
                return true;

            try
            {
                return !SystemParameters.ClientAreaAnimation;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Entrada suave: opacidad y desplazamiento vertical.</summary>
    public static void AnimateEntrance(FrameworkElement element, double offset = 18,
        Duration? duration = null, int delayMilliseconds = 0)
    {
        if (element is null)
            return;

        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.BeginAnimation(UIElement.RenderTransformProperty, null);

        if (ReduceMotion)
        {
            element.Opacity = 1;
            return;
        }

        if (element.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration ?? Normal,
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation
        {
            From = offset,
            To = 0,
            Duration = duration ?? Normal,
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    /// <summary>Transición horizontal de página con dirección (-1 atrás, 1 adelante).</summary>
    public static void AnimatePageTransition(FrameworkElement element, double direction)
    {
        if (element is null)
            return;

        element.BeginAnimation(UIElement.OpacityProperty, null);
        var translate = FindTranslate(element);
        translate?.BeginAnimation(TranslateTransform.XProperty, null);

        if (ReduceMotion || !NavTransitionsEnabled)
        {
            element.Opacity = 1;
            if (translate is not null) translate.X = 0;
            return;
        }

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = Normal,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation
        {
            From = 34 * direction,
            To = 0,
            Duration = Slow,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate?.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    private static TranslateTransform? FindTranslate(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup group)
            return group.Children.OfType<TranslateTransform>().FirstOrDefault();
        return element.RenderTransform as TranslateTransform;
    }

    /// <summary>Entrada escalonada de los hijos de un contenedor.</summary>
    public static void AnimateStagger(DependencyObject root, int step = 55, int maxDelay = 420)
    {
        if (root is null || ReduceMotion)
            return;

        var index = 0;
        foreach (var child in EnumerateChildren(root))
        {
            if (child is not FrameworkElement element)
                continue;

            AnimateEntrance(element, 22, Normal, Math.Min(index * step, maxDelay));
            index++;
        }
    }

    /// <summary>Salida suave antes de quitar el elemento del árbol visual.</summary>
    public static void AnimateExit(FrameworkElement element, Action? completed = null, double offset = -14)
    {
        if (element is null)
        {
            completed?.Invoke();
            return;
        }

        if (ReduceMotion)
        {
            completed?.Invoke();
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (element.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }

        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = Fast,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var slide = new DoubleAnimation
        {
            To = offset,
            Duration = Fast,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = Fast.TimeSpan };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            completed?.Invoke();
        };
        timer.Start();
    }

    /// <summary>Escala un elemento (hover de botones y tarjetas).</summary>
    public static void AnimateScale(FrameworkElement element, double scale, Duration? duration = null)
    {
        if (element is null || ReduceMotion)
            return;

        if (element.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform(1, 1);
            element.RenderTransform = transform;
        }

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        var animation = new DoubleAnimation
        {
            To = scale,
            Duration = duration ?? Fast,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    /// <summary>Devuelve el elemento a su escala natural.</summary>
    public static void ResetScale(FrameworkElement element, Duration? duration = null)
        => AnimateScale(element, 1, duration);

    /// <summary>Recorre los hijos visuales directos de un elemento.</summary>
    public static IEnumerable<DependencyObject> EnumerateChildren(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
            yield return VisualTreeHelper.GetChild(parent, i);
    }
}
