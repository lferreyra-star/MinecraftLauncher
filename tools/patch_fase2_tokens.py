import io
import re
from pathlib import Path

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

for p in Path('src/XDLauncher/Themes').glob('*.xaml'):
    if p.name == 'Shared.xaml':
        continue
    s = read(str(p))
    if 'x:Key="HeroForegroundBrush"' not in s:
        s, count = re.subn(r'(<LinearGradientBrush x:Key="HeroScrimBrush".*?</LinearGradientBrush>)',
                           r'\1\n    <SolidColorBrush x:Key="HeroForegroundBrush" Color="#FFFFFFFF" />',
                           s, count=1, flags=re.S)
        if count:
            write(str(p), s)

p = 'src/XDLauncher/Themes/Shared.xaml'
s = read(p)
s = s.replace('<FontFamily x:Key="DisplayFont">Segoe UI Variable Display, Segoe UI</FontFamily>',
              '<FontFamily x:Key="DisplayFont">Inter, Segoe UI Variable Display, Segoe UI</FontFamily>')
s = s.replace('<FontFamily x:Key="BodyFont">Segoe UI Variable Text, Segoe UI</FontFamily>',
              '<FontFamily x:Key="BodyFont">Inter, Segoe UI Variable Text, Segoe UI</FontFamily>')
marker = '    <!-- Compatibility aliases kept while legacy screens are rebuilt. -->\n'
empty = '''    <Style x:Key="EmptyState" TargetType="StackPanel">
        <Setter Property="HorizontalAlignment" Value="Center" />
        <Setter Property="VerticalAlignment" Value="Center" />
        <Setter Property="Margin" Value="0,16" />
    </Style>
    <Style x:Key="EmptyStateTitle" TargetType="TextBlock" BasedOn="{StaticResource CardTitle}">
        <Setter Property="HorizontalAlignment" Value="Center" />
        <Setter Property="Margin" Value="0,10,0,0" />
    </Style>
    <Style x:Key="EmptyStateBody" TargetType="TextBlock" BasedOn="{StaticResource Caption}">
        <Setter Property="HorizontalAlignment" Value="Center" />
        <Setter Property="Margin" Value="0,4,0,0" />
    </Style>

'''
assert marker in s
s = s.replace(marker, empty + marker, 1)
write(p, s)

for p in ['src/XDLauncher/Views/HomeView.xaml', 'src/XDLauncher/Views/SettingsView.xaml']:
    s = read(p)
    s = s.replace('Foreground="White"', 'Foreground="{DynamicResource HeroForegroundBrush}"')
    s = s.replace('Foreground="#D8D2E8"', 'Foreground="{DynamicResource HeroForegroundBrush}"')
    s = s.replace('Foreground="#D9D3E8"', 'Foreground="{DynamicResource HeroForegroundBrush}"')
    write(p, s)

p = 'src/XDLauncher/Views/JavaView.xaml'
s = read(p)
old = '<StackPanel HorizontalAlignment="Center">\n                                    <TextBlock Text="&#xE7BA;"'
new = '<StackPanel Style="{StaticResource EmptyState}">\n                                    <TextBlock Text="&#xE7BA;"'
assert old in s
s = s.replace(old, new, 1)
old = '''                                    <TextBlock Text="Cambia el filtro o pulsa Actualizar." Style="{StaticResource Muted}" HorizontalAlignment="Center" />
'''
new = '''                                    <TextBlock Text="Cambia el filtro o pulsa Actualizar." Style="{StaticResource EmptyStateBody}" />
                                    <Button Content="Actualizar versiones" Style="{StaticResource PrimaryButton}" Margin="0,16,0,0"
                                            Command="{Binding RefreshCommand}" />
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/Services/SettingsService.cs'
s = read(p)
old = 'public bool RespectSystemMotion { get; set; }'
new = 'public bool RespectSystemMotion { get; set; } = true;'
assert old in s
s = s.replace(old, new, 1)
write(p, s)
