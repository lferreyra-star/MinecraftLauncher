import io

def banner(kicker, title, subtitle, chips):
    chip_blocks = ''.join(
        f'''\n                        <Border Style="{{StaticResource Pill}}" Margin="0,0,8,0">
                            <TextBlock Text="{c}" FontSize="10" FontWeight="SemiBold" />
                        </Border>''' for c in chips)
    return f'''                <Border Style="{{StaticResource HeroBanner}}" Height="164" Margin="0,0,0,14">
                    <Border.Background>
                        <ImageBrush ImageSource="{{Binding DataContext.CurrentBackgroundPath, RelativeSource={{RelativeSource AncestorType=Window}}, Converter={{StaticResource ImagePath}}}}"
                                    Stretch="UniformToFill" />
                    </Border.Background>
                    <Border Style="{{StaticResource HeroOverlay}}" />
                    <StackPanel Margin="26" VerticalAlignment="Bottom">
                        <TextBlock Text="{kicker}" Style="{{StaticResource Kicker}}" />
                        <TextBlock Text="{title}" Style="{{StaticResource HeroTitle}}" FontSize="28" Margin="0,6,0,0" />
                        <TextBlock Text="{subtitle}" Style="{{StaticResource BodyText}}" MaxWidth="620" Margin="0,4,0,0" />
                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">{chip_blocks}
                        </StackPanel>
                    </StackPanel>
                </Border>
'''

patches = [
 ('src/XDLauncher/Views/JavaView.xaml',
  '            <StackPanel>\n                <Border Style="{StaticResource Card}" Padding="22">',
  banner('BIBLIOTECA JAVA', 'Versiones y lanzamiento',
         'Instala versiones oficiales, aísla perfiles y lanza Minecraft con la configuración que necesitas.',
         ['{Binding LatestReleaseLabel}', '{Binding WorkspaceName}'])),
 ('src/XDLauncher/Views/VersionsView.xaml',
  '        <StackPanel Margin="0,0,4,20">\n            <Border Style="{StaticResource Card}" Padding="22">',
  banner('BEDROCK · VERSIONES', 'Actualizaciones oficiales',
         'Comprueba la versión instalada, la fecha del catálogo y el estado real del paquete de Microsoft Store.',
         ['{Binding Version}', '{Binding Channel}'])),
 ('src/XDLauncher/Views/AddonsView.xaml',
  '            <StackPanel Grid.Row="0">\n                <Border Style="{StaticResource Card}" Padding="22">',
  banner('BIBLIOTECA VISUAL', 'Complementos',
         'Shaders, texturas, skins, mapas y mundos importados a las carpetas oficiales de Minecraft.',
         ['{Binding CountLabel}'])),
 ('src/XDLauncher/Views/DownloadView.xaml',
  '            <StackPanel Grid.Row="0">\n                <Border Style="{StaticResource Card}" Padding="22">',
  banner('CENTRO DE DESCARGAS', 'Descargas y herramientas',
         'Progreso real, verificación de hashes y acceso directo a las fuentes oficiales.',
         ['{Binding ActiveDownloads} descargas activas'])),
 ('src/XDLauncher/Views/MobileView.xaml',
  '            <StackPanel Margin="0,0,14,0" VerticalAlignment="Top">\n                <Border Style="{StaticResource Card}" Padding="22">',
  banner('MINECRAFT MOBILE CONNECT', 'Juega desde tu teléfono',
         'Conecta Android por USB o Wi-Fi y transmite tu mundo al PC con scrcpy.',
         ['{Binding StatusLabel}', '{Binding DeviceName}'])),
 ('src/XDLauncher/Views/GameView.xaml',
  '            <StackPanel>\n                <Border Style="{StaticResource Card}" Padding="22">',
  banner('BEDROCK EDITION', 'Estado del juego',
         'Instalación, componentes, archivos y lanzamiento de Minecraft desde Microsoft Store.',
         ['{Binding VersionLabel}', '{Binding ChannelLabel}'])),
]

for path, anchor, hero in patches:
    s = io.open(path, encoding='utf-8-sig').read()
    if anchor not in s:
        print('MISS', path)
        continue
    s = s.replace(anchor, anchor.split('\n')[0] + '\n' + hero + '\n' + anchor.split('\n')[1], 1)
    io.open(path, 'w', encoding='utf-8').write(s)
    print('OK', path)
