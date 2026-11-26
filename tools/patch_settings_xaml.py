import io

p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()

start = s.index('            <Grid>\n                <StackPanel VerticalAlignment="Center">')
end = s.index('            <Border Style="{StaticResource SettingsPanelAccount}"', start)
header = '''            <Border Height="224" CornerRadius="18" ClipToBounds="True" Margin="0,0,0,16"
                    BorderBrush="{DynamicResource BorderSoftBrush}" BorderThickness="1">
                <Grid>
                    <Grid.Background>
                        <ImageBrush ImageSource="pack://application:,,,/Assets/backgrounds/bg-fantastico.png" Stretch="UniformToFill" />
                    </Grid.Background>
                    <Border Style="{StaticResource HeroOverlay}" />
                    <Grid>
                        <StackPanel VerticalAlignment="Center" Margin="30,0,0,0" MaxWidth="760">
                            <Border Style="{StaticResource Badge}" HorizontalAlignment="Left">
                                <TextBlock Text="CENTRO DE CONTROL" FontSize="9" FontWeight="Bold"
                                           Foreground="{DynamicResource AccentLightBrush}" />
                            </Border>
                            <TextBlock Text="Configura tu experiencia" Style="{StaticResource DisplayHero}"
                                       FontSize="34" Foreground="White" Margin="0,10,0,0" />
                            <TextBlock Text="Identidad, apariencia, mundo y sistema. Todo se aplica al instante y se conserva entre sesiones."
                                       Style="{StaticResource BodyText}" Foreground="#D9D3E8" MaxWidth="640" Margin="0,8,0,0" />
                            <StackPanel Orientation="Horizontal" Margin="0,16,0,0">
                                <Border Style="{StaticResource Pill}" Margin="0,0,8,0">
                                    <TextBlock Text="{Binding State.Theme, StringFormat='Tema · {0}'}" FontSize="10" FontWeight="SemiBold" />
                                </Border>
                                <Border Style="{StaticResource Pill}" Margin="0,0,8,0">
                                    <TextBlock Text="{Binding State.MotionLevelLabel, StringFormat='Movimiento · {0}'}" FontSize="10" FontWeight="SemiBold" />
                                </Border>
                                <Border Style="{StaticResource Pill}">
                                    <TextBlock Text="{Binding LauncherVersion, StringFormat='v{0}'}" FontSize="10" FontWeight="SemiBold" />
                                </Border>
                            </StackPanel>
                        </StackPanel>

                        <Border x:Name="SettingsHeroIcon" Width="92" Height="92" CornerRadius="26"
                                HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,40,0"
                                Background="{DynamicResource GlassStrongBrush}" BorderBrush="{DynamicResource AccentBrush}"
                                BorderThickness="1" RenderTransformOrigin="0.5,0.5" Effect="{StaticResource AccentGlowEffect}">
                            <Border.RenderTransform><ScaleTransform x:Name="SettingsHeroIconScale" ScaleX="0.9" ScaleY="0.9" /></Border.RenderTransform>
                            <TextBlock Text="&#xE713;" FontFamily="{StaticResource IconFont}" FontSize="38"
                                       Foreground="{DynamicResource AccentLightBrush}"
                                       HorizontalAlignment="Center" VerticalAlignment="Center" />
                            <Border.Triggers>
                                <EventTrigger RoutedEvent="Loaded">
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetName="SettingsHeroIconScale" Storyboard.TargetProperty="ScaleX"
                                                             To="1" Duration="0:0:0.6">
                                                <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                            </DoubleAnimation>
                                            <DoubleAnimation Storyboard.TargetName="SettingsHeroIconScale" Storyboard.TargetProperty="ScaleY"
                                                             To="1" Duration="0:0:0.6">
                                                <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                            </DoubleAnimation>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </EventTrigger>
                            </Border.Triggers>
                        </Border>
                    </Grid>
                </Grid>
            </Border>

            <Border Style="{StaticResource Card}" Margin="0,0,0,14" Padding="12">
                <ListBox ItemsSource="{Binding Categories}" SelectedValuePath="Key"
                         SelectedValue="{Binding SelectedCategory, Mode=TwoWay}" BorderThickness="0"
                         Background="Transparent" ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                    <ListBox.ItemsPanel><ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate></ListBox.ItemsPanel>
                    <ListBox.ItemContainerStyle>
                        <Style TargetType="ListBoxItem">
                            <Setter Property="Margin" Value="0,0,8,8" />
                            <Setter Property="Cursor" Value="Hand" />
                            <Setter Property="Template">
                                <Setter.Value>
                                    <ControlTemplate TargetType="ListBoxItem">
                                        <Border x:Name="Chrome" Width="184" Height="62" CornerRadius="13"
                                                Background="{DynamicResource ChipBrush}" BorderThickness="1"
                                                BorderBrush="{DynamicResource BorderSoftBrush}" Padding="10,8">
                                            <Grid>
                                                <Border x:Name="AccentBar" Width="3" Height="30" CornerRadius="2"
                                                        HorizontalAlignment="Left" Background="{DynamicResource AccentBrush}" Opacity="0" />
                                                <TextBlock x:Name="Glyph" Text="{Binding Glyph}" FontFamily="{StaticResource IconFont}"
                                                           FontSize="17" Foreground="{DynamicResource TextMutedBrush}"
                                                           HorizontalAlignment="Left" VerticalAlignment="Center" Margin="10,0,0,0" />
                                                <StackPanel Margin="38,0,0,0" VerticalAlignment="Center">
                                                    <TextBlock Text="{Binding Title}" FontSize="12" FontWeight="SemiBold"
                                                               Foreground="{DynamicResource TextSecondaryBrush}" />
                                                    <TextBlock Text="{Binding Description}" FontSize="9.5" Margin="0,2,0,0"
                                                               Foreground="{DynamicResource TextMutedBrush}" TextTrimming="CharacterEllipsis" />
                                                </StackPanel>
                                            </Grid>
                                            <Border.RenderTransformOrigin><Point X="0.5" Y="0.5" /></Border.RenderTransformOrigin>
                                            <Border.RenderTransform><ScaleTransform x:Name="CardScale" ScaleX="1" ScaleY="1" /></Border.RenderTransform>
                                        </Border>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property="IsMouseOver" Value="True">
                                                <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                <Setter TargetName="Glyph" Property="Foreground" Value="{DynamicResource AccentLightBrush}" />
                                            </Trigger>
                                            <Trigger Property="IsSelected" Value="True">
                                                <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentSoftBrush}" />
                                                <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                <Setter TargetName="AccentBar" Property="Opacity" Value="1" />
                                                <Setter TargetName="Glyph" Property="Foreground" Value="{DynamicResource AccentLightBrush}" />
                                                <Trigger.EnterActions>
                                                    <BeginStoryboard>
                                                        <Storyboard>
                                                            <DoubleAnimation Storyboard.TargetName="CardScale" Storyboard.TargetProperty="ScaleX"
                                                                             From="0.94" To="1" Duration="0:0:0.22">
                                                                <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                            </DoubleAnimation>
                                                            <DoubleAnimation Storyboard.TargetName="CardScale" Storyboard.TargetProperty="ScaleY"
                                                                             From="0.94" To="1" Duration="0:0:0.22">
                                                                <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                            </DoubleAnimation>
                                                        </Storyboard>
                                                    </BeginStoryboard>
                                                </Trigger.EnterActions>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Setter.Value>
                            </Setter>
                        </Style>
                    </ListBox.ItemContainerStyle>
                </ListBox>
            </Border>

            <Grid x:Name="SettingsContentRoot">
'''
s = s[:start] + header + s[end:]

