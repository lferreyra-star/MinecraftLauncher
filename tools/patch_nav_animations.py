import io

p = 'src/XDLauncher/Themes/Shared.xaml'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '''                        <Border x:Name="Chrome" Background="Transparent" BorderBrush="Transparent"
                                BorderThickness="1" CornerRadius="12" Padding="{TemplateBinding Padding}">''',
    '''                        <Border x:Name="Chrome" Background="Transparent" BorderBrush="Transparent"
                                BorderThickness="1" CornerRadius="12" Padding="{TemplateBinding Padding}"
                                RenderTransformOrigin="0.5,0.5">
                            <Border.RenderTransform><ScaleTransform x:Name="ChromeScale" ScaleX="1" ScaleY="1" /></Border.RenderTransform>''',
    1,
)

s = s.replace(
    '''                                    <TextBlock x:Name="Glyph" Text="{Binding Glyph}" FontFamily="{StaticResource IconFont}"
                                               FontSize="15" HorizontalAlignment="Center" VerticalAlignment="Center"
                                               Foreground="{DynamicResource TextMutedBrush}" />''',
    '''                                    <TextBlock x:Name="Glyph" Text="{Binding Glyph}" FontFamily="{StaticResource IconFont}"
                                               FontSize="15" HorizontalAlignment="Center" VerticalAlignment="Center"
                                               Foreground="{DynamicResource TextMutedBrush}" RenderTransformOrigin="0.5,0.5">
                                        <TextBlock.RenderTransform><ScaleTransform x:Name="GlyphScale" ScaleX="1" ScaleY="1" /></TextBlock.RenderTransform>
                                    </TextBlock>''',
    1,
)

s = s.replace(
    '''                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">''',
    '''                    <Grid.Triggers>
                        <EventTrigger RoutedEvent="MouseEnter">
                            <BeginStoryboard>
                                <Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="GlyphScale" Storyboard.TargetProperty="ScaleX" To="1.14" Duration="0:0:0.16" AutoReverse="True">
                                        <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                    </DoubleAnimation>
                                    <DoubleAnimation Storyboard.TargetName="GlyphScale" Storyboard.TargetProperty="ScaleY" To="1.14" Duration="0:0:0.16" AutoReverse="True">
                                        <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                    </DoubleAnimation>
                                </Storyboard>
                            </BeginStoryboard>
                        </EventTrigger>
                    </Grid.Triggers>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">''',
    1,
)

s = s.replace(
    '''                            <Setter TargetName="Chevron" Property="Opacity" Value="0.65" />
                        </Trigger>''',
    '''                            <Setter TargetName="Chevron" Property="Opacity" Value="0.65" />
                            <Trigger.EnterActions>
                                <BeginStoryboard>
                                    <Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="ChromeScale" Storyboard.TargetProperty="ScaleX" From="0.97" To="1" Duration="0:0:0.24">
                                            <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                        </DoubleAnimation>
                                        <DoubleAnimation Storyboard.TargetName="ChromeScale" Storyboard.TargetProperty="ScaleY" From="0.97" To="1" Duration="0:0:0.24">
                                            <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                        </DoubleAnimation>
                                        <DoubleAnimation Storyboard.TargetName="GlyphScale" Storyboard.TargetProperty="ScaleX" From="0.7" To="1" Duration="0:0:0.3">
                                            <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                        </DoubleAnimation>
                                        <DoubleAnimation Storyboard.TargetName="GlyphScale" Storyboard.TargetProperty="ScaleY" From="0.7" To="1" Duration="0:0:0.3">
                                            <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                        </DoubleAnimation>
                                    </Storyboard>
                                </BeginStoryboard>
                            </Trigger.EnterActions>
                        </Trigger>''',
    1,
)

io.open(p, 'w', encoding='utf-8').write(s)
