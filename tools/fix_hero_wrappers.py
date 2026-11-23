import io, re
files = [
 'src/XDLauncher/Views/HomeView.xaml',
 'src/XDLauncher/Views/JavaView.xaml',
 'src/XDLauncher/Views/VersionsView.xaml',
 'src/XDLauncher/Views/AddonsView.xaml',
 'src/XDLauncher/Views/DownloadView.xaml',
 'src/XDLauncher/Views/MobileView.xaml',
 'src/XDLauncher/Views/GameView.xaml',
]
pat = re.compile(r'(<Border Style="\{StaticResource HeroBanner\}".*?</StackPanel>\s*)</Border>', re.S)
for path in files:
    s = io.open(path, encoding='utf-8-sig').read()
    def fix(m):
        block = m.group(1)
        block = block.replace('<Border Style="{StaticResource HeroOverlay}" />',
                              '<Grid>\n                        <Border Style="{StaticResource HeroOverlay}" />')
        return block + '                    </Grid>\n                </Border>'
    s2, n = pat.subn(fix, s, count=1)
    print(path, n)
    io.open(path, 'w', encoding='utf-8').write(s2)
