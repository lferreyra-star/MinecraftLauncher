import io

p = 'src/XDLauncher/MainWindow.xaml'
s = io.open(p, encoding='utf-8-sig').read()

old = '''                <Grid x:Name="PageStage" Grid.Row="2" Margin="24,24,24,14">
                    <ContentControl x:Name="PreviousPageHost" IsHitTestVisible="False" Opacity="0" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                    <ContentControl x:Name="PageHost" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                </Grid>'''

new = '''                <Grid x:Name="PageStage" Grid.Row="2" Margin="24,24,24,14">
                    <ContentControl x:Name="PageHost" Panel.ZIndex="1" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                    <ContentControl x:Name="PreviousPageHost" Panel.ZIndex="2" IsHitTestVisible="False" Opacity="0" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                    <Border x:Name="TransitionSweep" Panel.ZIndex="3" IsHitTestVisible="False" Opacity="0"
                            Width="260" HorizontalAlignment="Left" VerticalAlignment="Stretch" Margin="-260,0,0,0">
                        <Border.RenderTransform><TranslateTransform X="0" Y="0" /></Border.RenderTransform>
                        <Border.Background>
                            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                <GradientStop Color="#00000000" Offset="0" />
                                <GradientStop Color="{DynamicResource AccentColor}" Offset="0.5" />
                                <GradientStop Color="#00000000" Offset="1" />
                            </LinearGradientBrush>
                        </Border.Background>
                    </Border>
                </Grid>'''

assert old in s
s = s.replace(old, new, 1)
io.open(p, 'w', encoding='utf-8').write(s)
