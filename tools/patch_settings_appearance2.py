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

new = '''            <Border Style="{StaticResource SettingsPanelAppearance}" Margin="0,14,0,0">
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
            </Border>

'''

start = s.index('            <Border Style="{StaticResource SettingsPanelAppearance}"')
end = s.index('            <Border Style="{StaticResource SettingsPanelThemes}"', start)
s = s[:start] + new + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
print('appearance replaced')
