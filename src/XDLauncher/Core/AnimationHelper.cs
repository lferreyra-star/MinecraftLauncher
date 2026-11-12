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

    /// <summary>True cuando el sistema pide reducir las animaciones.</summary>
    public static bool ReduceMotion
    {
        get
        {
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
