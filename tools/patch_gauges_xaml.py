import io

p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '             xmlns:views="clr-namespace:XdLauncher.Views"\n',
    '             xmlns:views="clr-namespace:XdLauncher.Views"\n             xmlns:local="clr-namespace:XdLauncher.Core"\n',
    1,
)

s = s.replace(
    '    <UserControl.Resources>\n',
    '    <UserControl.Resources>\n        <local:PercentToGaugeOffsetConverter x:Key="GaugeOffset" />\n',
    1,
)

anchor = '''                    <TextBlock Text="RENDIMIENTO" Style="{StaticResource SectionTitle}" />
'''
gauges = '''                    <TextBlock Text="RENDIMIENTO" Style="{StaticResource SectionTitle}" />
                    <Border Style="{StaticResource InnerCard}" Padding="20" Margin="0,14,0,0">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="*" />
                            </Grid.ColumnDefinitions>
                            <StackPanel HorizontalAlignment="Center">
                                <Grid Width="122" Height="122">
                                    <Ellipse Width="112" Height="112" Stroke="{DynamicResource BorderSoftBrush}" StrokeThickness="9"
                                             HorizontalAlignment="Center" VerticalAlignment="Center" />
                                    <Ellipse Width="112" Height="112" Stroke="{DynamicResource AccentBrush}" StrokeThickness="9"
                                             StrokeDashArray="349" StrokeDashCap="Round"
                                             StrokeDashOffset="{Binding State.SystemInfo.RamUsagePercent, Converter={StaticResource GaugeOffset}}"
                                             HorizontalAlignment="Center" VerticalAlignment="Center" RenderTransformOrigin="0.5,0.5">
                                        <Ellipse.RenderTransform><RotateTransform Angle="-90" /></Ellipse.RenderTransform>
                                    </Ellipse>
                                    <TextBlock Text="{Binding State.SystemInfo.RamUsageText}" FontFamily="{StaticResource DisplayFont}"
                                               FontSize="20" FontWeight="SemiBold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Grid>
                                <TextBlock Text="MEMORIA DEL SISTEMA" Style="{StaticResource Kicker}" HorizontalAlignment="Center" Margin="0,8,0,0" />
                            </StackPanel>
                            <StackPanel Grid.Column="1" HorizontalAlignment="Center">
                                <Grid Width="122" Height="122">
                                    <Ellipse Width="112" Height="112" Stroke="{DynamicResource BorderSoftBrush}" StrokeThickness="9"
                                             HorizontalAlignment="Center" VerticalAlignment="Center" />
                                    <Ellipse Width="112" Height="112" Stroke="{DynamicResource AccentLightBrush}" StrokeThickness="9"
                                             StrokeDashArray="349" StrokeDashCap="Round"
                                             StrokeDashOffset="{Binding State.LauncherMemoryPercent, Converter={StaticResource GaugeOffset}}"
                                             HorizontalAlignment="Center" VerticalAlignment="Center" RenderTransformOrigin="0.5,0.5">
                                        <Ellipse.RenderTransform><RotateTransform Angle="-90" /></Ellipse.RenderTransform>
                                    </Ellipse>
                                    <TextBlock Text="{Binding State.LauncherMemoryText}" FontFamily="{StaticResource DisplayFont}"
                                               FontSize="20" FontWeight="SemiBold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Grid>
                                <TextBlock Text="XD LAUNCHER" Style="{StaticResource Kicker}" HorizontalAlignment="Center" Margin="0,8,0,0" />
                            </StackPanel>
                        </Grid>
                    </Border>
'''
assert anchor in s
s = s.replace(anchor, gauges, 1)
io.open(p, 'w', encoding='utf-8').write(s)
