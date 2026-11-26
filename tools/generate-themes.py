from pathlib import Path

THEMES = [
 dict(file='EclipseDark', name='Eclipse Dark', dark=True,
  bg='#05070B', root='#03050A', title='#F2080C14', side1='#F70C131E', side2='#F7060A12',
  top1='#E60A1019', top2='#B3060A11', s1='#FF101927', s2='#FF0A121C', st1='#FF18263A', st2='#FF0E1725',
  ra1='#FF1A2940', ra2='#FF111D2D', hover='#FF1D2C43', sunken='#FF070C14', glass='#14FFFFFF',
  border='#FF24364D', borderSoft='#FF192739', borderGlow='#FF3E5C7E', hairline='#332F4A67',
  text='#FFF2F7FF', text2='#FFC2D1E3', muted='#FF8497AF', disabled='#FF5B6D82', inverse='#FF04140A',
  chip='#FF111C2B', chipStrong='#FF1A2B40', rowHover='#0FFFFFFF', rowSelected='#1A5CE07A',
  input='#FF0A121D', inputHover='#FF0E1826', inputFocus='#FF101E2E', glass2='#22FFFFFF', glassBorder='#26FFFFFF',
  scrim='#E6000000', ov1='#7004080E', ov2='#B804080E', ov3='#E604070D',
  hs1='#8A08111C', hs2='#66070E17', hs3='#08070E17', grid='#12FFFFFF',
  orbA='#5A5CE07A', orbB='#4745C8F5', orbC='#3BA78BFA',
  hero1='#FF0E1C2B', hero2='#FF0F2A27', hero3='#FF101B33', av1='#FF5CE07A', av2='#FF45C8F5',
  success='#FF4FE08B', successSoft='#263FE08B', warning='#FFFFC857', warningSoft='#26FFC857', error='#FFFF7A7A',
  errorSoft='#26FF7A7A', info='#FF67C7FF', infoSoft='#2667C7FF',
  cyan='#FF4FD6FF', violet='#FFA78BFA', gold='#FFF2C66D', cyanS='#1F4FD6FF', violetS='#1FA78BFA', goldS='#1FF2C66D',
  accent='#FF5CE07A', accentStrong='#FF35C25B', accentLight='#FF9CF2AC', accentSoft='#1F5CE07A', accentText='#FF04140A'),

 dict(file='Aurora', name='Aurora', dark=True,
  bg='#040B14', root='#02070D', title='#F207131A', side1='#F7091E24', side2='#F704111A',
  top1='#E6081B23', top2='#B305111A', s1='#FF0C1F2A', s2='#FF07141D', st1='#FF123242', st2='#FF0B1D29',
  ra1='#FF144054', ra2='#FF0D2634', hover='#FF174A5E', sunken='#FF050F17', glass='#16E6FFFA',
  border='#FF1D4A5C', borderSoft='#FF14313F', borderGlow='#FF36E0C8', hairline='#332F6C80',
  text='#FFF0FCFF', text2='#FFC0E9E6', muted='#FF83B4B4', disabled='#FF547F84', inverse='#FF04140A',
  chip='#FF0D2733', chipStrong='#FF123A49', rowHover='#12E6FFFA', rowSelected='#1A37E0C8',
  input='#FF071820', inputHover='#FF0A222E', inputFocus='#FF0D2B38', glass2='#24E6FFFA', glassBorder='#2EE6FFFA',
  scrim='#E6000000', ov1='#7003111C', ov2='#B803111C', ov3='#E6030A12',
  hs1='#8A081E28', hs2='#66061721', hs3='#08061016', grid='#14E6FFFA',
  orbA='#5A37E0C8', orbB='#474FD6FF', orbC='#3B9A7BFF',
  hero1='#FF0B2632', hero2='#FF0D3A3A', hero3='#FF101C38', av1='#FF37E0C8', av2='#FF4FD6FF',
  success='#FF55E8A8', successSoft='#2655E8A8', warning='#FFFFD36B', warningSoft='#26FFD36B', error='#FFFF7A94',
  errorSoft='#26FF7A94', info='#FF63D8FF', infoSoft='#2663D8FF',
  cyan='#FF63F0FF', violet='#FF9A8CFF', gold='#FFF6D06B', cyanS='#1F63F0FF', violetS='#1F9A8CFF', goldS='#1FF6D06B',
  accent='#FF37E0C8', accentStrong='#FF1FB99E', accentLight='#FF9CF7E6', accentSoft='#1F37E0C8', accentText='#FF04140A'),

 dict(file='Nebula', name='Nebula', dark=True,
  bg='#0A0614', root='#060310', title='#F2120A20', side1='#F7181030', side2='#F7120A22',
  top1='#E6160F26', top2='#B6100A1C', s1='#FF17102A', s2='#FF0D0818', st1='#FF241744', st2='#FF160F30',
  ra1='#FF301F57', ra2='#FF1E133B', hover='#FF3A265F', sunken='#FF0A0616', glass='#18E0C8FF',
  border='#FF3B2A63', borderSoft='#FF281C48', borderGlow='#FFB08CFF', hairline='#335B3D8A',
  text='#FFF7F1FF', text2='#FFD8C8F5', muted='#FF9E8DC4', disabled='#FF6B5D8F', inverse='#FF150620',
  chip='#FF1C1336', chipStrong='#FF2B1C4C', rowHover='#14E0C8FF', rowSelected='#1FA78BFA',
  input='#FF100A22', inputHover='#FF170F2E', inputFocus='#FF1C1239', glass2='#26E0C8FF', glassBorder='#2EE0C8FF',
  scrim='#E6000000', ov1='#700A0614', ov2='#B80A0614', ov3='#E6060310',
  hs1='#8A160E28', hs2='#B2100A1F', hs3='#08080414', grid='#14D8C8FF',
  orbA='#5AA78BFA', orbB='#47F472B6', orbC='#3B8B5CF6',
  hero1='#FF1A1032', hero2='#FF2A1748', hero3='#FF12102C', av1='#FFA78BFA', av2='#FFF472B6',
  success='#FF58E6A8', successSoft='#2658E6A8', warning='#FFFFC46B', warningSoft='#26FFC46B', error='#FFFF7A9E',
  errorSoft='#26FF7A9E', info='#FF8CB6FF', infoSoft='#268CB6FF',
  cyan='#FF7CE8FF', violet='#FFBE9CFF', gold='#FFF6C46B', cyanS='#1F7CE8FF', violetS='#1FBE9CFF', goldS='#1FF6C46B',
  accent='#FFA78BFA', accentStrong='#FF8258E6', accentLight='#FFDCC8FF', accentSoft='#1FA78BFA', accentText='#FF150620'),

 dict(file='Cyberpunk', name='Cyberpunk', dark=True,
  bg='#07040E', root='#040209', title='#F20B0714', side1='#F7120A20', side2='#F70A0512',
  top1='#E6120A1C', top2='#B60A0514', s1='#FF140C24', s2='#FF0A0614', st1='#FF201038', st2='#FF140824',
  ra1='#FF2B1248', ra2='#FF1B0C32', hover='#FF3A165C', sunken='#FF060310', glass='#18FF4FD8',
  border='#FF482068', borderSoft='#FF30154A', borderGlow='#FFFF4FD8', hairline='#337A2FA8',
  text='#FFF7F2FF', text2='#FFE0CFF5', muted='#FFA694C7', disabled='#FF715F93', inverse='#FF12041A',
  chip='#FF1D1033', chipStrong='#FF2C1348', rowHover='#12FF4FD8', rowSelected='#1FFF4FD8',
  input='#FF0F0820', inputHover='#FF160B2C', inputFocus='#FF1C0E38', glass2='#26FF4FD8', glassBorder='#2EFF4FD8',
  scrim='#E6000000', ov1='#7007040E', ov2='#B807040E', ov3='#E6040209',
  hs1='#8A130A20', hs2='#B20D0618', hs3='#08060310', grid='#14FF4FD8',
  orbA='#5AFF4FD8', orbB='#4745C8F5', orbC='#3BFFD36B',
  hero1='#FF1B0A32', hero2='#FF2A0C44', hero3='#FF120A28', av1='#FFFF4FD8', av2='#FF45C8F5',
  success='#FF5CE0A8', successSoft='#265CE0A8', warning='#FFFFD34F', warningSoft='#26FFD34F', error='#FFFF5C8A',
  errorSoft='#26FF5C8A', info='#FF4FF5FF', infoSoft='#264FF5FF',
  cyan='#FF4FF5FF', violet='#FFFF4FD8', gold='#FFFFD34F', cyanS='#1F4FF5FF', violetS='#1FFF4FD8', goldS='#1FFFD34F',
  accent='#FFFF4FD8', accentStrong='#FFE02BB4', accentLight='#FFFFB8EC', accentSoft='#1FFF4FD8', accentText='#FF12041A'),

 dict(file='Emerald', name='Emerald', dark=True,
  bg='#040A08', root='#020604', title='#F207100C', side1='#F70B1A13', side2='#F706100C',
  top1='#E6091710', top2='#B6050F0A', s1='#FF0C1A14', s2='#FF07120C', st1='#FF123026', st2='#FF0B1F18',
  ra1='#FF174330', ra2='#FF0F2B20', hover='#FF1C5238', sunken='#FF050D08', glass='#148DE5AB',
  border='#FF20513C', borderSoft='#FF173A2B', borderGlow='#FF5CE07A', hairline='#334F8A67',
  text='#FFF2FBF5', text2='#FFC2E5D2', muted='#FF84B49A', disabled='#FF5B846D', inverse='#FF04140A',
  chip='#FF10251B', chipStrong='#FF173728', rowHover='#108DE5AB', rowSelected='#1A5CE07A',
  input='#FF091710', inputHover='#FF0C2118', inputFocus='#FF0F2A1F', glass2='#228DE5AB', glassBorder='#2A8DE5AB',
  scrim='#E6000000', ov1='#70030A07', ov2='#B8030A07', ov3='#E6020604',
  hs1='#8A081310', hs2='#66060F0B', hs3='#08060308', grid='#148DE5AB',
  orbA='#5A5CE07A', orbB='#4745C8F5', orbC='#3B34D399',
  hero1='#FF0C2018', hero2='#FF0F2E20', hero3='#FF101B14', av1='#FF5CE07A', av2='#FF34D399',
  success='#FF4FE08B', successSoft='#263FE08B', warning='#FFFFC857', warningSoft='#26FFC857', error='#FFFF7A7A',
  errorSoft='#26FF7A7A', info='#FF67C7FF', infoSoft='#2667C7FF',
  cyan='#FF5CE5C8', violet='#FFA7F0C8', gold='#FFF2D66D', cyanS='#1F5CE5C8', violetS='#1FA7F0C8', goldS='#1FF2D66D',
  accent='#FF5CE07A', accentStrong='#FF35C25B', accentLight='#FF9CF2AC', accentSoft='#1F5CE07A', accentText='#FF04140A'),

 dict(file='Ocean', name='Ocean', dark=True,
  bg='#040812', root='#02050B', title='#F2080C16', side1='#F70A1422', side2='#F7060D18',
  top1='#E608121E', top2='#B6040B15', s1='#FF0B1828', s2='#FF06101C', st1='#FF112940', st2='#FF0A1A2E',
  ra1='#FF163656', ra2='#FF0E2339', hover='#FF1A466B', sunken='#FF040A12', glass='#1467C7FF',
  border='#FF214466', borderSoft='#FF183349', borderGlow='#FF4F8DFD', hairline='#334F8AC2',
  text='#FFF2F8FF', text2='#FFC2D9EF', muted='#FF849EBF', disabled='#FF5B7291', inverse='#FF041020',
  chip='#FF102036', chipStrong='#FF173049', rowHover='#1067C7FF', rowSelected='#1A4F8DFD',
  input='#FF081522', inputHover='#FF0B1B2E', inputFocus='#FF0E2238', glass2='#2267C7FF', glassBorder='#2A67C7FF',
  scrim='#E6000000', ov1='#7004080F', ov2='#B804080F', ov3='#E602050B',
  hs1='#8A08111C', hs2='#66060D15', hs3='#08060A12', grid='#1467C7FF',
  orbA='#5A4F8DFD', orbB='#4738BDF8', orbC='#3B4FD6FF',
  hero1='#FF0C1B30', hero2='#FF0D2740', hero3='#FF101A30', av1='#FF4F8DFD', av2='#FF38BDF8',
  success='#FF4FE0A8', successSoft='#264FE0A8', warning='#FFFFC857', warningSoft='#26FFC857', error='#FFFF7A7A',
  errorSoft='#26FF7A7A', info='#FF67C7FF', infoSoft='#2667C7FF',
  cyan='#FF67E8FF', violet='#FFA7B8FF', gold='#FFF2D66D', cyanS='#1F67E8FF', violetS='#1FA7B8FF', goldS='#1FF2D66D',
  accent='#FF4F8DFD', accentStrong='#FF2F6FDD', accentLight='#FFA8C9FF', accentSoft='#1F4F8DFD', accentText='#FF041020'),

 dict(file='Crimson', name='Crimson', dark=True,
  bg='#0D0406', root='#080203', title='#F21A070B', side1='#F7220B11', side2='#F718070B',
  top1='#E61A0A0F', top2='#B612070B', s1='#FF1A0B10', s2='#FF100609', st1='#FF2C1119', st2='#FF1C0B12',
  ra1='#FF3D1721', ra2='#FF28101A', hover='#FF501F2C', sunken='#FF0A0305', glass='#18F87171',
  border='#FF52202E', borderSoft='#FF3A1622', borderGlow='#FFEF4444', hairline='#33A84F5F',
  text='#FFFBF2F2', text2='#FFEAC8CB', muted='#FFB89097', disabled='#FF8A646C', inverse='#FF1A0408',
  chip='#FF261016', chipStrong='#FF391822', rowHover='#12F87171', rowSelected='#1AEF4444',
  input='#FF15080C', inputHover='#FF1E0B12', inputFocus='#FF281018', glass2='#26F87171', glassBorder='#2EF87171',
  scrim='#E6000000', ov1='#700D0406', ov2='#B80D0406', ov3='#E6080203',
  hs1='#8A1A0A0F', hs2='#B212070C', hs3='#08080407', grid='#14F87171',
  orbA='#5AEF4444', orbB='#47F97316', orbC='#3BF472B6',
  hero1='#FF240C14', hero2='#FF35101C', hero3='#FF180A10', av1='#FFEF4444', av2='#FFF97316',
  success='#FF4FE08B', successSoft='#263FE08B', warning='#FFFFC857', warningSoft='#26FFC857', error='#FFFF6B6B',
  errorSoft='#26FF6B6B', info='#FF67C7FF', infoSoft='#2667C7FF',
  cyan='#FF67E8FF', violet='#FFC8A8BD', gold='#FFF6C46B', cyanS='#1F67E8FF', violetS='#1FC8A8BD', goldS='#1FF6C46B',
  accent='#FFEF4444', accentStrong='#FFCF2F2F', accentLight='#FFFFA8A8', accentSoft='#1FEF4444', accentText='#FF1A0408'),

 dict(file='PurpleGalaxy', name='Purple Galaxy', dark=True,
  bg='#070414', root='#04020E', title='#F2110A1C', side1='#F7181030', side2='#F7110822',
  top1='#E6140C24', top2='#B60D071B', s1='#FF150E2A', s2='#FF0D071B', st1='#FF241A46', st2='#FF160F32',
  ra1='#FF30225C', ra2='#FF1E1441', hover='#FF3D2A70', sunken='#FF080416', glass='#18A855F7',
  border='#FF3E2C6B', borderSoft='#FF2B1E4E', borderGlow='#FFA855F7', hairline='#337B4FBF',
  text='#FFF8F2FF', text2='#FFDCCBFF', muted='#FFA08FD0', disabled='#FF6F5F9C', inverse='#FF140521',
  chip='#FF1B1338', chipStrong='#FF2A1D50', rowHover='#12A855F7', rowSelected='#1FA855F7',
  input='#FF0F0924', inputHover='#FF160D34', inputFocus='#FF1D1142', glass2='#26A855F7', glassBorder='#2EA855F7',
  scrim='#E6000000', ov1='#70070414', ov2='#B8070414', ov3='#E604020E',
  hs1='#8A140C28', hs2='#B20D081E', hs3='#08060414', grid='#14C084FC',
  orbA='#5AA855F7', orbB='#47D946EF', orbC='#3B6366F1',
  hero1='#FF1A1038', hero2='#FF28194F', hero3='#FF12102C', av1='#FFA855F7', av2='#FFD946EF',
  success='#FF4FE0A8', successSoft='#264FE0A8', warning='#FFFFC857', warningSoft='#26FFC857', error='#FFFF7A9E',
  errorSoft='#26FF7A9E', info='#FF8CB6FF', infoSoft='#268CB6FF',
  cyan='#FF7CE8FF', violet='#FFC8A8FF', gold='#FFF6C46B', cyanS='#1F7CE8FF', violetS='#1FC8A8FF', goldS='#1FF6C46B',
  accent='#FFA855F7', accentStrong='#FF8B42E0', accentLight='#FFDCC8FF', accentSoft='#1FA855F7', accentText='#FF140521'),

 dict(file='MinecraftClassic', name='Minecraft Classic', dark=True,
  bg='#10120C', root='#0A0C07', title='#F2141810', side1='#F71E2416', side2='#F7141810',
  top1='#E6181C12', top2='#B612160E', s1='#FF1A2016', s2='#FF11160E', st1='#FF2A321F', st2='#FF1D2617',
  ra1='#FF35402A', ra2='#FF252E1C', hover='#FF445233', sunken='#FF0B0E08', glass='#16A3BE6B',
  border='#FF4A5536', borderSoft='#FF35402A', borderGlow='#FFA3BE6B', hairline='#337A8A4F',
  text='#FFF6F3E6', text2='#FFD8D2BC', muted='#FFA39B85', disabled='#FF756E5C', inverse='#FF14180C',
  chip='#FF222B1B', chipStrong='#FF313C28', rowHover='#12A3BE6B', rowSelected='#1AA3BE6B',
  input='#FF141A10', inputHover='#FF1B2316', inputFocus='#FF232D1C', glass2='#22A3BE6B', glassBorder='#2AA3BE6B',
  scrim='#E6000000', ov1='#7010120C', ov2='#B810120C', ov3='#E60A0C07',
  hs1='#8A181C12', hs2='#B212160E', hs3='#080A0C08', grid='#14A3BE6B',
  orbA='#5AA3BE6B', orbB='#47C2A44B', orbC='#3BD4B84A',
  hero1='#FF20281A', hero2='#FF2B3521', hero3='#FF161C11', av1='#FFA3BE6B', av2='#FFD4B84A',
  success='#FF7CD44A', successSoft='#267CD44A', warning='#FFE8C34A', warningSoft='#26E8C34A', error='#FFE05C5C',
  errorSoft='#26E05C5C', info='#FF8CB6FF', infoSoft='#268CB6FF',
  cyan='#FF8CD4FF', violet='#FFD4A8FF', gold='#FFE8C34A', cyanS='#1F8CD4FF', violetS='#1FD4A8FF', goldS='#1FE8C34A',
  accent='#FFA3BE6B', accentStrong='#FF85A24F', accentLight='#FFD8E8A8', accentSoft='#1FA3BE6B', accentText='#FF14180C'),

 dict(file='LightPremium', name='Light Premium', dark=False,
  bg='#F4F7FC', root='#EDF2F9', title='#F8FBFF', side1='#FCFDFEFF', side2='#F2F6FAFE',
  top1='#F2FBFDFF', top2='#D9F3F7FC', s1='#FFFFFFFF', s2='#FFF7FAFE', st1='#FFFFFFFF', st2='#FFEDF4FC',
  ra1='#FFFFFFFF', ra2='#FFF6F9FE', hover='#FFF0F5FC', sunken='#FFEAF0F8', glass='#B3FFFFFF',
  border='#FFD7E2F0', borderSoft='#FFE5EDF7', borderGlow='#FFB6C9E0', hairline='#3318293D',
  text='#FF0D1826', text2='#FF465A73', muted='#FF70849B', disabled='#FF98A8BA', inverse='#FFFFFFFF',
  chip='#FFEFF4FB', chipStrong='#FFE2EBF6', rowHover='#0A0D1826', rowSelected='#145CE07A',
  input='#FFFFFFFF', inputHover='#FFF8FBFF', inputFocus='#FFFFFFFF', glass2='#D9FFFFFF', glassBorder='#66FFFFFF',
  scrim='#7A0D1826', ov1='#E6F4F7FC', ov2='#B8F1F5FB', ov3='#E6F4F7FC',
  hs1='#8AFDFEFF', hs2='#C7F8FBFF', hs3='#00F8FBFF', grid='#0E0D1826',
  orbA='#335CE07A', orbB='#2E45C8F5', orbC='#26A78BFA',
  hero1='#FFE8F2EE', hero2='#FFE4EEF8', hero3='#FFEDE9FB', av1='#FF2DD4A7', av2='#FF4F8DFD',
  success='#FF14985A', successSoft='#1F14985A', warning='#FFB7791B', warningSoft='#1FB7791B', error='#FFD04545',
  errorSoft='#1FD04545', info='#FF2074C9', infoSoft='#1F2074C9',
  cyan='#FF1785B8', violet='#FF7556D6', gold='#FF9B6E14', cyanS='#1F1785B8', violetS='#1F7556D6', goldS='#1F9B6E14',
  accent='#FF5CE07A', accentStrong='#FF35C25B', accentLight='#FF9CF2AC', accentSoft='#1F5CE07A', accentText='#FF04140A'),
]


