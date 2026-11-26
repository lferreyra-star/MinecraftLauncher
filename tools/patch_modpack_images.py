import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Services/ModInstallService.cs'
s = read(p)
old = '    public string Workspace { get; set; } = string.Empty;\n    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.Now;\n}\n\npublic sealed class InstalledMod'
new = '    public string Workspace { get; set; } = string.Empty;\n    public string? IconUrl { get; set; }\n    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.Now;\n}\n\npublic sealed class InstalledMod'
assert old in s
s = s.replace(old, new, 1)
old = '''            GameDirectory = gameDirectory,
            Workspace = workspace.Kind.ToString(),
            InstalledAt = DateTimeOffset.Now
        };'''
new = '''            GameDirectory = gameDirectory,
            Workspace = workspace.Kind.ToString(),
            IconUrl = project.IconUrl,
            InstalledAt = DateTimeOffset.Now
        };'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/Views/ModsCenterView.xaml'
s = read(p)
old = '''                                                    <Border Style="{StaticResource Pill}" HorizontalAlignment="Left"><TextBlock Text="MODPACK" FontSize="9" FontWeight="Bold" Foreground="{DynamicResource AccentBrush}" /></Border>
                                                    <StackPanel Grid.Row="1" Margin="0,12,0,0">'''
new = '''                                                    <Grid>
                                                        <Border Style="{StaticResource Pill}" HorizontalAlignment="Left">
                                                            <TextBlock Text="MODPACK" FontSize="9" FontWeight="Bold" Foreground="{DynamicResource AccentBrush}" />
                                                        </Border>
                                                        <Border Width="48" Height="48" CornerRadius="13" HorizontalAlignment="Right"
                                                                Background="{DynamicResource ChipBrush}" ClipToBounds="True">
                                                            <Grid>
                                                                <TextBlock Text="&#xE7B8;" FontFamily="{StaticResource IconFont}" FontSize="18"
                                                                           Foreground="{DynamicResource AccentBrush}"
                                                                           HorizontalAlignment="Center" VerticalAlignment="Center" />
                                                                <Border Visibility="{Binding IconUrl, Converter={StaticResource NullToVisibility}}">
                                                                    <Border.Background>
                                                                        <ImageBrush ImageSource="{Binding IconUrl}" Stretch="UniformToFill" />
                                                                    </Border.Background>
                                                                </Border>
                                                            </Grid>
                                                        </Border>
                                                    </Grid>
                                                    <StackPanel Grid.Row="1" Margin="0,12,0,0">'''
assert old in s
s = s.replace(old, new, 1)

old = '''                                            <Border Style="{StaticResource InnerCard}" Margin="0,0,0,8" Padding="13">
                                                <Grid>
                                                    <StackPanel>
                                                        <TextBlock Text="{Binding Name}" FontWeight="SemiBold" FontSize="13" />
                                                        <TextBlock Text="{Binding VersionLabel, StringFormat='v{0}'}" Style="{StaticResource Caption}" Margin="0,3,0,0" />
                                                        <TextBlock Text="{Binding MinecraftVersion, StringFormat='Minecraft {0}'}" Style="{StaticResource Caption}" />
                                                    </StackPanel>
                                                    <Button Content="Abrir carpeta" Style="{StaticResource TinyButton}" HorizontalAlignment="Right"
                                                            Command="{Binding DataContext.OpenModpackFolderCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                            CommandParameter="{Binding}" />
                                                </Grid>
                                            </Border>'''
new = '''                                            <Border Style="{StaticResource InnerCard}" Margin="0,0,0,8" Padding="13">
                                                <Grid>
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width="Auto" />
                                                        <ColumnDefinition Width="*" />
                                                        <ColumnDefinition Width="Auto" />
                                                    </Grid.ColumnDefinitions>
                                                    <Border Width="38" Height="38" CornerRadius="11" Background="{DynamicResource AccentSoftBrush}" ClipToBounds="True">
                                                        <Grid>
                                                            <TextBlock Text="&#xE7B8;" FontFamily="{StaticResource IconFont}" FontSize="16"
                                                                       Foreground="{DynamicResource AccentBrush}"
                                                                       HorizontalAlignment="Center" VerticalAlignment="Center" />
                                                            <Border Visibility="{Binding IconUrl, Converter={StaticResource NullToVisibility}}">
                                                                <Border.Background>
                                                                    <ImageBrush ImageSource="{Binding IconUrl}" Stretch="UniformToFill" />
                                                                </Border.Background>
                                                            </Border>
                                                        </Grid>
                                                    </Border>
                                                    <StackPanel Grid.Column="1" Margin="12,0,0,0">
                                                        <TextBlock Text="{Binding Name}" FontWeight="SemiBold" FontSize="13" />
                                                        <TextBlock Text="{Binding VersionLabel, StringFormat='v{0}'}" Style="{StaticResource Caption}" Margin="0,3,0,0" />
                                                        <TextBlock Text="{Binding MinecraftVersion, StringFormat='Minecraft {0}'}" Style="{StaticResource Caption}" />
                                                    </StackPanel>
                                                    <Button Grid.Column="2" Content="Abrir carpeta" Style="{StaticResource TinyButton}" VerticalAlignment="Center"
                                                            Command="{Binding DataContext.OpenModpackFolderCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                            CommandParameter="{Binding}" />
                                                </Grid>
                                            </Border>'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
