import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/ViewModels/ModsCenterViewModel.cs'
s = read(p)

anchor = '''    public ObservableCollection<ModrinthVersion> ModVersions { get; }
'''
addition = '''    public ObservableCollection<JavaVersionEntry> JavaVersions => _java.Versions;
    public event Action? OpenJavaRequested;
'''
assert anchor in s
s = s.replace(anchor, anchor + addition, 1)

anchor = '''    public ModrinthVersion? SelectedModVersion
    {
'''
addition = '''    public JavaVersionEntry? SelectedJavaVersion
    {
        get => _java.SelectedVersion;
        set
        {
            if (ReferenceEquals(_java.SelectedVersion, value))
                return;
            _java.SelectedVersion = value;
            OnPropertyChanged(nameof(SelectedJavaVersion));
            OnPropertyChanged(nameof(TargetFolder));
            OnPropertyChanged(nameof(CompatibilitySummary));
            ClearModVersions();
            if (value is not null && SelectedProject is not null)
                _ = LoadVersionsAsync(SelectedProject);
            _ = RefreshLibraryAsync();
        }
    }

    public bool HasJavaVersion => _java.SelectedVersion is not null;

'''
assert anchor in s
s = s.replace(anchor, addition + anchor, 1)

old = '''    private async Task InitializeAsync()
    {
        try
        {
            var versions = await _modrinth.GetGameVersionsAsync();
'''
new = '''    private async Task InitializeAsync()
    {
        try
        {
            await _java.EnsureLoadedAsync();
            await RunOnUiAsync(() =>
            {
                OnPropertyChanged(nameof(JavaVersions));
                OnPropertyChanged(nameof(HasJavaVersion));
                OnPropertyChanged(nameof(SelectedJavaVersion));
                OnPropertyChanged(nameof(TargetFolder));
            });

            var versions = await _modrinth.GetGameVersionsAsync();
'''
assert old in s
s = s.replace(old, new, 1)

old = '''        if (version is null)
        {
            _dialogs.ShowError("Centro de mods", "Selecciona una versión Java para abrir su carpeta de mods.");
            return;
        }
'''
new = '''        if (version is null)
        {
            if (_dialogs.Confirm("Centro de mods",
                    "Selecciona una versión Java para abrir su carpeta de mods. ¿Quieres ir a Minecraft Java?",
                    "Ir a Java", "Ahora no"))
                OpenJavaRequested?.Invoke();
            return;
        }
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/ViewModels/ShellViewModel.cs'
s = read(p)
old = '        Mods = new ModsCenterViewModel(Java, dialogs);\n'
new = old + '        Mods.OpenJavaRequested += () => Navigate("java");\n'
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/Views/ModsCenterView.xaml'
s = read(p)
old = '''                        <Grid>
                            <TextBlock Text="{Binding ActiveProfileLabel}" Style="{StaticResource Caption}" Foreground="{DynamicResource AccentBrush}" VerticalAlignment="Center" />
                            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                                <Button Content="Abrir carpeta mods" Style="{StaticResource GhostButton}" Command="{Binding OpenModsFolderCommand}" />
                                <Button Content="Actualizar datos" Style="{StaticResource GhostButton}" Margin="4,0,0,0" Command="{Binding RefreshCommand}" />
                            </StackPanel>
                        </Grid>
'''
new = '''                        <StackPanel>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto" />
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Text="VERSIÓN JAVA" Style="{StaticResource Kicker}" VerticalAlignment="Center" />
                                <ComboBox Grid.Column="1" Margin="12,0,12,0" MinWidth="180" MaxWidth="320"
                                          HorizontalAlignment="Left" ItemsSource="{Binding JavaVersions}"
                                          SelectedItem="{Binding SelectedJavaVersion}"
                                          AutomationProperties.Name="Versión Java para mods">
                                    <ComboBox.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal">
                                                <TextBlock Text="{Binding Id}" FontWeight="SemiBold" />
                                                <TextBlock Text=" · instalada" Style="{StaticResource Caption}"
                                                           Visibility="{Binding IsInstalled, Converter={StaticResource BoolToVisibility}}" />
                                            </StackPanel>
                                        </DataTemplate>
                                    </ComboBox.ItemTemplate>
                                </ComboBox>
                                <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                                    <Button Content="Abrir carpeta mods" Style="{StaticResource GhostButton}" Command="{Binding OpenModsFolderCommand}" />
                                    <Button Content="Actualizar datos" Style="{StaticResource GhostButton}" Margin="4,0,0,0" Command="{Binding RefreshCommand}" />
                                </StackPanel>
                            </Grid>
                            <TextBlock Text="{Binding ActiveProfileLabel}" Style="{StaticResource Caption}"
                                       Foreground="{DynamicResource AccentBrush}" Margin="0,6,0,0" />
                            <TextBlock Text="Selecciona una versión Java para ver su carpeta de mods."
                                       Style="{StaticResource Caption}" Margin="0,4,0,0"
                                       Visibility="{Binding HasJavaVersion, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}" />
                        </StackPanel>
'''
assert old in s
s = s.replace(old, new, 1)
write(p, s)
