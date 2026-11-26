import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Views/ModsCenterView.xaml'
s = read(p)

old = '''                        <ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="520" Margin="0,10,0,0">
                            <ItemsControl ItemsSource="{Binding Projects}">
                                <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
                                <ItemsControl.ItemTemplate>'''
new = '''                        <ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="520" Margin="0,10,0,0">
                            <ListBox ItemsSource="{Binding Projects}" SelectedItem="{Binding SelectedProject}"
                                     Background="Transparent" BorderThickness="0"
                                     ScrollViewer.VerticalScrollBarVisibility="Disabled"
                                     ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                                <ListBox.ItemContainerStyle>
                                    <Style TargetType="ListBoxItem">
                                        <Setter Property="Margin" Value="0,0,12,12" />
                                        <Setter Property="Padding" Value="0" />
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="ListBoxItem">
                                                    <Border x:Name="Chrome" Background="Transparent" CornerRadius="14"
                                                            BorderThickness="1" BorderBrush="Transparent">
                                                        <ContentPresenter />
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property="IsMouseOver" Value="True">
                                                            <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource RowHoverBrush}" />
                                                        </Trigger>
                                                        <Trigger Property="IsSelected" Value="True">
                                                            <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </ListBox.ItemContainerStyle>
                                <ListBox.ItemsPanel><ItemsPanelTemplate><WrapPanel Orientation="Horizontal" /></ItemsPanelTemplate></ListBox.ItemsPanel>
                                <ListBox.ItemTemplate>'''
assert old in s
s = s.replace(old, new, 1)

old = '''                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </ScrollViewer>'''
new = '''                                </ListBox.ItemTemplate>
                            </ListBox>
                        </ScrollViewer>'''
assert old in s
s = s.replace(old, new, 1)

old = '''                                <TextBlock Text="{Binding SelectedProject.UpdatedAt, StringFormat='Actualizado: {0}'}" Style="{StaticResource Caption}" Margin="0,4,0,0" />
                                <Button Content="INSTALAR" Style="{StaticResource PrimaryButton}" Margin="0,16,0,0"
                                        Command="{Binding InstallCommand}" CommandParameter="{Binding SelectedProject}" />
'''
new = '''                                <TextBlock Text="{Binding SelectedProject.UpdatedAt, StringFormat='Actualizado: {0}'}" Style="{StaticResource Caption}" Margin="0,4,0,0" />
                                <Border Style="{StaticResource Divider}" Margin="0,14,0,0" />
                                <TextBlock Text="VERSIÓN DEL MOD" Style="{StaticResource Kicker}" Margin="0,12,0,0" />
                                <ComboBox ItemsSource="{Binding ModVersions}" SelectedItem="{Binding SelectedModVersion}"
                                          Margin="0,8,0,0" HorizontalAlignment="Stretch" MinWidth="220">
                                    <ComboBox.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal">
                                                <TextBlock Text="{Binding VersionNumber}" FontWeight="SemiBold" />
                                                <TextBlock Text="{Binding DatePublished, StringFormat='  ·  {0}'}"
                                                           Style="{StaticResource Caption}" />
                                            </StackPanel>
                                        </DataTemplate>
                                    </ComboBox.ItemTemplate>
                                </ComboBox>
                                <TextBlock Text="Cargando versiones…" Style="{StaticResource Caption}" Margin="0,6,0,0"
                                           Visibility="{Binding IsLoadingVersions, Converter={StaticResource BoolToVisibility}}" />
                                <TextBlock Text="No hay versiones compatibles con este perfil." Style="{StaticResource Caption}"
                                           Margin="0,6,0,0"
                                           Visibility="{Binding HasModVersions, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}" />
                                <TextBlock Text="{Binding TargetFolder, StringFormat='Se instalará en: {0}'}"
                                           Style="{StaticResource Caption}" Margin="0,10,0,0" TextTrimming="CharacterEllipsis"
                                           ToolTip="{Binding TargetFolder}" />
                                <Button Content="INSTALAR VERSIÓN SELECCIONADA" Style="{StaticResource PrimaryButton}"
                                        Margin="0,16,0,0" Command="{Binding InstallSelectedVersionCommand}" />
                                <Button Content="Instalar última compatible" Style="{StaticResource SecondaryButton}"
                                        Margin="0,8,0,0" Command="{Binding InstallCommand}"
                                        CommandParameter="{Binding SelectedProject}" />
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
