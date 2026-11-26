import io
p = 'src/XDLauncher/MainWindow.xaml'
s = io.open(p, encoding='utf-8-sig').read()

topbar = s.index('<Border x:Name="TopBar"')
start = s.index('                    <Grid>\n                        <Grid.ColumnDefinitions>', topbar)
end = s.index('                </Border>\n                <Border x:Name="TopTabsBar"', start)
new = '''                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>

                        <StackPanel Orientation="Horizontal" Margin="16,0,12,0" VerticalAlignment="Center">
                            <Border Width="38" Height="38" CornerRadius="11" Background="{DynamicResource AccentSoftBrush}"
                                    BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                <Image Source="/Assets/logo.png" Width="17" Height="17" />
                            </Border>
                            <StackPanel Margin="9,0,0,0">
                                <TextBlock Text="XD LAUNCHER" Style="{StaticResource BrandText}" FontSize="14" />
                                <TextBlock Text="ECLIPSE" Style="{StaticResource BrandKicker}" FontSize="8.5" />
                            </StackPanel>
                        </StackPanel>

                        <StackPanel x:Name="HeaderPanel" Grid.Column="1" VerticalAlignment="Center" Margin="0,0,18,0"
                                    ClipToBounds="True" RenderTransformOrigin="0,0.5">
                            <StackPanel Orientation="Horizontal">
                                <Border Width="38" Height="38" CornerRadius="12" Background="{DynamicResource AccentSoftBrush}"
                                        BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                    <TextBlock Text="{Binding CurrentGlyph}" FontFamily="{StaticResource IconFont}" FontSize="16"
                                               Foreground="{DynamicResource AccentBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Border>
                                <StackPanel Margin="11,0,0,0">
                                    <TextBlock Text="{Binding CurrentTitle}" Style="{StaticResource PageTitle}" FontSize="20"
                                               MaxWidth="340" TextTrimming="CharacterEllipsis" />
                                    <TextBlock Text="{Binding CurrentSubtitle}" Style="{StaticResource Caption}" FontSize="10.5"
                                               MaxWidth="420" TextTrimming="CharacterEllipsis" />
                                </StackPanel>
                            </StackPanel>
                        </StackPanel>

                        <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,8,0">
                            <Border Style="{StaticResource Pill}" Padding="10,6" Margin="0,0,8,0">
                                <StackPanel Orientation="Horizontal">
                                    <Ellipse Width="7" Height="7" Fill="{DynamicResource SuccessBrush}" VerticalAlignment="Center" Margin="0,0,7,0" />
                                    <TextBlock Text="{Binding TopBarStatusText}" Style="{StaticResource Caption}" />
                                </StackPanel>
                            </Border>
                            <Border Style="{StaticResource Pill}" Padding="10,6" Margin="0,0,8,0"
                                    Visibility="{Binding Game.IsGameRunning, Converter={StaticResource BoolToVisibility}}">
                                <TextBlock Text="EN JUEGO" FontSize="9.5" FontWeight="Bold" Foreground="{DynamicResource AccentLightBrush}" />
                            </Border>
                            <Border Style="{StaticResource Pill}" Padding="10,6" Margin="0,0,10,0">
                                <TextBlock FontSize="9.5" FontWeight="SemiBold" Foreground="{DynamicResource TextSecondaryBrush}">
                                    <Run Text="{Binding Downloads.ActiveDownloads, Mode=OneWay}" /><Run Text=" descargas" />
                                </TextBlock>
                            </Border>
                            <Button Style="{StaticResource GhostButton}" Height="34" Padding="9,0" Margin="0,0,10,0"
                                    Command="{Binding NavigateCommand}" CommandParameter="profile" ToolTip="Selector de perfil">
                                <StackPanel Orientation="Horizontal">
                                    <Border Width="26" Height="26" CornerRadius="9" Background="{DynamicResource AccentSoftBrush}"
                                            BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                        <TextBlock Text="{Binding AccountInitial}" FontSize="11" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource AccentLightBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                    </Border>
                                    <TextBlock Text="{Binding AccountDisplayName}" Margin="8,0,5,0" MaxWidth="130" FontSize="11.5"
                                               TextTrimming="CharacterEllipsis" VerticalAlignment="Center" />
                                </StackPanel>
                            </Button>
                            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                <Button Style="{StaticResource ChromeButton}" Content="&#xE921;" Click="Minimize_Click" ToolTip="Minimizar" />
                                <Button x:Name="MaximizeButton" Style="{StaticResource ChromeButton}" Content="&#xE922;" Click="Maximize_Click" ToolTip="Maximizar" />
                                <Button Style="{StaticResource ChromeCloseButton}" Content="&#xE8BB;" Click="Close_Click" ToolTip="Cerrar" />
                            </StackPanel>
                        </StackPanel>
                    </Grid>
'''
s = s[:start] + new + s[end:]

s = s.replace('''                        <RowDefinition Height="96" />''', '''                        <RowDefinition Height="116" />''', 1)
s = s.replace('''                        <TextBlock Grid.Row="1" Text="{Binding LauncherVersion, StringFormat='v{0}'}" Style="{StaticResource Caption}"
                                   HorizontalAlignment="Left" Margin="22,6,0,0" VerticalAlignment="Bottom" />''', '''                        <TextBlock Grid.Row="1" Text="{Binding LauncherVersion, StringFormat='v{0}'}" Style="{StaticResource Caption}"
                                   HorizontalAlignment="Left" Margin="22,4,0,6" VerticalAlignment="Bottom" />''', 1)

io.open(p, 'w', encoding='utf-8').write(s)
