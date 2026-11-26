import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()
start = s.index('            <Border Style="{StaticResource SettingsPanelInterface}" Margin="0,14,0,0">')
end = s.index('            <Border Style="{StaticResource SettingsPanelMinecraft}" Margin="0,14,0,0">', start)
new = '''            <Border Style="{StaticResource SettingsPanelInterface}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="INTERFAZ" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Ajusta el espacio de trabajo y la densidad visual del launcher."
                               Style="{StaticResource Caption}" Margin="0,6,0,0" />

                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,16,0,0">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
                            <StackPanel Margin="0,0,10,0">
                                <TextBlock Text="TAMAÑO DE VENTANA" Style="{StaticResource Kicker}" />
                                <ComboBox Margin="0,8,0,0" MinWidth="180"
                                          ItemsSource="{Binding State.WindowSizes}"
                                          SelectedValuePath="Label" SelectedValue="{Binding State.WindowSizeLabel}">
                                    <ComboBox.ItemTemplate><DataTemplate><TextBlock Text="{Binding Label}" /></DataTemplate></ComboBox.ItemTemplate>
                                </ComboBox>
                            </StackPanel>
                            <StackPanel Grid.Column="1" Margin="10,0,0,0">
                                <TextBlock Text="ESCALA DE INTERFAZ" Style="{StaticResource Kicker}" />
                                <TextBlock Text="{Binding State.UiScaleLabel}" Foreground="{DynamicResource AccentBrush}"
                                           FontSize="18" FontWeight="SemiBold" Margin="0,8,0,0" />
                                <StackPanel Margin="0,8,0,0">
                                    <Border Height="4" CornerRadius="2" Width="180" Background="{DynamicResource BorderSoftBrush}"
                                            HorizontalAlignment="Left" Margin="0,0,0,6" />
                                    <Border Height="4" CornerRadius="2" Width="140" Background="{DynamicResource AccentSoftBrush}"
                                            HorizontalAlignment="Left" Margin="0,0,0,6" />
                                    <Border Height="4" CornerRadius="2" Width="100" Background="{DynamicResource ChipStrongBrush}"
                                            HorizontalAlignment="Left" />
                                </StackPanel>
                            </StackPanel>
                        </Grid>
                    </Border>

                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,12,0,0">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                            <StackPanel Margin="0,0,16,0">
                                <TextBlock Text="Mostrar novedades en el inicio" Style="{StaticResource BodyText}" />
                                <TextBlock Text="Muestra el panel de noticias oficiales en la pantalla de inicio."
                                           Style="{StaticResource Caption}" Margin="0,4,0,0" />
                            </StackPanel>
                            <CheckBox Grid.Column="1" Style="{StaticResource ToggleSwitch}"
                                      IsChecked="{Binding State.ShowNews, Mode=TwoWay}" VerticalAlignment="Center" />
                        </Grid>
                    </Border>

                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,12,0,0">
                        <StackPanel>
                            <TextBlock Text="VISTA PREVIA" Style="{StaticResource Kicker}" />
                            <TextBlock Text="Así se distribuyen los paneles con la escala seleccionada."
                                       Style="{StaticResource Caption}" Margin="0,6,0,0" />
                            <Border Height="72" CornerRadius="12" Background="{DynamicResource SurfaceSunkenBrush}"
                                    BorderBrush="{DynamicResource BorderSoftBrush}" BorderThickness="1" Margin="0,12,0,0" Padding="12">
                                <Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="60" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
                                    <Border CornerRadius="8" Background="{DynamicResource AccentSoftBrush}" Margin="0,0,12,0" />
                                    <StackPanel Grid.Column="1">
                                        <Border Height="8" CornerRadius="4" Width="180" Background="{DynamicResource BorderSoftBrush}" />
                                        <Border Height="8" CornerRadius="4" Width="260" Background="{DynamicResource ChipStrongBrush}" Margin="0,10,0,0" />
                                    </StackPanel>
                                </Grid>
                            </Border>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

'''
s = s[:start] + new + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
