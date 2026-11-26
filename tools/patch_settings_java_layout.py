import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()
start = s.index('            <Border Style="{StaticResource SettingsPanelJava}" Margin="0,14,0,0">')
end = s.index('            <Border Style="{StaticResource SettingsPanelDownloads}" Margin="0,14,0,0">', start)
new = '''            <Border Style="{StaticResource SettingsPanelJava}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="JAVA EDITION" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Nombre del jugador, memoria y rutas de la instalación de Java."
                               Style="{StaticResource Caption}" Margin="0,6,0,0" />

                    <Border Style="{StaticResource SettingsSubCard}" Padding="16" Margin="0,16,0,0">
                        <StackPanel>
                            <TextBlock Text="NOMBRE DEL JUGADOR" Style="{StaticResource Kicker}" />
                            <TextBox Style="{StaticResource InputTextBox}" Margin="0,12,0,0" MinWidth="240"
                                     HorizontalAlignment="Left" Text="{Binding State.JavaPlayerName, UpdateSourceTrigger=LostFocus}" />
                        </StackPanel>
                    </Border>

                    <Border Style="{StaticResource SettingsSubCard}" Padding="16" Margin="0,12,0,0">
                        <StackPanel>
                            <Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <TextBlock Text="MEMORIA PARA JAVA" Style="{StaticResource Kicker}" />
                                <TextBlock Grid.Column="1" Text="{Binding State.JavaMemoryMb, StringFormat='{}{0} MB'}"
                                           Foreground="{DynamicResource AccentBrush}" FontSize="16" FontWeight="SemiBold" />
                            </Grid>
                            <Slider Minimum="1024" Maximum="32768" IsSnapToTickEnabled="True" TickFrequency="1024"
                                    Margin="0,16,0,0" Value="{Binding State.JavaMemoryMb, Mode=TwoWay}" />
                            <Grid Margin="0,8,0,0">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="1*" /><ColumnDefinition Width="2*" /><ColumnDefinition Width="4*" />
                                    <ColumnDefinition Width="8*" /><ColumnDefinition Width="16*" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="2" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                                <TextBlock Grid.Column="1" Text="4" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                                <TextBlock Grid.Column="2" Text="8" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                                <TextBlock Grid.Column="3" Text="16" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                                <TextBlock Grid.Column="4" Text="32 GB" Style="{StaticResource Caption}" FontSize="10" HorizontalAlignment="Right" />
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border Style="{StaticResource SettingsSubCard}" Padding="16" Margin="0,12,0,0">
                        <StackPanel>
                            <TextBlock Text="RUTA DE JAVA" Style="{StaticResource Kicker}" />
                            <Grid Margin="0,12,0,0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <Grid>
                                    <TextBox x:Name="JavaPathBox" Style="{StaticResource InputTextBox}"
                                             Text="{Binding State.JavaPath, UpdateSourceTrigger=LostFocus}" />
                                    <TextBlock Text="Ruta automática detectada" Style="{StaticResource Caption}" IsHitTestVisible="False"
                                               Margin="12,0,0,0" VerticalAlignment="Center"
                                               Visibility="{Binding Text, ElementName=JavaPathBox, Converter={StaticResource StringToVisibility}, ConverterParameter=invert}" />
                                </Grid>
                                <Button Grid.Column="1" Content="Examinar" Style="{StaticResource SecondaryButton}" Margin="8,0,0,0"
                                        ToolTip="Abrir selector en Java Edition"
                                        Command="{Binding DataContext.NavigateCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="java" />
                            </Grid>
                        </StackPanel>
                    </Border>

                    <Border Style="{StaticResource SettingsSubCard}" Padding="16" Margin="0,12,0,0">
                        <StackPanel>
                            <TextBlock Text="DIRECTORIO DEL JUEGO" Style="{StaticResource Kicker}" />
                            <Grid Margin="0,12,0,0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions>
                                <Grid>
                                    <TextBox x:Name="GameDirectoryBox" Style="{StaticResource InputTextBox}"
                                             Text="{Binding State.JavaGameDirectory, UpdateSourceTrigger=LostFocus}" />
                                    <TextBlock Text="%APPDATA%\\.minecraft" Style="{StaticResource Caption}" IsHitTestVisible="False"
                                               Margin="12,0,0,0" VerticalAlignment="Center"
                                               Visibility="{Binding Text, ElementName=GameDirectoryBox, Converter={StaticResource StringToVisibility}, ConverterParameter=invert}" />
                                </Grid>
                                <Button Grid.Column="1" Content="Examinar" Style="{StaticResource SecondaryButton}" Margin="8,0,0,0"
                                        ToolTip="Abrir selector en Java Edition"
                                        Command="{Binding DataContext.NavigateCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="java" />
                            </Grid>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

'''
s = s[:start] + new + s[end:]
s = s.replace('<StackPanel Margin="0,0,4,24">', '<StackPanel Margin="0,0,4,32">', 1)
io.open(p, 'w', encoding='utf-8').write(s)
