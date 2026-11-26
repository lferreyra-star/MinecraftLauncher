import io
p = 'src/XDLauncher/MainWindow.xaml'
s = io.open(p, encoding='utf-8-sig').read()

account_style = '''
        <Style x:Key="AccountChipToggle" TargetType="ToggleButton">
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="FocusVisualStyle" Value="{x:Null}" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ToggleButton">
                        <Border x:Name="Chrome" Background="{DynamicResource ChipBrush}" BorderBrush="{DynamicResource BorderSoftBrush}"
                                BorderThickness="1" CornerRadius="13" Padding="9" Margin="14,8,14,0">
                            <Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <Border Width="32" Height="32" CornerRadius="10" Background="{DynamicResource AccentSoftBrush}"
                                        BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                    <TextBlock Text="{Binding AccountInitial}" FontWeight="SemiBold" FontSize="13"
                                               Foreground="{DynamicResource AccentLightBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Border>
                                <StackPanel Grid.Column="1" Margin="10,0,4,0" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding DisplayName}" FontWeight="SemiBold" FontSize="12.5"
                                               TextTrimming="CharacterEllipsis" />
                                    <TextBlock Text="{Binding AccountProviderLabel}" Style="{StaticResource Caption}" FontSize="10" />
                                </StackPanel>
                                <TextBlock Grid.Column="2" Text="&#xE70D;" FontFamily="{StaticResource IconFont}" FontSize="9"
                                           Foreground="{DynamicResource TextMutedBrush}" VerticalAlignment="Center" />
                            </Grid>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                            </Trigger>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentSoftBrush}" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
'''
s = s.replace('    </Window.Resources>', account_style + '    </Window.Resources>')

# Sidebar footer: account chip + popup
start = s.index('                    <Grid Grid.Row="2">')
end = s.index('\n                </Grid>\n            </Border>', start)
footer = '''                    <Grid Grid.Row="2" Margin="0,0,0,4">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="*" />
                        </Grid.RowDefinitions>

                        <Border Style="{StaticResource GlassCard}" Margin="14,10,14,0" Padding="10"
                                Effect="{StaticResource PanelShadow}">
                            <StackPanel Orientation="Horizontal">
                                <Ellipse Width="8" Height="8" Fill="{DynamicResource SuccessBrush}" VerticalAlignment="Center" Margin="0,0,9,0" />
                                <StackPanel>
                                    <TextBlock Text="{Binding GameStatusText}" Style="{StaticResource Caption}" />
                                    <TextBlock Text="{Binding TopBarActivityText}" Style="{StaticResource Caption}" FontSize="10" />
                                </StackPanel>
                            </StackPanel>
                        </Border>

                        <ToggleButton x:Name="AccountToggle" Grid.Row="1" Style="{StaticResource AccountChipToggle}" />
                        <Popup x:Name="AccountPopup" Placement="Top" PlacementTarget="{Binding ElementName=AccountToggle}"
                               StaysOpen="False" IsOpen="{Binding IsChecked, ElementName=AccountToggle, Mode=TwoWay}">
                            <Border Background="{DynamicResource SurfaceRaisedBrush}" BorderBrush="{DynamicResource AccentBrush}"
                                    BorderThickness="1" CornerRadius="14" Padding="8" Width="260" Margin="0,0,0,8"
                                    Effect="{StaticResource PanelShadow}">
                                <StackPanel>
                                    <TextBlock Text="{Binding AccountDisplayName}" FontWeight="SemiBold" Margin="8,6,0,0" />
                                    <TextBlock Text="{Binding AccountEmail}" Style="{StaticResource Caption}" Margin="8,2,0,8" />
                                    <Border Style="{StaticResource Divider}" Margin="0,0,0,4" />
                                    <Button Content="Ver perfil" Style="{StaticResource GhostButton}" HorizontalContentAlignment="Left"
                                            Command="{Binding NavigateCommand}" CommandParameter="profile" Click="AccountMenuClick" />
                                    <Button Content="Cerrar sesión" Style="{StaticResource DangerButton}" HorizontalContentAlignment="Left"
                                            Command="{Binding SignOutCommand}" Click="AccountMenuClick" />
                                </StackPanel>
                            </Border>
                        </Popup>

                        <TextBlock Grid.Row="2" Text="{Binding LauncherVersion, StringFormat='v{0}'}" Style="{StaticResource Caption}"
                                   HorizontalAlignment="Left" Margin="22,8,0,0" VerticalAlignment="Bottom" />
                    </Grid>'''
