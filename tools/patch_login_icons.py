import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Views/LoginWindow.xaml'
s = read(p)

replacements = [
    ('''                                <TextBlock FontFamily="{StaticResource IconFont}" FontSize="15" Text="&#xE714;"
                                           Foreground="{DynamicResource TextMutedBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />''',
     '''                                <Path Width="16" Height="16" Stretch="Uniform" Fill="{DynamicResource TextMutedBrush}"
                                      Data="M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z"
                                      HorizontalAlignment="Center" VerticalAlignment="Center" />'''),
    ('''                                <TextBlock FontFamily="{StaticResource IconFont}" FontSize="15" Text="&#xE13D;"
                                           Foreground="{DynamicResource TextMutedBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />''',
     '''                                <Path Width="16" Height="16" Stretch="Uniform" Fill="{DynamicResource TextMutedBrush}"
                                      Data="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"
                                      HorizontalAlignment="Center" VerticalAlignment="Center" />'''),
    ('''                                <TextBlock FontFamily="{StaticResource IconFont}" FontSize="15" Text="&#xE890;"
                                           Foreground="{DynamicResource TextMutedBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />''',
     '''                                <Path Width="16" Height="16" Stretch="Uniform" Fill="{DynamicResource TextMutedBrush}"
                                      Data="M12 5C7 5 2.73 8.11 1 12c1.73 3.89 6 7 11 7s9.27-3.11 11-7c-1.73-3.89-6-7-11-7zm0 11c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8a3 3 0 100 6 3 3 0 000-6z"
                                      HorizontalAlignment="Center" VerticalAlignment="Center" />'''),
    ('''                                <TextBlock FontFamily="{StaticResource IconFont}" FontSize="15" Text="&#xE7B3;"
                                           Foreground="{DynamicResource TextMutedBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />''',
     '''                                <Path Width="16" Height="16" Stretch="Uniform" Fill="{DynamicResource TextMutedBrush}"
                                      Data="M12 17c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm6-9h-1V6c0-2.76-2.24-5-5-5S7 3.24 7 6v2H6c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zM8.9 6c0-1.71 1.39-3.1 3.1-3.1s3.1 1.39 3.1 3.1v2H8.9V6z"
                                      HorizontalAlignment="Center" VerticalAlignment="Center" />'''),
    ('''                                    Style="{StaticResource IconButton}" Width="34" Height="34" Margin="0,0,2,0"
                                    Content="{Binding PasswordVisibilityGlyph}" FontFamily="{StaticResource IconFont}"
                                    Click="TogglePassword_Click"''',
     '''                                    Style="{StaticResource IconButton}" Width="34" Height="34" Margin="0,0,2,0"
                                    Click="TogglePassword_Click"'''),
]

for old, new in replacements:
    assert old in s, old[:80]
    s = s.replace(old, new, 1)

anchor = '''                                    Click="TogglePassword_Click" AutomationProperties.Name="Mostrar u ocultar contraseña"
                                    ToolTip="Mostrar u ocultar contraseña" />'''
new_button = '''                                    Click="TogglePassword_Click" AutomationProperties.Name="Mostrar u ocultar contraseña"
                                    ToolTip="Mostrar u ocultar contraseña">
                                <Grid Width="16" Height="16">
                                    <Path Stretch="Uniform" Stroke="{DynamicResource TextMutedBrush}" StrokeThickness="1.3"
                                          Fill="Transparent" Data="M1 8C3 4.5 5.5 3 8 3s5 1.5 7 5c-2 3.5-4.5 5-7 5S3 11.5 1 8z" />
                                    <Ellipse Width="4" Height="4" Fill="{DynamicResource TextMutedBrush}" />
                                    <Line X1="2" Y1="14" X2="14" Y2="2" Stroke="{DynamicResource TextMutedBrush}"
                                          StrokeThickness="1.4" Visibility="{Binding ShowPassword, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}" />
                                </Grid>
                            </Button>'''
assert anchor in s
s = s.replace(anchor, new_button, 1)
write(p, s)