def sc(v):
    return f'<SolidColorBrush x:Key="{v[0]}" Color="{v[1]}" />'


def lg(key, stops, start='0,0', end='1,1'):
    lines = [f'<LinearGradientBrush x:Key="{key}" StartPoint="{start}" EndPoint="{end}">']
    for color, offset in stops:
        lines.append(f'<GradientStop Color="{color}" Offset="{offset}" />')
    lines.append('</LinearGradientBrush>')
    return '\n'.join(lines)


def rg(key, stops):
    lines = [f'<RadialGradientBrush x:Key="{key}" GradientOrigin="0.5,0.5" Center="0.5,0.5" RadiusX="0.5" RadiusY="0.5">']
    for color, offset in stops:
        lines.append(f'<GradientStop Color="{color}" Offset="{offset}" />')
    lines.append('</RadialGradientBrush>')
    return '\n'.join(lines)


def render(t):
    solids = [
      ('WindowBackgroundBrush',t['bg']),('RootBackgroundBrush',t['root']),('TitleBarBrush',t['title']),
      ('SurfaceHoverBrush',t['hover']),('SurfaceSunkenBrush',t['sunken']),('SurfaceGlassBrush',t['glass']),
      ('BorderBrush',t['border']),('BorderSoftBrush',t['borderSoft']),('BorderGlowBrush',t['borderGlow']),
      ('HairlineBrush',t['hairline']),('TextPrimaryBrush',t['text']),('TextSecondaryBrush',t['text2']),
      ('TextMutedBrush',t['muted']),('TextDisabledBrush',t['disabled']),('TextInverseBrush',t['inverse']),
      ('ChipBrush',t['chip']),('ChipStrongBrush',t['chipStrong']),('RowHoverBrush',t['rowHover']),
      ('RowSelectedBrush',t['rowSelected']),('InputBrush',t['input']),('InputHoverBrush',t['inputHover']),
      ('InputFocusBrush',t['inputFocus']),('GlassBrush',t['glass']),('GlassStrongBrush',t['glass2']),
      ('GlassBorderBrush',t['glassBorder']),('ScrimBrush',t['scrim']),('ProgressTrackBrush',t['chipStrong']),
      ('SuccessBrush',t['success']),('SuccessSoftBrush',t['successSoft']),('WarningBrush',t['warning']),
      ('WarningSoftBrush',t['warningSoft']),('ErrorBrush',t['error']),('ErrorSoftBrush',t['errorSoft']),
      ('DangerBrush',t['error']),('InfoBrush',t['info']),('InfoSoftBrush',t['infoSoft']),
      ('BrandCyanBrush',t['cyan']),('BrandVioletBrush',t['violet']),('BrandGoldBrush',t['gold']),
      ('BrandCyanSoftBrush',t['cyanS']),('BrandVioletSoftBrush',t['violetS']),('BrandGoldSoftBrush',t['goldS']),
      ('AccentBrush',t['accent']),('AccentStrongBrush',t['accentStrong']),('AccentLightBrush',t['accentLight']),
      ('AccentSoftBrush',t['accentSoft']),('AccentTextBrush',t['accentText']),
    ]
    parts = [
      '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
      '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
      '                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">',
      f'    <sys:String x:Key="ThemeName">{t["name"]}</sys:String>',
      f'    <sys:String x:Key="ThemeIsDark">{"true" if t["dark"] else "false"}</sys:String>',
      f'    <Color x:Key="AccentColor">{t["accent"]}</Color>',
      '    <!-- ' + t['name'] + ' tokens -->',
      *['    '+sc(x) for x in solids],
      '    '+lg('SidebarBackgroundBrush',[(t['side1'],0),(t['side2'],1)]),
      '    '+lg('TopBarBrush',[(t['top1'],0),(t['top2'],1)],'0,0','0,1'),
      '    '+lg('SurfaceBrush',[(t['s1'],0),(t['s2'],1)]),
      '    '+lg('SurfaceStrongBrush',[(t['st1'],0),(t['st2'],1)]),
      '    '+lg('SurfaceRaisedBrush',[(t['ra1'],0),(t['ra2'],1)],'0,0','0,1'),
      '    '+lg('BackdropOverlayBrush',[(t['ov1'],0),(t['ov2'],0.55),(t['ov3'],1)],'0,0','0,1'),
      '    '+lg('HeroScrimBrush',[(t['hs1'],0),(t['hs2'],0.48),(t['hs3'],1)],'0,0','1,0'),
      '    '+lg('HeroPlaceholderBrush',[(t['hero1'],0),(t['hero2'],0.55),(t['hero3'],1)]),
      '    '+lg('AvatarGradientBrush',[(t['av1'],0),(t['av2'],1)]),
      '    '+lg('PanelSheenBrush',[(t['glassBorder'],0),(t['glassBorder'],0.5),(t['glassBorder'],1)]),
      '    '+lg('AccentGradientBrush',[(t['accentLight'],0),(t['accent'],0.55),(t['accentStrong'],1)]),
      '    '+lg('AccentGradientSoftBrush',[(t['accentLight'],0),(t['accent'],0.5),(t['accentStrong'],1)]),
      '    '+rg('VignetteBrush',[('#00000000',0.55),('#66000000' if t['dark'] else '#1F0D1826',1)]),
      '    '+rg('AmbientOrbBrushA',[(t['orbA'],0),('#' + '1E' + t['orbA'][3:],0.55),('#00FFFFFF',1)]),
      '    '+rg('AmbientOrbBrushB',[(t['orbB'],0),('#' + '15' + t['orbB'][3:],0.6),('#00FFFFFF',1)]),
      '    '+rg('AmbientOrbBrushC',[(t['orbC'],0),('#' + '12' + t['orbC'][3:],0.62),('#00FFFFFF',1)]),
      '    '+rg('AccentGlowBrush',[(t['accent'],0),(t['accent'],0.55),('#00FFFFFF',1)]),
      '''    <DrawingBrush x:Key="GridPatternBrush" TileMode="Tile" Stretch="None" Viewport="0,0,56,56" ViewportUnits="Absolute">
        <DrawingBrush.Drawing>
            <GeometryDrawing Brush="Transparent">
                <GeometryDrawing.Geometry>
                    <GeometryGroup>
                        <LineGeometry StartPoint="0,0" EndPoint="0,56" />
                        <LineGeometry StartPoint="0,0" EndPoint="56,0" />
                    </GeometryGroup>
                </GeometryDrawing.Geometry>
                <GeometryDrawing.Pen>
                    <Pen Brush="''' + t['grid'] + '''" Thickness="1" />
                </GeometryDrawing.Pen>
            </GeometryDrawing>
        </DrawingBrush.Drawing>
    </DrawingBrush>''',
      '</ResourceDictionary>',
    ]
    return '\n'.join(parts)

out = Path('src/XDLauncher/Themes')
out.mkdir(parents=True, exist_ok=True)
for t in THEMES:
    p = out / f'{t["file"]}.xaml'
    p.write_text(render(t), encoding='utf-8')
    print(p)
