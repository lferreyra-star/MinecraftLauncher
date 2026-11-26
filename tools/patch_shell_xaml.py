import io

p = 'src/XDLauncher/MainWindow.xaml'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '''            <Image Source="{Binding CurrentBackgroundPath, Converter={StaticResource ImagePath}}"
                   Stretch="UniformToFill" Opacity="0.55" />''',
    '''            <Image x:Name="BackdropImage" RenderTransformOrigin="0.5,0.5"
                   Source="{Binding CurrentBackgroundPath, Converter={StaticResource ImagePath}}"
                   Stretch="UniformToFill" Opacity="0.55">
                <Image.RenderTransform>
                    <ScaleTransform x:Name="BackdropScale" ScaleX="1" ScaleY="1" />
                </Image.RenderTransform>
            </Image>''',
    1,
)

s = s.replace(
    '''                    <Border Grid.Row="1" Background="{DynamicResource SurfaceStrongBrush}" BorderBrush="{DynamicResource BorderSoftBrush}"
                            BorderThickness="1" CornerRadius="14" Padding="0">
                        <ListBox x:Name="NavList" ItemContainerStyle="{StaticResource NavRailItem}" ItemsSource="{Binding Source={StaticResource GroupedNavigation}}"
                                 SelectedItem="{Binding CurrentNavigation, Mode=TwoWay}"
                                 Margin="10,8,10,6" BorderThickness="0"
                                 ScrollViewer.VerticalScrollBarVisibility="Auto"
                                 ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                            <ListBox.GroupStyle>''',
    '''                    <Border Grid.Row="1" Background="{DynamicResource SurfaceStrongBrush}" BorderBrush="{DynamicResource BorderSoftBrush}"
                            BorderThickness="1" CornerRadius="14" Padding="0">
                        <Grid x:Name="NavStage">
                            <Border x:Name="NavIndicator" Width="3" Height="40" Margin="12,8,0,0"
                                    HorizontalAlignment="Left" VerticalAlignment="Top" CornerRadius="2"
                                    Background="{DynamicResource AccentBrush}" Effect="{StaticResource AccentGlowEffect}"
                                    IsHitTestVisible="False" Opacity="0" Panel.ZIndex="4">
                                <Border.RenderTransform><TranslateTransform x:Name="NavIndicatorTranslate" Y="0" /></Border.RenderTransform>
                            </Border>
                            <ListBox x:Name="NavList" ItemContainerStyle="{StaticResource NavRailItem}" ItemsSource="{Binding Source={StaticResource GroupedNavigation}}"
                                     SelectedItem="{Binding CurrentNavigation, Mode=TwoWay}"
                                     Margin="10,8,10,6" BorderThickness="0"
                                     ScrollViewer.VerticalScrollBarVisibility="Auto"
                                     ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                            <ListBox.GroupStyle>''',
    1,
)

s = s.replace(
    '''                            </ListBox.GroupStyle>
                        </ListBox>
                    </Border>''',
    '''                            </ListBox.GroupStyle>
                            </ListBox>
                        </Grid>
                    </Border>''',
    1,
)

s = s.replace(
    '''                        <StackPanel Grid.Column="3" VerticalAlignment="Center" Margin="0,0,18,0">''',
    '''                        <StackPanel x:Name="HeaderPanel" Grid.Column="3" VerticalAlignment="Center" Margin="0,0,18,0"
                                    RenderTransformOrigin="0,0.5">''',
    1,
)

old_page = '''                <ContentControl x:Name="PageHost" Grid.Row="1" Content="{Binding CurrentPage}" Margin="24,24,24,14" RenderTransformOrigin="0.5,0.5">
                    <ContentControl.RenderTransform>
                        <TransformGroup>
                            <ScaleTransform ScaleX="1" ScaleY="1" />
                            <TranslateTransform Y="0" />
                        </TransformGroup>
                    </ContentControl.RenderTransform>
                </ContentControl>'''
new_page = '''                <Grid x:Name="PageStage" Grid.Row="1" Margin="24,24,24,14">
                    <ContentControl x:Name="PreviousPageHost" IsHitTestVisible="False" Opacity="0" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                    <ContentControl x:Name="PageHost" RenderTransformOrigin="0.5,0.5">
                        <ContentControl.RenderTransform>
                            <TransformGroup>
                                <ScaleTransform ScaleX="1" ScaleY="1" />
                                <TranslateTransform X="0" Y="0" />
                            </TransformGroup>
                        </ContentControl.RenderTransform>
                    </ContentControl>
                </Grid>'''
assert old_page in s
s = s.replace(old_page, new_page, 1)

io.open(p, 'w', encoding='utf-8').write(s)
