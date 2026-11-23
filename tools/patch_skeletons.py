import io

def skeleton(widths, prop):
    bars = ''.join(f'\n                            <Border Style="{{StaticResource SkeletonBar}}" Width="{w}" Margin="0,6,0,0" />' for w in widths)
    return f'''                    <StackPanel Visibility="{{Binding {prop}, Converter={{StaticResource BoolToVisibility}}}}" Margin="0,12,0,0">
                        <Border Style="{{StaticResource InnerCard}}" Padding="16">
                            <StackPanel>{bars}
                            </StackPanel>
                        </Border>
                    </StackPanel>
'''

patches = [
 ('src/XDLauncher/Views/JavaView.xaml',
  '                        <ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="620" Margin="0,14,0,0">',
  skeleton([420, 320, 260, 380], 'IsLoading')),
 ('src/XDLauncher/Views/ModsCenterView.xaml',
  '                        <ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="520" Margin="0,10,0,0">',
  skeleton([520, 460, 380], 'IsLoading')),
 ('src/XDLauncher/Views/AddonsView.xaml',
  '            <ItemsControl ItemsSource="{Binding VisibleItems}">',
  skeleton([380, 300, 420], 'IsBusy')),
]

for path, anchor, block in patches:
    s = io.open(path, encoding='utf-8-sig').read()
    if anchor not in s:
        print('MISS', path)
        continue
    s = s.replace(anchor, block + anchor, 1)
    io.open(path, 'w', encoding='utf-8').write(s)
    print('OK', path)
