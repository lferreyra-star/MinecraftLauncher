import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

base = 'src/XDLauncher/'

p = base + 'Themes/LightPremium.xaml'
s = read(p)
for old, new in [
    ('#8AFDFEFF', '#8A08111C'),
    ('#C7F8FBFF', '#66070E17'),
    ('#00F8FBFF', '#08070E17'),
]:
    if old in s:
        s = s.replace(old, new)
write(p, s)

p = base + 'Themes/Shared.xaml'
s = read(p)
old = '<DropShadowEffect x:Key="AccentGlowEffect" Color="#5CE07A" BlurRadius="28" ShadowDepth="0" Opacity="0.45" />'
new = '<DropShadowEffect x:Key="AccentGlowEffect" Color="{DynamicResource AccentColor}" BlurRadius="28" ShadowDepth="0" Opacity="0.45" />'
assert old in s
s = s.replace(old, new)
s = s.replace('{StaticResource AccentGlowEffect}', '{DynamicResource AccentGlowEffect}')
old = '    <core:UpperTextConverter x:Key="UpperText" />\n'
new = old + '    <core:BoolToTextConverter x:Key="BoolToText" />\n'
assert old in s
s = s.replace(old, new)

old = '''    <Style TargetType="ProgressBar">
        <Setter Property="Height" Value="8" />
        <Setter Property="Foreground" Value="{DynamicResource AccentBrush}" />
        <Setter Property="Background" Value="{DynamicResource ProgressTrackBrush}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ProgressBar">
                    <Border Background="{TemplateBinding Background}" CornerRadius="4" ClipToBounds="True">
                        <Grid x:Name="PART_Track">
                            <Border x:Name="PART_Indicator" Background="{TemplateBinding Foreground}" CornerRadius="4"
                                    HorizontalAlignment="Left" />
                        </Grid>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
'''
new = '''    <Style TargetType="ProgressBar">
        <Setter Property="Height" Value="8" />
        <Setter Property="Foreground" Value="{DynamicResource AccentBrush}" />
        <Setter Property="Background" Value="{DynamicResource ProgressTrackBrush}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ProgressBar">
                    <Border Background="{TemplateBinding Background}" CornerRadius="4" ClipToBounds="True">
                        <Grid x:Name="PART_Track">
                            <Border x:Name="PART_Indicator" Background="{TemplateBinding Foreground}" CornerRadius="4"
                                    HorizontalAlignment="Left" />
                            <Border x:Name="IndeterminateBar" Width="90" Background="{TemplateBinding Foreground}"
                                    CornerRadius="4" HorizontalAlignment="Left" Opacity="0">
                                <Border.RenderTransform>
                                    <TranslateTransform x:Name="IndeterminateTransform" X="-90" />
                                </Border.RenderTransform>
                            </Border>
                        </Grid>
                        <VisualStateManager.VisualStateGroups>
                            <VisualStateGroup x:Name="CommonStates">
                                <VisualState x:Name="Determinate" />
                                <VisualState x:Name="Indeterminate">
                                    <Storyboard RepeatBehavior="Forever">
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="PART_Indicator"
                                                                       Storyboard.TargetProperty="Visibility">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="{x:Static Visibility.Collapsed}" />
                                        </ObjectAnimationUsingKeyFrames>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="IndeterminateBar"
                                                                       Storyboard.TargetProperty="Opacity">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="1" />
                                        </ObjectAnimationUsingKeyFrames>
                                        <DoubleAnimation Storyboard.TargetName="IndeterminateTransform"
                                                         Storyboard.TargetProperty="X" From="-90" To="900"
                                                         Duration="0:0:1.4" />
                                    </Storyboard>
                                </VisualState>
                            </VisualStateGroup>
                        </VisualStateManager.VisualStateGroups>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
'''
assert old in s
s = s.replace(old, new)
write(p, s)

for p in [base + 'MainWindow.xaml', base + 'Views/SettingsView.xaml']:
    s = read(p)
    s = s.replace('{StaticResource AccentGlowEffect}', '{DynamicResource AccentGlowEffect}')
    write(p, s)

p = base + 'Core/Converters.cs'
s = read(p)
marker = 'public sealed class BytesToTextConverter : IValueConverter\n'
new_class = '''public sealed class BoolToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var options = (parameter as string ?? "SÍ|NO").Split('|');
        var affirmative = options.Length > 0 ? options[0] : "SÍ";
        var negative = options.Length > 1 ? options[1] : "NO";
        return value is bool flag && flag ? affirmative : negative;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

'''
assert marker in s
s = s.replace(marker, new_class + marker, 1)
write(p, s)

p = base + 'Views/ModsCenterView.xaml'
s = read(p)
old = '<Button Content="{Binding IsEnabled, Converter={StaticResource UpperText}}" Style="{StaticResource TinyButton}"'
new = '<Button Content="{Binding IsEnabled, Converter={StaticResource BoolToText}, ConverterParameter=ACTIVAR|DESACTIVAR}" Style="{StaticResource TinyButton}"'
assert old in s
s = s.replace(old, new)
write(p, s)