s = s[:start] + footer + s[end:]

# Topbar: logo + status + activity + page title + account + window controls
start = s.index('                <Border x:Name="TopBar"')
end = s.index('\n                <ContentControl x:Name="PageHost"', start)
topbar = '''                <Border x:Name="TopBar" Visibility="{Binding IsFullscreen, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}"
                        Background="{DynamicResource TopBarBrush}" BorderBrush="{DynamicResource BorderSoftBrush}"
                        BorderThickness="0,0,0,1" MouseLeftButtonDown="TitleBar_MouseLeftButtonDown">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
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

                        <Border Grid.Column="1" Style="{StaticResource Pill}" Padding="10,6" Margin="0,0,10,0">
                            <StackPanel Orientation="Horizontal">
                                <Ellipse Width="7" Height="7" Fill="{DynamicResource SuccessBrush}" VerticalAlignment="Center" Margin="0,0,7,0" />
                                <TextBlock Text="{Binding TopBarStatusText}" Style="{StaticResource Caption}" />
                            </StackPanel>
                        </Border>

                        <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,12,0">
                            <Border Style="{StaticResource Pill}" Padding="10,6" Margin="0,0,6,0"
                                    Visibility="{Binding Game.IsGameRunning, Converter={StaticResource BoolToVisibility}}">
                                <TextBlock Text="EN JUEGO" FontSize="9.5" FontWeight="Bold" Foreground="{DynamicResource AccentLightBrush}" />
                            </Border>
                            <Border Style="{StaticResource Pill}" Padding="10,6">
                                <TextBlock FontSize="9.5" FontWeight="SemiBold" Foreground="{DynamicResource TextSecondaryBrush}">
                                    <Run Text="{Binding Downloads.ActiveDownloads, Mode=OneWay}" /><Run Text=" descargas" />
                                </TextBlock>
                            </Border>
                        </StackPanel>

                        <StackPanel Grid.Column="3" VerticalAlignment="Center" Margin="0,0,18,0">
                            <StackPanel Orientation="Horizontal">
                                <Border Width="38" Height="38" CornerRadius="12" Background="{DynamicResource AccentSoftBrush}"
                                        BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                    <TextBlock Text="{Binding CurrentGlyph}" FontFamily="{StaticResource IconFont}" FontSize="16"
                                               Foreground="{DynamicResource AccentBrush}" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Border>
                                <StackPanel Margin="11,0,0,0">
                                    <TextBlock Text="{Binding CurrentTitle}" Style="{StaticResource PageTitle}" FontSize="20" />
                                    <TextBlock Text="{Binding CurrentSubtitle}" Style="{StaticResource Caption}" FontSize="10.5" />
                                </StackPanel>
                            </StackPanel>
                        </StackPanel>

                        <Button Grid.Column="4" Style="{StaticResource GhostButton}" Height="34" Padding="9,0" Margin="0,0,10,0"
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

                        <StackPanel Grid.Column="5" Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,8,0">
                            <Button Style="{StaticResource ChromeButton}" Content="&#xE921;" Click="Minimize_Click" ToolTip="Minimizar" />
                            <Button x:Name="MaximizeButton" Style="{StaticResource ChromeButton}" Content="&#xE922;" Click="Maximize_Click" ToolTip="Maximizar" />
                            <Button Style="{StaticResource ChromeCloseButton}" Content="&#xE8BB;" Click="Close_Click" ToolTip="Cerrar" />
                        </StackPanel>
                    </Grid>
                </Border>'''
s = s[:start] + topbar + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
print('patched main window')
