import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()

def row(label, desc, prop):
    return f'''                    <Grid Margin="0,10,0,0">
                        <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                        <StackPanel Margin="0,0,12,0">
                            <TextBlock Text="{label}" Style="{{StaticResource BodyText}}" />
                            <TextBlock Text="{desc}" Style="{{StaticResource Caption}}" Margin="0,2,0,0" />
                        </StackPanel>
                        <CheckBox Grid.Column="1" Style="{{StaticResource ToggleSwitch}}" IsChecked="{{Binding State.{prop}, Mode=TwoWay}}" />
                    </Grid>'''

old_appearance = '''            <Border Style="{StaticResource SettingsPanelAppearance}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="APARENCIA" Style="{StaticResource SectionTitle}" />
                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,14,0,0">
                        <StackPanel>
                            <TextBlock Text="Tema base" Style="{StaticResource Caption}" />
                            <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                <RadioButton GroupName="AppearanceTheme" Style="{StaticResource SegmentRadio}" Content="Oscuro" Width="110" Margin="0,0,8,0"
                                             IsChecked="{Binding State.IsDarkTheme, Mode=TwoWay}" />
                                <RadioButton Grid.Column="1" GroupName="AppearanceTheme" Style="{StaticResource SegmentRadio}" Content="Claro" Width="110"
                                             IsChecked="{Binding State.IsLightTheme, Mode=TwoWay}" />
                            </StackPanel>
                            <TextBlock Text="Sistema oscuro predeterminado: Switch-like. Sistema claro: skins pálidas y sin solo elementos a blanco."
                                       Style="{StaticResource Caption}" />
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,12,0,0">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                            <StackPanel Margin="0,0,12,0">
                                <TextBlock Text="RAM de visuales" Style="{StaticResource Caption}" />
                                <TextBlock Text="Glances, partículas y fundidos ligeros." Style="{StaticResource Caption}" Margin="0,4,0,0" />
                            </StackPanel>
                            <CheckBox Grid.Column="1" Style="{StaticResource ToggleSwitch}" AutomationProperties.AutomationId="AnimationsToggle" IsChecked="{Binding State.AnimationsEnabled, Mode=TwoWay}" />
                            <TextBlock Grid.Column="1" Text="Apages y fundidos apagados" Style="{StaticResource Caption}"
                                       Visibility="{Binding State.AnimationsEnabled, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}"
                                       Foreground="{DynamicResource TextMutedBrush}" Margin="10,0,0,0" />
                        </Grid>
                    </Border>
                </StackPanel>
            </Border>'''

