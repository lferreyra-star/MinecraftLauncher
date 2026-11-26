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

    /// <summary>Perfil global de movimiento seleccionado por el usuario.</summary>
    public static string MotionLevel { get; set; } = "Cinematográfico";

    public static IReadOnlyList<string> MotionLevels { get; } =
        ["Desactivado", "Sutil", "Equilibrado", "Cinematográfico"];

    private static double MotionScale => MotionLevel switch
    {
        "Sutil" => 0.45,
        "Equilibrado" => 0.75,
        "Cinematográfico" => 1.2,
        _ => 0
    };

    public static bool MotionOff => ReduceMotion || MotionLevel == "Desactivado" || PerformanceMode;

    public static Duration PageEnterDuration =>
        TimeSpan.FromMilliseconds(Math.Clamp((int)(380 * MotionScale), 160, 480));

    public static Duration PageExitDuration =>
        TimeSpan.FromMilliseconds(Math.Clamp((int)(190 * MotionScale), 90, 240));

    private static double PageSlide => 56 * MotionScale;

    private static double ScaleShift => 0.03 * MotionScale;

    /// <summary>True cuando se pueden reproducir efectos ambientales.</summary>
    public static bool AmbientMotionEnabled => AmbientEffectsEnabled && !ReduceMotion && !PerformanceMode;

    /// <summary>Partículas activas según intensidad y modo rendimiento.</summary>
    public static bool ParticlesActive => AmbientMotionEnabled && ParticleEffectsEnabled;

    /// <summary>Número seguro de partículas para el equipo actual.</summary>
    public static int ParticleCount => ParticlesActive ? (int)(30 * AmbientIntensity) : 0;

    /// <summary>Opacidad de los orbes ambientales.</summary>
    public static double AmbientOpacity => AmbientMotionEnabled ? 0.25 + (AmbientIntensity * 0.55) : 0;

    /// <summary>Si se aplica la preferencia de movimiento reducido de Windows.</summary>
    public static bool RespectSystemMotion { get; set; }

    /// <summary>True cuando el usuario o el sistema piden reducir las animaciones.</summary>
    public static bool ReduceMotion
    {
        get
        {
            if (!AnimationsEnabled)
                return true;
            if (!RespectSystemMotion)
                return false;

            try { return !SystemParameters.ClientAreaAnimation; }
            catch { return false; }
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

        if (MotionOff)
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

        if (MotionOff || !NavTransitionsEnabled)
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

    /// <summary>Transición de entrada completa: fade, dirección, profundidad y escala.</summary>
    public static void AnimateTransitionEnter(FrameworkElement element, double direction, bool isInitial = false)
    {
        if (element is null)
            return;

        var (scale, translate) = EnsurePageTransforms(element);
        CancelTransforms(element, scale, translate);

        if (MotionOff)
        {
            element.Opacity = 1;
            scale.ScaleX = scale.ScaleY = 1;
            translate.X = translate.Y = 0;
            return;
        }

        var duration = PageEnterDuration;
        var distance = PageSlide * direction * (isInitial ? 0.45 : 1);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = isInitial ? 0.55 : 0,
            To = 1,
            Duration = duration,
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = distance,
            To = 0,
            Duration = duration,
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 10 * MotionScale,
            To = 0,
            Duration = duration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            From = 1 - ScaleShift,
            To = 1,
            Duration = duration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
        {
            From = 1 - ScaleShift,
            To = 1,
            Duration = duration,
            EasingFunction = ease
        });
    }

    /// <summary>Salida corta del contenido anterior para dar continuidad al cambio.</summary>
    public static void AnimateTransitionExit(FrameworkElement element, double direction, Action? completed = null)
    {
        if (element is null)
        {
            completed?.Invoke();
            return;
        }

        var (scale, translate) = EnsurePageTransforms(element);
        CancelTransforms(element, scale, translate);

        if (MotionOff || !NavTransitionsEnabled)
        {
            element.Opacity = 0;
            completed?.Invoke();
            return;
        }

        var duration = PageExitDuration;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = element.Opacity,
            To = 0,
            Duration = duration,
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            To = -PageSlide * 1.25 * direction,
            Duration = duration,
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
        {
            To = -8 * MotionScale,
            Duration = duration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            To = 1 - (ScaleShift * 1.4),
            Duration = duration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
        {
            To = 1 - (ScaleShift * 0.6),
            Duration = duration,
            EasingFunction = ease
        });

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration.TimeSpan };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            completed?.Invoke();
        };
        timer.Start();
    }

    /// <summary>Entrada ligera del encabezado de la sección.</summary>
    public static void AnimateHeader(FrameworkElement element, double direction)
    {
        if (element is null)
            return;

        element.BeginAnimation(UIElement.OpacityProperty, null);
        if (MotionOff)
        {
            element.Opacity = 1;
            return;
        }

        if (element.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }
        translate.BeginAnimation(TranslateTransform.XProperty, null);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = PageEnterDuration,
            EasingFunction = ease
        });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = 18 * direction * MotionScale,
            To = 0,
            Duration = PageEnterDuration,
            EasingFunction = ease
        });
    }

    /// <summary>Transición suave del fondo de la sección.</summary>
    public static void AnimateBackground(FrameworkElement element, double targetOpacity, double direction)
    {
        if (element is null)
            return;

        if (element.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            element.RenderTransform = scale;
        }
        element.BeginAnimation(UIElement.OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (MotionOff)
        {
            element.Opacity = targetOpacity;
            scale.ScaleX = scale.ScaleY = 1;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fadeDuration = TimeSpan.FromMilliseconds(Math.Clamp((int)(520 * MotionScale), 260, 620));
        var zoomDuration = TimeSpan.FromMilliseconds(Math.Clamp((int)(620 * MotionScale), 300, 720));
        var zoomFrom = 1.035 + (0.012 * Math.Abs(direction));

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = targetOpacity * 0.35,
            To = targetOpacity,
            Duration = fadeDuration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
        {
            From = zoomFrom,
            To = 1,
            Duration = zoomDuration,
            EasingFunction = ease
        });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
        {
            From = zoomFrom,
            To = 1,
            Duration = zoomDuration,
            EasingFunction = ease
        });
    }

    /// <summary>Barrido luminoso que acompaña el cambio de sección.</summary>
    public static void AnimateSweep(FrameworkElement element, double from, double to)
    {
        if (element is null)
            return;

        if (element.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            element.RenderTransform = translate;
        }

        element.BeginAnimation(UIElement.OpacityProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);

        if (MotionOff || !NavTransitionsEnabled)
        {
            element.Opacity = 0;
            translate.X = to;
            return;
        }

        translate.X = from;
        var opacity = new DoubleAnimationUsingKeyFrames { Duration = PageEnterDuration };
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.45, KeyTime.FromPercent(0.35)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        element.BeginAnimation(UIElement.OpacityProperty, opacity);

        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = PageEnterDuration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsurePageTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup group)
        {
            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (scale is null)
            {
                scale = new ScaleTransform(1, 1);
                group.Children.Insert(0, scale);
            }
            if (translate is null)
            {
                translate = new TranslateTransform();
                group.Children.Add(translate);
            }
            return (scale, translate);
        }

        var newScale = new ScaleTransform(1, 1);
        var newTranslate = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { newScale, newTranslate } };
        return (newScale, newTranslate);
    }

    private static void CancelTransforms(FrameworkElement element, ScaleTransform scale, TranslateTransform translate)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.XProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);
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
        if (root is null || MotionOff)
            return;

        var index = 0;
        foreach (var child in EnumerateChildren(root))
        {
            if (child is not FrameworkElement element)
                continue;

            AnimateEntrance(element, 22 * MotionScale, Normal, Math.Min(index * step, maxDelay));
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

        if (MotionOff)
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
        if (element is null || MotionOff)
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
