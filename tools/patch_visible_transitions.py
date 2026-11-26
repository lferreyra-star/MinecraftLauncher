import io

p = 'src/XDLauncher/Core/AnimationHelper.cs'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace('''        "Sutil" => 0.5,
        "Equilibrado" => 0.75,
        "Cinematográfico" => 1,''','''        "Sutil" => 0.45,
        "Equilibrado" => 0.75,
        "Cinematográfico" => 1.2,''',1)
s = s.replace('TimeSpan.FromMilliseconds(Math.Clamp((int)(360 * MotionScale), 180, 420))','TimeSpan.FromMilliseconds(Math.Clamp((int)(380 * MotionScale), 160, 480))',1)
s = s.replace('TimeSpan.FromMilliseconds(Math.Clamp((int)(170 * MotionScale), 90, 210))','TimeSpan.FromMilliseconds(Math.Clamp((int)(190 * MotionScale), 90, 240))',1)
s = s.replace('private static double PageSlide => 42 * MotionScale;','private static double PageSlide => 56 * MotionScale;',1)
s = s.replace('private static double ScaleShift => 0.018 * MotionScale;','private static double ScaleShift => 0.03 * MotionScale;',1)

s = s.replace('''    private static (ScaleTransform Scale, TranslateTransform Translate) EnsurePageTransforms(FrameworkElement element)''','''    /// <summary>Barrido luminoso que acompaña el cambio de sección.</summary>
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
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.26, KeyTime.FromPercent(0.35)));
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

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsurePageTransforms(FrameworkElement element)''',1)

io.open(p, 'w', encoding='utf-8').write(s)