p = base + 'MainWindow.xaml'
s = read(p)
start = s.index('                    <Border Background="{DynamicResource GlassStrongBrush}"')
end = s.index('                    </Border>', start) + len('                    </Border>')
new_toast = '''                    <Border Background="{DynamicResource GlassStrongBrush}" BorderBrush="{DynamicResource BorderBrush}"
                            BorderThickness="1" CornerRadius="14" Margin="0,8,0,0" Padding="12" Width="420"
                            Effect="{DynamicResource PanelShadow}">
                        <Border.Style>
                            <Style TargetType="Border">
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding IsError}" Value="True">
                                        <Setter Property="Background" Value="{DynamicResource ErrorSoftBrush}" />
                                        <Setter Property="BorderBrush" Value="{DynamicResource ErrorBrush}" />
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </Border.Style>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto" />
                                <ColumnDefinition Width="*" />
                                <ColumnDefinition Width="Auto" />
                            </Grid.ColumnDefinitions>
                            <Border x:Name="IconTile" Width="30" Height="30" CornerRadius="9"
                                    Background="{DynamicResource AccentSoftBrush}"
                                    BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                <TextBlock x:Name="IconText" Text="{Binding IconGlyph}" FontFamily="{StaticResource IconFont}" FontSize="12"
                                           Foreground="{DynamicResource AccentBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                            </Border>
                            <TextBlock Grid.Column="1" Text="{Binding Message}" Margin="10,0,10,0" VerticalAlignment="Center" />
                            <Button Grid.Column="2" Content="&#xE711;" Style="{StaticResource IconButton}" Width="26" Height="26"
                                    Command="{Binding DataContext.DismissToastCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}" />
                        </Grid>
                    </Border>'''
s = s[:start] + new_toast + s[end:]
write(p, s)

p = base + 'Views/VersionsView.xaml'
s = read(p)
s = s.replace('NIEVACIÓN', 'ACTUALIZACIÓN')
old = '''                            <Border Style="{StaticResource Badge}" Background="{DynamicResource ErrorSoftBrush}"
                                    BorderBrush="{DynamicResource ErrorBrush}" Grid.Column="1" Margin="10,0,0,0">
                                <TextBlock Text="{Binding UpdateStatus}" FontSize="10" FontWeight="Bold" Foreground="{DynamicResource ErrorBrush}" />
                            </Border>
'''
new = '''                            <Border Grid.Column="1" Margin="10,0,0,0">
                                <Border.Style>
                                    <Style TargetType="Border" BasedOn="{StaticResource Badge}">
                                        <Setter Property="Visibility" Value="Collapsed" />
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding UpdateStatus}" Value="Actualización disponible">
                                                <Setter Property="Visibility" Value="Visible" />
                                                <Setter Property="Background" Value="{DynamicResource ErrorSoftBrush}" />
                                                <Setter Property="BorderBrush" Value="{DynamicResource ErrorBrush}" />
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </Border.Style>
                                <TextBlock Text="{Binding UpdateStatus}" FontSize="10" FontWeight="Bold" Foreground="{DynamicResource ErrorBrush}" />
                            </Border>
'''
assert old in s
s = s.replace(old, new)
old = '''                        <Border x:Name="UpdateBadge" Padding="12,7" Margin="0,0,0,10">
                            <StackPanel Orientation="Horizontal">
'''
new = '''                        <Border x:Name="UpdateBadge" Padding="12,7" Margin="0,0,0,10">
                            <Border.Style>
                                <Style TargetType="Border">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                    <Setter Property="CornerRadius" Value="10" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding UpdateStatus}" Value="Actualización disponible">
                                            <Setter Property="Visibility" Value="Visible" />
                                            <Setter Property="Background" Value="{DynamicResource ErrorSoftBrush}" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Border.Style>
                            <StackPanel Orientation="Horizontal">
'''
assert old in s
s = s.replace(old, new)
write(p, s)

p = base + 'Views/HomeView.xaml'
s = read(p)
old = '<Button Style="{StaticResource IconButton}" Width="9" Height="9" Padding="0" Margin="2,0"'
new = '<Button Content="•" FontFamily="{StaticResource BodyFont}" FontSize="14" Foreground="{DynamicResource TextMutedBrush}" VerticalContentAlignment="Center" Style="{StaticResource IconButton}" Width="9" Height="9" Padding="0" Margin="2,0"'
assert old in s
s = s.replace(old, new)
old = '<ItemsControl ItemsSource="{Binding News}" Margin="0,14,0,0" MaxHeight="120">'
new = '<ItemsControl ItemsSource="{Binding News}" Margin="0,14,0,0">'
assert old in s
s = s.replace(old, new)
write(p, s)

p = base + 'Views/SettingsView.xaml'
s = read(p)
old = '<Button Content="{StaticResource IconGlyph}" Style="{StaticResource CardButton}" Padding="8" Margin="0,8,0,0"'
new = '<Button Content="&#xE13D;" FontFamily="{StaticResource IconFont}" FontSize="18" Foreground="{DynamicResource AccentBrush}" Style="{StaticResource CardButton}" Padding="8" Margin="0,8,0,0"'
assert old in s
s = s.replace(old, new)
write(p, s)