new_appearance = '''            <Border Style="{StaticResource SettingsPanelAppearance}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="APARIENCIA" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Diez temas completos: superficies, bordes, textos, glows y fondos cambian a la vez."
                               Style="{StaticResource Caption}" Margin="0,4,0,0" />

                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,14,0,0">
                        <StackPanel>
                            <TextBlock Text="TEMA VISUAL" Style="{StaticResource Kicker}" />
                            <ListBox ItemsSource="{Binding ThemeOptions}" SelectedItem="{Binding State.Theme, Mode=TwoWay}"
                                     Background="Transparent" BorderThickness="0" Margin="0,10,0,0">
                                <ListBox.ItemsPanel><ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate></ListBox.ItemsPanel>
                                <ListBox.ItemContainerStyle>
                                    <Style TargetType="ListBoxItem">
                                        <Setter Property="Margin" Value="0,0,10,10" />
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="ListBoxItem">
                                                    <Border x:Name="Chrome" Width="150" Height="56" CornerRadius="12"
                                                            Background="{DynamicResource ChipBrush}" BorderThickness="1"
                                                            BorderBrush="{DynamicResource BorderSoftBrush}" Padding="12,8">
                                                        <StackPanel>
                                                            <Border Height="3" CornerRadius="2" Background="{DynamicResource AccentGradientBrush}" />
                                                            <TextBlock Text="{Binding}" FontWeight="SemiBold" FontSize="11.5" Margin="0,7,0,0" />
                                                        </StackPanel>
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
                        </StackPanel>
                    </Border>

                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,12,0,0">
                        <StackPanel>
                            <TextBlock Text="ILUMINACIÓN Y PROFUNDIDAD" Style="{StaticResource Kicker}" />
                            <Grid Margin="0,10,0,0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <TextBlock Text="Intensidad ambiental" VerticalAlignment="Center" />
                                <Slider Grid.Column="1" Minimum="0" Maximum="1" TickFrequency="0.05" IsSnapToTickEnabled="True"
                                        Value="{Binding State.AmbientIntensity, Mode=TwoWay}" Margin="14,0" />
                                <TextBlock Grid.Column="2" Text="{Binding State.AmbientIntensity, StringFormat={}{0:P0}}" Foreground="{DynamicResource AccentBrush}" />
                            </Grid>
                            <Grid Margin="0,10,0,0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <TextBlock Text="Transparencia" VerticalAlignment="Center" />
                                <Slider Grid.Column="1" Minimum="0" Maximum="1" TickFrequency="0.05" IsSnapToTickEnabled="True"
                                        Value="{Binding State.TransparencyLevel, Mode=TwoWay}" Margin="14,0" />
                                <TextBlock Grid.Column="2" Text="{Binding State.TransparencyLevel, StringFormat={}{0:P0}}" Foreground="{DynamicResource AccentBrush}" />
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,12,0,0">
                        <StackPanel>
                            <TextBlock Text="EFECTOS" Style="{StaticResource Kicker}" />
                            ''' + row('Animaciones de interfaz', 'Fundidos, entradas y microinteracciones.', 'AnimationsEnabled') + '''
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>'''

if old_appearance not in s:
    print('MISS appearance')
else:
    s = s.replace(old_appearance, new_appearance)

old_motion_start = s.index('            <Border Style="{StaticResource SettingsPanelMotion}"')
old_motion_end = s.index('            <Border Style="{StaticResource SettingsPanelInterface}"', old_motion_start)
new_motion = '''            <Border Style="{StaticResource SettingsPanelMotion}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="ANIMACIONES" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Controla cada efecto por separado. Todo se guarda al instante." Style="{StaticResource Caption}" Margin="0,4,0,0" />
                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,14,0,0">
                        <StackPanel>
''' + row('Transiciones de navegación', 'Deslizamiento al cambiar de sección.', 'NavTransitionsEnabled') + \
    row('Partículas ambientales', 'Luces suspendidas en el fondo.', 'ParticleEffectsEnabled') + \
    row('Efectos hover', 'Elevación y brillo al pasar el cursor.', 'HoverEffectsEnabled') + \
    row('Parallax del fondo', 'Profundidad suave según el puntero.', 'ParallaxEnabled') + \
    row('Animaciones de tarjetas', 'Aparición escalonada del contenido.', 'CardAnimationsEnabled') + \
    row('Animaciones de carga', 'Skeletons y progreso animado.', 'LoadingAnimationsEnabled') + \
    row('Auras ambientales', 'Orbes luminosos en movimiento.', 'AmbientEffectsEnabled') + '''
                        </StackPanel>
                    </Border>
                    <Border Style="{StaticResource InnerCard}" Padding="16" Margin="0,12,0,0">
''' + row('Modo de alto rendimiento', 'Desactiva partículas, parallax y animaciones costosas.', 'PerformanceMode') + '''
                    </Border>
                </StackPanel>
            </Border>

'''
s = s[:old_motion_start] + new_motion + s[old_motion_end:]

# Añadir galería de temas también en la categoría Temas
anchor = '''            <Border Style="{StaticResource SettingsPanelThemes}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="ACENTOS" Style="{StaticResource SectionTitle}" />'''
replacement = '''            <Border Style="{StaticResource SettingsPanelThemes}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="TEMAS Y ACENTOS" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Selecciona un tema en Apariencia y después ajusta el color de acento aquí."
                               Style="{StaticResource Caption}" Margin="0,4,0,0" />'''
if anchor in s:
    s = s.replace(anchor, replacement)

io.open(p, 'w', encoding='utf-8').write(s)
print('patched settings view')
