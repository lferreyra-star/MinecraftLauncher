import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()

def replace_panel(key, next_key, new):
    global s
    start = s.index('            <Border Style="{StaticResource %s}" Margin="0,14,0,0">' % key)
    end = s.index('            <Border Style="{StaticResource %s}" Margin="0,14,0,0">' % next_key, start)
    s = s[:start] + new + s[end:]

replace_panel('SettingsPanelDownloads', 'SettingsPanelPerformance', '''            <Border Style="{StaticResource SettingsPanelDownloads}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="DESCARGAS" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Gestiona la caché y abre el centro de descargas del launcher."
                               Style="{StaticResource Caption}" Margin="0,6,0,0" />
                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,16,0,0">
                        <StackPanel>
                            <WrapPanel>
                                <Button Content="Abrir carpeta" Style="{StaticResource SecondaryButton}" Margin="0,0,8,0"
                                        Command="{Binding OpenDownloadsFolderCommand}" />
                                <Button Content="Abrir centro de descargas" Style="{StaticResource PrimaryButton}"
                                        Command="{Binding DataContext.NavigateCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="downloads" />
                            </WrapPanel>
                            <Border Style="{StaticResource Divider}" />
                            <TextBlock Text="Cache / Downloads y progreso en tiempo real" Style="{StaticResource Caption}" />
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

''')

replace_panel('SettingsPanelSystem', 'SettingsPanelDiagnostics', '''            <Border Style="{StaticResource SettingsPanelSystem}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="SISTEMA" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Accesos directos a integración, registros y datos locales."
                               Style="{StaticResource Caption}" Margin="0,6,0,0" />
                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,16,0,0">
                        <StackPanel>
                            <WrapPanel>
                                <Button Content="Iniciar con Windows" Style="{StaticResource SecondaryButton}" Margin="0,0,8,8"
                                        Command="{Binding State.OpenStartupAppsCommand}" />
                                <Button Content="Abrir registros" Style="{StaticResource SecondaryButton}" Margin="0,0,8,8"
                                        Command="{Binding State.OpenLogsFolderCommand}" />
                                <Button Content="Datos del launcher" Style="{StaticResource SecondaryButton}" Margin="0,0,8,8"
                                        Command="{Binding OpenDataFolderCommand}" />
                            </WrapPanel>
                            <Border Style="{StaticResource Divider}" />
                            <WrapPanel>
                                <Button Content="Limpiar caché" Style="{StaticResource DangerButton}" Margin="0,0,8,0"
                                        Command="{Binding State.ClearCacheCommand}" />
                                <Button Content="Actualizar manualmente" Style="{StaticResource SecondaryButton}"
                                        Command="{Binding State.RefreshSystemInfoCommand}" />
                            </WrapPanel>
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

''')

replace_panel('SettingsPanelDiagnostics', 'SettingsPanelPrivacy', '''            <Border Style="{StaticResource SettingsPanelDiagnostics}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="DIAGNÓSTICO" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="Copia un resumen del equipo para soporte sin exponer tus credenciales."
                               Style="{StaticResource Caption}" Margin="0,6,0,0" />
                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,16,0,0">
                        <StackPanel>
                            <WrapPanel>
                                <Button Content="Copiar diagnóstico" Style="{StaticResource SecondaryButton}" Margin="0,0,8,0"
                                        Command="{Binding CopyDiagnosticsCommand}" />
                                <Button Content="Actualizar información" Style="{StaticResource SecondaryButton}"
                                        Command="{Binding RefreshInfoCommand}" />
                            </WrapPanel>
                            <Border Style="{StaticResource Divider}" />
                            <TextBlock Text="El informe incluye versión del launcher, sistema operativo, Java y estado de los servicios."
                                       Style="{StaticResource Caption}" TextWrapping="Wrap" />
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

''')

start = s.index('            <Border Style="{StaticResource SettingsPanelPrivacy}" Margin="0,14,0,0">')
end = s.index('            <Border Style="{StaticResource SettingsPanelAbout}" Margin="0,14,0,0">', start)
new = '''            <Border Style="{StaticResource SettingsPanelPrivacy}" Margin="0,14,0,0">
                <StackPanel>
                    <TextBlock Text="PRIVACIDAD Y SEGURIDAD" Style="{StaticResource SectionTitle}" />
                    <TextBlock Text="No se recopilan datos personales. Las contraseñas no se guardan en disco y los tokens se protegen con DPAPI para el usuario actual. Firebase y la cuenta de XD Launcher conservan su flujo de autenticación original."
                               Style="{StaticResource BodyText}" Margin="0,6,0,0" />
                    <Border Style="{StaticResource SettingsSubCard}" Margin="0,16,0,0">
                        <StackPanel>
                            <Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
                                <Border Width="40" Height="40" CornerRadius="12" Background="{DynamicResource AccentSoftBrush}"
                                        BorderBrush="{DynamicResource AccentBrush}" BorderThickness="1">
                                    <TextBlock Text="&#xE72E;" FontFamily="{StaticResource IconFont}" FontSize="17"
                                               Foreground="{DynamicResource AccentLightBrush}"
                                               HorizontalAlignment="Center" VerticalAlignment="Center" />
                                </Border>
                                <StackPanel Grid.Column="1" Margin="12,0,0,0">
                                    <TextBlock Text="Almacenamiento local" Style="{StaticResource BodyText}" />
                                    <TextBlock Text="Sesión, configuración y biblioteca permanecen en tu equipo."
                                               Style="{StaticResource Caption}" Margin="0,4,0,0" />
                                </StackPanel>
                            </Grid>
                            <Border Style="{StaticResource Divider}" />
                            <Button Content="Datos del launcher" Style="{StaticResource SecondaryButton}"
                                    HorizontalAlignment="Left" Command="{Binding OpenDataFolderCommand}" />
                        </StackPanel>
                    </Border>
                </StackPanel>
            </Border>

'''
s = s[:start] + new + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