s = s.replace('''        </StackPanel>
    </ScrollViewer>
</UserControl>''','''            </Grid>
        </StackPanel>
    </ScrollViewer>
</UserControl>''', 1)

s = s.replace('                    <TextBlock Text="Paletas ac TECHF: verde Elite, esmeralda, Lima FÁRbnb, Cian, ocean" Style="{StaticResource Caption}" Margin="0,4,0,0" />\n', '')

s = s.replace(
    '''                                                            <Border Height="3" CornerRadius="2" Background="{DynamicResource AccentGradientBrush}" />
                                                            <TextBlock Text="{Binding}" FontWeight="SemiBold" FontSize="11.5" Margin="0,7,0,0" />''',
    '''                                                            <StackPanel Orientation="Horizontal">
                                                                <Border Width="30" Height="13" CornerRadius="3" Background="{DynamicResource AccentBrush}" Margin="0,0,4,0" />
                                                                <Border Width="30" Height="13" CornerRadius="3" Background="{DynamicResource SurfaceStrongBrush}" Margin="0,0,4,0" />
                                                                <Border Width="30" Height="13" CornerRadius="3" Background="{DynamicResource AccentSoftBrush}" />
                                                            </StackPanel>
                                                            <TextBlock Text="{Binding}" FontWeight="SemiBold" FontSize="11.5" Margin="0,7,0,0" />''',
    1,
)

motion_anchor = '''                    <TextBlock Text="Controla cada efecto por separado. Todo se guarda al instante." Style="{StaticResource Caption}" Margin="0,4,0,0" />
'''
motion_new = motion_anchor + '''                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,14,0,0">
                        <StackPanel>
                            <TextBlock Text="PERFIL DE MOVIMIENTO" Style="{StaticResource Kicker}" />
                            <ListBox ItemsSource="{Binding State.MotionLevels}" SelectedItem="{Binding State.MotionLevel, Mode=TwoWay}"
                                     Background="Transparent" BorderThickness="0" Margin="0,10,0,0">
                                <ListBox.ItemsPanel><ItemsPanelTemplate><UniformGrid Columns="4" /></ItemsPanelTemplate></ListBox.ItemsPanel>
                                <ListBox.ItemContainerStyle>
                                    <Style TargetType="ListBoxItem">
                                        <Setter Property="Margin" Value="0,0,8,0" />
                                        <Setter Property="Cursor" Value="Hand" />
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="ListBoxItem">
                                                    <Border x:Name="Chrome" Height="48" CornerRadius="12" Background="{DynamicResource ChipBrush}"
                                                            BorderBrush="{DynamicResource BorderSoftBrush}" BorderThickness="1">
                                                        <TextBlock Text="{Binding}" FontSize="11.5" FontWeight="SemiBold"
                                                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                                                   Foreground="{DynamicResource TextSecondaryBrush}" />
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property="IsMouseOver" Value="True">
                                                            <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                        </Trigger>
                                                        <Trigger Property="IsSelected" Value="True">
                                                            <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentSoftBrush}" />
                                                            <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </ListBox.ItemContainerStyle>
                            </ListBox>
                            <TextBlock Text="Los cambios se aplican de inmediato a transiciones, tarjetas, fondos y partículas."
                                       Style="{StaticResource Caption}" Margin="0,10,0,0" />
                        </StackPanel>
                    </Border>
'''
assert motion_anchor in s
s = s.replace(motion_anchor, motion_new, 1)

io.open(p, 'w', encoding='utf-8').write(s)
