import io

p = 'src/XDLauncher/MainWindow.xaml'
s = io.open(p, encoding='utf-8-sig').read()

s = s.replace(
    '''                <Grid.RowDefinitions>
                    <RowDefinition Height="66" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>''',
    '''                <Grid.RowDefinitions>
                    <RowDefinition Height="66" />
                    <RowDefinition Height="44" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>''',
    1,
)

tabs = '''                <Border x:Name="TopTabsBar" Grid.Row="1" Margin="0,0,0,4"
                        Visibility="{Binding IsFullscreen, Converter={StaticResource BoolToVisibility}, ConverterParameter=invert}"
                        Background="{DynamicResource SurfaceRaisedBrush}" BorderBrush="{DynamicResource BorderSoftBrush}"
                        BorderThickness="0,0,0,1" Padding="16,0,16,0">
                    <ListBox ItemsSource="{Binding QuickNavigation}" SelectedValuePath="Key"
                             SelectedValue="{Binding CurrentNavigationKey, Mode=TwoWay}"
                             Background="Transparent" BorderThickness="0"
                             ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                             ScrollViewer.VerticalScrollBarVisibility="Disabled">
                        <ListBox.ItemsPanel>
                            <ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate>
                        </ListBox.ItemsPanel>
                        <ListBox.ItemContainerStyle>
                            <Style TargetType="ListBoxItem">
                                <Setter Property="Margin" Value="0,0,6,0" />
                                <Setter Property="Padding" Value="0" />
                                <Setter Property="Cursor" Value="Hand" />
                                <Setter Property="Template">
                                    <Setter.Value>
                                        <ControlTemplate TargetType="ListBoxItem">
                                            <Border x:Name="Chrome" CornerRadius="10" Padding="14,0" Background="Transparent"
                                                    BorderBrush="Transparent" BorderThickness="1" RenderTransformOrigin="0.5,0.5">
                                                <Border.RenderTransform><ScaleTransform x:Name="TabScale" ScaleX="1" ScaleY="1" /></Border.RenderTransform>
                                                <Grid>
                                                    <TextBlock x:Name="TabText" Text="{Binding Title}" FontSize="11.5" FontWeight="SemiBold"
                                                               Foreground="{DynamicResource TextMutedBrush}" VerticalAlignment="Center" />
                                                    <Border x:Name="TabUnderline" Height="2" CornerRadius="1" VerticalAlignment="Bottom"
                                                            Margin="0,0,0,3" Background="{DynamicResource AccentBrush}" Opacity="0" />
                                                </Grid>
                                            </Border>
                                            <ControlTemplate.Triggers>
                                                <Trigger Property="IsMouseOver" Value="True">
                                                    <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource RowHoverBrush}" />
                                                    <Setter TargetName="TabText" Property="Foreground" Value="{DynamicResource TextPrimaryBrush}" />
                                                </Trigger>
                                                <Trigger Property="IsSelected" Value="True">
                                                    <Setter TargetName="Chrome" Property="Background" Value="{DynamicResource AccentSoftBrush}" />
                                                    <Setter TargetName="Chrome" Property="BorderBrush" Value="{DynamicResource AccentSoftBrush}" />
                                                    <Setter TargetName="TabText" Property="Foreground" Value="{DynamicResource AccentLightBrush}" />
                                                    <Setter TargetName="TabUnderline" Property="Opacity" Value="1" />
                                                    <Trigger.EnterActions>
                                                        <BeginStoryboard>
                                                            <Storyboard>
                                                                <DoubleAnimation Storyboard.TargetName="TabScale" Storyboard.TargetProperty="ScaleX" From="0.92" To="1" Duration="0:0:0.2">
                                                                    <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut" /></DoubleAnimation.EasingFunction>
                                                                </DoubleAnimation>
                                                                <DoubleAnimation Storyboard.TargetName="TabScale" Storyboard.TargetProperty="ScaleY" From="0.92" To="1" Duration="0:0:0.2">
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
                </Border>

'''
s = s.replace('                <Grid x:Name="PageStage" Grid.Row="1"', tabs + '                <Grid x:Name="PageStage" Grid.Row="2"', 1)

io.open(p, 'w', encoding='utf-8').write(s)
