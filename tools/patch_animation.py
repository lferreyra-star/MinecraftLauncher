import io

p = 'src/XDLauncher/Core/AnimationHelper.cs'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '    /// <summary>Modo visual de alto rendimiento: desactiva efectos costosos.</summary>\n    public static bool PerformanceMode { get; set; }\n',
    '    /// <summary>Modo visual de alto rendimiento: desactiva efectos costosos.</summary>\n    public static bool PerformanceMode { get; set; }\n\n'
    '    /// <summary>Perfil global de movimiento seleccionado por el usuario.</summary>\n'
    '    public static string MotionLevel { get; set; } = "Cinematográfico";\n\n'
    '    public static IReadOnlyList<string> MotionLevels { get; } =\n'
    '        ["Desactivado", "Sutil", "Equilibrado", "Cinematográfico"];\n\n'
    '    private static double MotionScale => MotionLevel switch\n'
    '    {\n'
    '        "Sutil" => 0.5,\n'
    '        "Equilibrado" => 0.75,\n'
    '        "Cinematográfico" => 1,\n'
    '        _ => 0\n'
    '    };\n\n'
    '    public static bool MotionOff => ReduceMotion || MotionLevel == "Desactivado" || PerformanceMode;\n\n'
    '    public static Duration PageEnterDuration =>\n'
    '        TimeSpan.FromMilliseconds(Math.Clamp((int)(360 * MotionScale), 180, 420));\n\n'
    '    public static Duration PageExitDuration =>\n'
    '        TimeSpan.FromMilliseconds(Math.Clamp((int)(170 * MotionScale), 90, 210));\n\n'
    '    private static double PageSlide => 42 * MotionScale;\n\n'
    '    private static double ScaleShift => 0.018 * MotionScale;\n'
)

s = s.replace(
    '        if (ReduceMotion)\n        {\n            element.Opacity = 1;\n            return;\n        }\n\n        if (element.RenderTransform is not TranslateTransform translate)',
    '        if (MotionOff)\n        {\n            element.Opacity = 1;\n            return;\n        }\n\n        if (element.RenderTransform is not TranslateTransform translate)',
    1,
)

s = s.replace(
    '        if (ReduceMotion || !NavTransitionsEnabled)\n        {\n            element.Opacity = 1;\n            if (translate is not null) translate.X = 0;\n            return;\n        }',
    '        if (MotionOff || !NavTransitionsEnabled)\n        {\n            element.Opacity = 1;\n            if (translate is not null) translate.X = 0;\n            return;\n        }',
)

insert_at = s.index('    private static TranslateTransform? FindTranslate(FrameworkElement element)')
new_methods = '''    /// <summary>Transición de entrada completa: fade, dirección, profundidad y escala.</summary>
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
            To = -PageSlide * 0.55 * direction,
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
            To = 1 - (ScaleShift * 0.6),
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

'''
s = s[:insert_at] + new_methods + s[insert_at:]

s = s.replace('        if (root is null || ReduceMotion)\n            return;', '        if (root is null || MotionOff)\n            return;')
s = s.replace('            AnimateEntrance(element, 22, Normal, Math.Min(index * step, maxDelay));',
              '            AnimateEntrance(element, 22 * MotionScale, Normal, Math.Min(index * step, maxDelay));')

s = s.replace(
    '        if (ReduceMotion)\n        {\n            completed?.Invoke();\n            return;\n        }\n',
    '        if (MotionOff)\n        {\n            completed?.Invoke();\n            return;\n        }\n',
    1,
)
s = s.replace(
    '        if (element is null || ReduceMotion)\n            return;\n',
    '        if (element is null || MotionOff)\n            return;\n',
    1,
)

io.open(p, 'w', encoding='utf-8').write(s)
