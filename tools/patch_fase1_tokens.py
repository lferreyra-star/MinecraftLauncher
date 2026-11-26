import io
import re

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Themes/Shared.xaml'
s = read(p)
old = 'xmlns:core="clr-namespace:XdLauncher.Core">'
new = 'xmlns:core="clr-namespace:XdLauncher.Core"\n                     xmlns:sys="clr-namespace:System;assembly=mscorlib">'
assert old in s
s = s.replace(old, new, 1)

anchor = '    <FontFamily x:Key="IconFont">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>\n'
tokens = '''    <sys:Double x:Key="Space1">8</sys:Double>
    <sys:Double x:Key="Space2">16</sys:Double>
    <sys:Double x:Key="Space3">24</sys:Double>
    <sys:Double x:Key="Space4">32</sys:Double>
    <sys:Double x:Key="Space5">40</sys:Double>
    <CornerRadius x:Key="RadiusSm">8</CornerRadius>
    <CornerRadius x:Key="RadiusControl">11</CornerRadius>
    <CornerRadius x:Key="RadiusMd">12</CornerRadius>
    <CornerRadius x:Key="RadiusInner">14</CornerRadius>
    <CornerRadius x:Key="RadiusLg">16</CornerRadius>
    <CornerRadius x:Key="RadiusCard">18</CornerRadius>
    <CornerRadius x:Key="RadiusPill">999</CornerRadius>
    <Duration x:Key="DurationFast">0:0:0.16</Duration>
    <Duration x:Key="DurationNormal">0:0:0.2</Duration>
    <Duration x:Key="DurationSlow">0:0:0.28</Duration>
    <Duration x:Key="DurationShimmer">0:0:0.85</Duration>
'''
assert anchor in s
s = s.replace(anchor, anchor + tokens, 1)

replacements = [
    ('<Setter Property="CornerRadius" Value="18" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusCard}" />'),
    ('<Setter Property="CornerRadius" Value="14" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusInner}" />'),
    ('<Setter Property="CornerRadius" Value="11" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusControl}" />'),
    ('<Setter Property="CornerRadius" Value="12" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusMd}" />'),
    ('<Setter Property="CornerRadius" Value="999" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusPill}" />'),
    ('<Setter Property="CornerRadius" Value="8" />', '<Setter Property="CornerRadius" Value="{StaticResource RadiusSm}" />'),
    ('<Setter Property="Padding" Value="20" />', '<Setter Property="Padding" Value="{StaticResource Space3}" />'),
    ('<Setter Property="Padding" Value="16" />', '<Setter Property="Padding" Value="{StaticResource Space2}" />'),
    ('<Setter Property="Margin" Value="0,12" />', '<Setter Property="Margin" Value="0,8" />'),
    ('<Setter Property="Width" Value="10" />', '<Setter Property="Width" Value="8" />'),
    ('<Setter Property="Height" Value="10" />', '<Setter Property="Height" Value="8" />'),
    ('CornerRadius="11"', 'CornerRadius="{StaticResource RadiusControl}"'),
    ('CornerRadius="12"', 'CornerRadius="{StaticResource RadiusMd}"'),
    ('CornerRadius="18"', 'CornerRadius="{StaticResource RadiusCard}"'),
    ('CornerRadius="14"', 'CornerRadius="{StaticResource RadiusInner}"'),
    ('CornerRadius="8"', 'CornerRadius="{StaticResource RadiusSm}"'),
    ('Duration="0:0:0.16"', 'Duration="{StaticResource DurationFast}"'),
    ('Duration="0:0:0.2"', 'Duration="{StaticResource DurationNormal}"'),
    ('Duration="0:0:0.24"', 'Duration="{StaticResource DurationNormal}"'),
    ('Duration="0:0:0.3"', 'Duration="{StaticResource DurationSlow}"'),
    ('Duration="0:0:0.85"', 'Duration="{StaticResource DurationShimmer}"'),
]
for old, new in replacements:
    s = s.replace(old, new)
write(p, s)

p = 'src/XDLauncher/Views/JavaView.xaml'
s = read(p)
s, count = re.subn(r'\s*<Style x:Key="JavaScrollThumb" TargetType="Thumb">.*?</Style>', '', s, count=1, flags=re.S)
assert count == 1
s, count = re.subn(r'\s*<Style TargetType="ScrollBar">.*?</Style>', '', s, count=1, flags=re.S)
assert count == 1
write(p, s)

p = 'src/XDLauncher/Views/SettingsView.xaml'
s = read(p)
s, count = re.subn(r'\s*<Style x:Key="SettingsScrollThumb" TargetType="Thumb">.*?</Style>', '', s, count=1, flags=re.S)
assert count == 1
s, count = re.subn(r'\s*<Style TargetType="ScrollBar">.*?</Style>', '', s, count=1, flags=re.S)
assert count == 1
write(p, s)
