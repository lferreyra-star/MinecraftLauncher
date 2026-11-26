import io
import re

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Views/JavaView.xaml'
s = read(p)
s, count = re.subn(r'<ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="620" Margin="0,14,0,0">\s*<ItemsControl ItemsSource="\{Binding Versions\}">', '<ItemsControl ItemsSource="{Binding Versions}" Margin="0,14,0,0">', s, count=1)
assert count == 1
s, count = re.subn(r'</ItemsControl>\s*</ScrollViewer>', '</ItemsControl>', s, count=1)
assert count == 1
old = '<Button Content="Descargar mods" Style="{StaticResource LaunchSecondaryButton}" Margin="0,8,0,0" />'
new = '<Button Content="Descargar mods" Style="{StaticResource LaunchSecondaryButton}" Margin="0,8,0,0" Command="{Binding ModDownloadCommand}" />'
assert old in s
s = s.replace(old, new)
s = s.replace('VERSICONES', 'VERSIONES')
s = s.replace(' MaxHeight="42"', '')
write(p, s)

p = 'src/XDLauncher/Views/AboutView.xaml'
s = read(p)
old = '    <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">\n        <StackPanel Margin="0,0,4,20">'
new = '    <StackPanel Margin="0,0,4,32">'
assert old in s
s = s.replace(old, new, 1)
old = '    </ScrollViewer>\n</UserControl>'
new = '    </StackPanel>\n</UserControl>'
assert old in s
s = s.replace(old, new, 1)
write(p, s)
