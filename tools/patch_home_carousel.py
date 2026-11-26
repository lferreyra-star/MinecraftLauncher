import io
p = 'src/XDLauncher/Views/HomeView.xaml'
s = io.open(p, encoding='utf-8-sig').read()
start = s.index('                <Border Style="{StaticResource HeroBanner}"')
end = s.index('\n                <Border Style="{StaticResource Card}"', start)
hero = '''                <Border x:Name="BannerRoot" Style="{StaticResource HeroBanner}" Height="304" Margin="0,0,0,14">
                    <Border.Background>
                        <ImageBrush ImageSource="{Binding CurrentBannerPath, Converter={StaticResource ImagePath}}"
                                    Stretch="UniformToFill" />
                    </Border.Background>
                    <Border Style="{StaticResource HeroOverlay}" />
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto" />
                            <RowDefinition Height="*" />
                            <RowDefinition Height="Auto" />
                        </Grid.RowDefinitions>

                        <StackPanel Orientation="Horizontal" Margin="24,20,24,0">
                            <Border Style="{StaticResource Badge}">
                                <TextBlock Text="DESTACADO" FontSize="9" FontWeight="Bold"
                                           Foreground="{DynamicResource AccentLightBrush}" />
                            </Border>
                            <Border Style="{StaticResource Pill}" Margin="8,0,0,0">
                                <TextBlock Text="{Binding BannerPosition}" FontSize="9.5" FontWeight="SemiBold" />
                            </Border>
                        </StackPanel>

                        <StackPanel x:Name="BannerContent" Grid.Row="2" Margin="24,0,24,22">
                            <TextBlock Text="{Binding EditionBadgeLabel}" Style="{StaticResource Kicker}" />
                            <TextBlock Text="{Binding BannerTitle}" Style="{StaticResource DisplayHero}" FontSize="38" Margin="0,5,0,0" />
                            <TextBlock Text="{Binding BannerDescription}" Style="{StaticResource BodyText}" MaxWidth="560" Margin="0,6,0,0" />
                            <StackPanel Orientation="Horizontal" Margin="0,16,0,0">
                                <Button Style="{StaticResource PrimaryButton}" Height="46" MinWidth="200"
                                        Content="{Binding MainActionLabel}" Command="{Binding MainActionCommand}" />
                                <Button Style="{StaticResource SecondaryButton}" Height="46" Margin="8,0,0,0"
                                        Content="Verificar" Command="{Binding VerifyCommand}"
                                        Visibility="{Binding ShowVerify, Converter={StaticResource BoolToVisibility}}" />
                            </StackPanel>
                        </StackPanel>

                        <Grid Grid.Row="1" VerticalAlignment="Top" HorizontalAlignment="Right" Margin="0,18,22,0">
                            <StackPanel Orientation="Horizontal">
                                <Button Style="{StaticResource IconButton}" Content="&#xE76B;" Width="30" Height="30"
                                        Command="{Binding PreviousBannerCommand}" ToolTip=" Banner anterior" />
                                <Button Style="{StaticResource IconButton}" Content="&#xE76C;" Width="30" Height="30"
                                        Command="{Binding NextBannerCommand}" ToolTip="Siguiente banner" />
                            </StackPanel>
                        </Grid>

                        <ItemsControl Grid.Row="2" ItemsSource="{Binding BannerTitles}" HorizontalAlignment="Right"
                                      Margin="0,0,22,26" VerticalAlignment="Bottom">
                            <ItemsControl.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal" /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Button Style="{StaticResource IconButton}" Width="9" Height="9" Padding="0" Margin="2,0"
                                            ToolTip="{Binding}" Command="{Binding DataContext.SelectBannerCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                            CommandParameter="{Binding}" />
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </Grid>
                </Border>'''
s = s[:start] + hero + s[end:]
io.open(p, 'w', encoding='utf-8').write(s)
print('home hero carousel replaced')
