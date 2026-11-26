import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()
start = s.index('            <Border Style="{StaticResource Card}" Margin="0,0,0,14" Padding="12">')
end = s.index('            <Grid x:Name="SettingsContentRoot">', start)
new = '''            <Border CornerRadius="18" Padding="12" Margin="0,0,0,14" BorderThickness="0"
                    Background="{DynamicResource SurfaceBrush}" Effect="{StaticResource PanelShadow}">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="32" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="32" />
                    </Grid.ColumnDefinitions>

                    <Button x:Name="CategoryLeft" Grid.Column="0" Content="&#xE76B;" ToolTip="Categorías anteriores"
                            Style="{StaticResource GhostButton}" Width="30" Height="44" Padding="0" FontSize="14"
                            FontFamily="{StaticResource IconFont}" Foreground="{DynamicResource AccentLightBrush}"
                            Click="CategoryLeft_Click" VerticalAlignment="Center" />

                    <ListBox x:Name="CategoryList" Grid.Column="1"
                             ItemsSource="{Binding Categories}" SelectedValuePath="Key"
                             SelectedValue="{Binding SelectedCategory, Mode=TwoWay}" BorderThickness="0"
                             Background="Transparent" ScrollViewer.HorizontalScrollBarVisibility="Hidden"
                             ScrollViewer.VerticalScrollBarVisibility="Disabled">
                        <ListBox.ItemsPanel>
                            <ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate>
                        </ListBox.ItemsPanel>
                        <ListBox.ItemContainerStyle>
                            <Style TargetType="ListBoxItem">
                                <Setter Property="Margin" Value="0,0,8,0" />
                                <Setter Property="Cursor" Value="Hand" />
                                <Setter Property="Template">
                                    <Setter.Value>
                                        <ControlTemplate TargetType="ListBoxItem">
                                            <Border x:Name="Chrome" Width="164" Height="58" CornerRadius="14"
                                                    Background="{DynamicResource ChipBrush}" BorderThickness="1"
                                                    BorderBrush="{DynamicResource BorderSoftBrush}" Padding="10,8">
                                                <Grid>
                                                    <Border x:Name="AccentBar" Width="3" Height="28" CornerRadius="2"
                                                            HorizontalAlignment="Left" VerticalAlignment="Center"
                                                            Background="{DynamicResource AccentBrush}" Opacity="0" />
                                                    <TextBlock x:Name="Glyph" Text="{Binding Glyph}" FontFamily="{StaticResource IconFont}"
                                                               FontSize="16" Foreground="{DynamicResource TextMutedBrush}"
                                                               HorizontalAlignment="Left" VerticalAlignment="Center" Margin="8,0,0,0" />
                                                    <StackPanel Margin="32,0,0,0" VerticalAlignment="Center">
                                                        <TextBlock Text="{Binding Title}" FontSize="11.5" FontWeight="SemiBold"
                                                                   Foreground="{DynamicResource TextSecondaryBrush}" />
                                                        <TextBlock Text="{Binding Description}" FontSize="9" Margin="0,2,0,0"
                                                                   Foreground="{DynamicResource TextMutedBrush}" TextTrimming="CharacterEllipsis" />
                                                    </StackPanel>
                                                </Grid>
                                                <Border.RenderTransformOrigin><Point X="0.5" Y="0.5" /></Border.RenderTransformOrigin>
                                                <Border.RenderTransform><ScaleTransform x:Name="ChipScale" ScaleX="1" ScaleY="1" /></Border.RenderTransform>
                                            </Border>
                                            <ControlTemplate.Triggers>
                                                <Trigger Property="IsMouseOver" Value="True">
                                                    <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                    <Setter TargetName="Glyph" Property="Foreground" Value="{DynamicResource AccentLightBrush}" />
                                                    <Trigger.EnterActions>
                                                        <BeginStoryboard>
                                                            <Storyboard>
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleX"
                                                                                 To="1.03" Duration="0:0:0.16">
                                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                                </DoubleAnimation>
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleY"
                                                                                 To="1.03" Duration="0:0:0.16">
                                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                                </DoubleAnimation>
                                                            </Storyboard>
                                                        </BeginStoryboard>
                                                    </Trigger.EnterActions>
                                                    <Trigger.ExitActions>
                                                        <BeginStoryboard>
                                                            <Storyboard>
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleX" To="1" Duration="0:0:0.16" />
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleY" To="1" Duration="0:0:0.16" />
                                                            </Storyboard>
                                                        </BeginStoryboard>
                                                    </Trigger.ExitActions>
                                                </Trigger>
                                                <Trigger Property="IsSelected" Value="True">
                                                    <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentSoftBrush}" />
                                                    <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentBrush}" />
                                                    <Setter TargetName="AccentBar" Property="Opacity" Value="1" />
                                                    <Setter TargetName="Glyph" Property="Foreground" Value="{DynamicResource AccentLightBrush}" />
                                                    <Trigger.EnterActions>
                                                        <BeginStoryboard>
                                                            <Storyboard>
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleX"
                                                                                 From="0.96" To="1" Duration="0:0:0.18">
                                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                                </DoubleAnimation>
                                                                <DoubleAnimation Storyboard.TargetName="ChipScale" Storyboard.TargetProperty="ScaleY"
                                                                                 From="0.96" To="1" Duration="0:0:0.18">
                                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                                </DoubleAnimation>
                                                            </Storyboard>
                                                        </BeginStoryboard>
                                                    </Trigger.EnterActions>
                                                </Trigger>
                                            </ControlTemplate.Triggers>
                                        </ControlTemplate>
                                    </Setter.Value>
                                </Setter>
                            </Style>
                        </ListBox.ItemContainerStyle>
                    </ListBox>

                    <Button x:Name="CategoryRight" Grid.Column="2" Content="&#xE76C;" ToolTip="Más categorías"
                            Style="{StaticResource GhostButton}" Width="30" Height="44" Padding="0" FontSize="14"
                            FontFamily="{StaticResource IconFont}" Foreground="{DynamicResource AccentLightBrush}"
                            Click="CategoryRight_Click" VerticalAlignment="Center" />
                </Grid>
            </Border>

'''
s = s[:start] + new + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
