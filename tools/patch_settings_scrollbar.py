import io
p = 'src/XDLauncher/Views/SettingsView.xaml'
s = io.open(p, encoding='utf-8-sig').read()
needle = '''        <Style x:Key="SettingsRow" TargetType="Border">
            <Setter Property="BorderBrush" Value="{DynamicResource BorderSoftBrush}" />
            <Setter Property="BorderThickness" Value="0,0,0,1" />
            <Setter Property="Padding" Value="0,0,0,12" />
            <Setter Property="Margin" Value="0,0,0,12" />
        </Style>'''
addition = needle + '''
        <Style x:Key="SettingsScrollThumb" TargetType="Thumb">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Thumb">
                        <Border x:Name="ThumbBorder" Background="{DynamicResource GlassBorderBrush}" CornerRadius="4"
                                Opacity="0.55" Margin="2" />
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ThumbBorder" Property="Opacity" Value="0.95" />
                                <Setter TargetName="ThumbBorder" Property="Background" Value="{DynamicResource AccentBrush}" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="8" />
            <Setter Property="Height" Value="8" />
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid Background="Transparent">
                            <Track x:Name="PART_Track" IsDirectionReversed="True">
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False" />
                                </Track.DecreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb Style="{StaticResource SettingsScrollThumb}" />
                                </Track.Thumb>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False" />
                                </Track.IncreaseRepeatButton>
                            </Track>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property="Orientation" Value="Horizontal">
                                <Setter Property="Width" Value="Auto" />
                                <Setter Property="Height" Value="8" />
                                <Setter TargetName="PART_Track" Property="IsDirectionReversed" Value="False" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>'''
assert needle in s
s = s.replace(needle, addition, 1)
io.open(p, 'w', encoding='utf-8').write(s)
