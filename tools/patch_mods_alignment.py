import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Views/ModsCenterView.xaml'
s = read(p)

start = s.index('        <Border Grid.Row="1" Style="{StaticResource Card}" Padding="20">')
end = s.index('        <Grid Grid.Row="2" Margin="0,14,0,0">', start)
new_bar = '''        <Border Grid.Row="1" Style="{StaticResource Card}" Padding="16">
            <WrapPanel Orientation="Horizontal">
                <Grid Width="260" Height="40" Margin="0,0,8,8">
                    <TextBox x:Name="SearchBox" Style="{StaticResource InputTextBox}" Height="40"
                             Padding="34,0,10,0" AutomationProperties.Name="Buscar mods"
                             Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}">
                        <TextBox.InputBindings>
                            <KeyBinding Key="Enter" Command="{Binding SearchCommand}" />
                        </TextBox.InputBindings>
                    </TextBox>
                    <Path Width="16" Height="16" Margin="12,0,0,0" HorizontalAlignment="Left"
                          VerticalAlignment="Center" Stretch="Uniform" Fill="{DynamicResource TextMutedBrush}"
                          IsHitTestVisible="False"
                          Data="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z" />
                    <TextBlock Text="Buscar mods…" Margin="34,0,10,0" VerticalAlignment="Center"
                               Foreground="{DynamicResource TextMutedBrush}" IsHitTestVisible="False"
                               Visibility="{Binding Text, ElementName=SearchBox, Converter={StaticResource StringToVisibility}, ConverterParameter=invert}" />
                </Grid>
                <ComboBox Width="140" Height="40" Margin="0,0,8,8"
                          ItemsSource="{Binding GameVersions}" SelectedItem="{Binding SelectedGameVersion}"
                          AutomationProperties.Name="Versión de Minecraft" />
                <ComboBox Width="120" Height="40" Margin="0,0,8,8"
                          ItemsSource="{Binding Loaders}" SelectedItem="{Binding SelectedLoader}"
                          AutomationProperties.Name="Modloader" />
                <ComboBox Width="140" Height="40" Margin="0,0,8,8"
                          ItemsSource="{Binding Categories}" SelectedItem="{Binding SelectedCategory}"
                          AutomationProperties.Name="Categoría" />
                <ComboBox Width="130" Height="40" Margin="0,0,8,8"
                          ItemsSource="{Binding SortOptions}" SelectedItem="{Binding SelectedSort}"
                          AutomationProperties.Name="Orden" />
                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="0,0,0,8">
                    <Button Content="Buscar" Style="{StaticResource PrimaryButton}" Height="40" Padding="18,0"
                            Command="{Binding SearchCommand}" AutomationProperties.Name="Buscar mods" />
                    <Button Content="&#xE72C;" Style="{StaticResource IconButton}" Width="40" Height="40"
                            Margin="8,0,0,0" Command="{Binding RefreshCommand}"
                            AutomationProperties.Name="Actualizar mods" ToolTip="Actualizar" />
                </StackPanel>
            </WrapPanel>
        </Border>

'''
s = s[:start] + new_bar + s[end:]

s = s.replace(
    '<Border Visibility="{Binding SelectedProject, Converter={StaticResource NullToVisibility}, ConverterParameter=invert}" Margin="0,14,0,0">',
    '<Border Visibility="{Binding SelectedProject, Converter={StaticResource NullToVisibility}}" Margin="0,14,0,0">', 1)
s = s.replace(
    '<TextBlock Visibility="{Binding SelectedProject, Converter={StaticResource NullToVisibility}}"\n                                   Style="{StaticResource Muted}" Margin="0,14,0,0" Text="Selecciona un mod del catálogo para ver sus detalles." />',
    '<TextBlock Visibility="{Binding SelectedProject, Converter={StaticResource NullToVisibility}, ConverterParameter=invert}"\n                                   Style="{StaticResource Muted}" Margin="0,14,0,0" Text="Selecciona un mod del catálogo para ver sus detalles." />', 1)
write(p, s)
