import io
import re

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Views/SettingsView.xaml'
s = read(p)
anchor = s.index('ESCALA DE INTERFAZ')
start = s.index('<StackPanel Margin="0,8,0,0">', anchor)
end = s.index('</StackPanel>', start) + len('</StackPanel>')
new = '''<Slider Width="220" Minimum="0.85" Maximum="1.15" TickFrequency="0.05"
                                         IsSnapToTickEnabled="True" HorizontalAlignment="Left" Margin="0,12,0,0"
                                         Value="{Binding State.UiScale, Mode=TwoWay}" />
                                 <Grid Width="220" Margin="0,4,0,0" HorizontalAlignment="Left">
                                     <TextBlock Text="85 %" Style="{StaticResource Caption}" FontSize="10" />
                                     <TextBlock Text="100 %" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Center" />
                                     <TextBlock Text="115 %" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                                 </Grid>'''
s = s[:start] + new + s[end:]
write(p, s)

p = 'src/XDLauncher/Themes/Shared.xaml'
s = read(p)
count = s.count('<Setter Property="FocusVisualStyle" Value="{x:Null}" />')
assert count > 0
s = s.replace('<Setter Property="FocusVisualStyle" Value="{x:Null}" />', '<Setter Property="FocusVisualStyle" Value="{StaticResource FocusVisual}" />')
write(p, s)
