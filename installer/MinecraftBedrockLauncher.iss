; ---------------------------------------------------------------------------
;  XD Launcher · instalador
;  Genera MinecraftLauncher_Setup.exe con Inno Setup 6
; ---------------------------------------------------------------------------

#define AppName "XD Launcher"
#define AppShortName "MinecraftLauncher"
#define AppVersion "1.3.8"
#define AppPublisher "XD Studio"
#define AppExeName "MinecraftLauncher.exe"
#define AppId "{{B4E0B7A9-3F1D-4C7E-9E2B-6F5D1A8C2D40}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://www.minecraft.net
AppSupportURL=https://www.minecraft.net
AppUpdatesURL=https://www.minecraft.net
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename={#AppShortName}_Setup
SetupIconFile=..\src\XDLauncher\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern
WizardSizePercent=120
WizardImageFile=images\Wizard.bmp
WizardSmallImageFile=images\WizardSmall.bmp
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableDirPage=no
AllowNoIcons=yes
AllowUNCPath=false
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
VersionInfoVersion=1.3.8
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Instalador de XD Launcher (Minecraft Java + Bedrock)
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el &escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce
Name: "startmenuicon"; Description: "Crear un acceso directo en el menú &Inicio"; GroupDescription: "Accesos directos:"; Flags: checkedonce
Name: "quicklaunch"; Description: "Añadir el lanzador al menú &contextual de Windows"; GroupDescription: "Integración:"; Flags: unchecked

[Files]
Source: "..\dist\MinecraftLauncher\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion

[InstallDelete]
Type: files; Name: "{app}\MinecraftBedrockLauncher.exe"
Type: files; Name: "{app}\MinecraftBedrockLauncher.dll"
Type: files; Name: "{app}\MinecraftBedrockLauncher.deps.json"
Type: files; Name: "{app}\MinecraftBedrockLauncher.runtimeconfig.json"
Type: files; Name: "{app}\MinecraftBedrockLauncher.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"; WorkingDir: "{app}"
Name: "{group}\MINECRAFT MOBILE CONNECT"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletekey; Tasks: quicklaunch
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExeName}\shell\open\command"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey; Tasks: quicklaunch

; Protocolo xdlauncher:// para traspasar la sesion de la web al launcher de escritorio
Root: HKA; Subkey: "Software\Classes\xdlauncher"; ValueType: string; ValueName: ""; ValueData: "URL:XDLauncher Protocol"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\xdlauncher"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\xdlauncher\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\xdlauncher\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DataDirPage: TInputDirWizardPage;

function DefaultDataFolder: String;
begin
  Result := ExpandConstant('{userappdata}\XDLauncher');
end;

procedure InitializeWizard;
begin
  { Página de carpeta de datos con botón "Examinar" }
  DataDirPage := CreateInputDirPage(wpSelectDir,
    'Carpeta de datos del launcher',
    '¿Dónde se guardan los ajustes, las herramientas ADB/scrcpy y la caché?',
    'Por defecto se usa la carpeta de usuario (AppData). Si prefieres guardarla en otro disco ' +
    '(por ejemplo D:\MinecraftData), elige la carpeta aquí: el instalador la creará con los ' +
    'permisos necesarios y copiará tus datos actuales.' + #13#10 + #13#10 +
    'Deja el campo como está para usar la ubicación por defecto.',
    False, SetupMessage(msgNewFolderName));
  DataDirPage.Add('');
  DataDirPage.Values[0] := GetPreviousData('DataDir', DefaultDataFolder);
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'DataDir', DataDirPage.Values[0]);
end;

function CreatePathDeep(const Path: String): Boolean;
var
  Parent: String;
begin
  Result := True;
  if Path = '' then
  begin
    Result := False;
    Exit;
  end;

  if DirExists(Path) then
    Exit;

  Parent := ExtractFileDir(Path);
  if (Parent <> '') and (Parent <> Path) and (not DirExists(Parent)) then
    Result := CreatePathDeep(Parent);

  if Result and (not DirExists(Path)) then
    Result := CreateDir(Path);
end;

function CopyTree(const SourceDir: String; const DestDir: String): Boolean;
var
  FindRec: TFindRec;
  Src: String;
  Dst: String;
begin
  Result := True;

  if not FindFirst(SourceDir + '\*', FindRec) then
    Exit;

  repeat
    Src := SourceDir + '\' + FindRec.Name;
    Dst := DestDir + '\' + FindRec.Name;

    if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
    begin
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        if not DirExists(Dst) then
          CreateDir(Dst);
        Result := CopyTree(Src, Dst) and Result;
      end;
    end
    else
    begin
      if not FileExists(Dst) then
        CopyFile(Src, Dst, False);
    end;
  until not FindNext(FindRec);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Target: String;
  Source: String;
begin
  Result := True;

  if CurPageID = DataDirPage.ID then
  begin
    Target := Trim(DataDirPage.Values[0]);

    if (Target = '') or (SameText(Target, DefaultDataFolder)) then
    begin
      DeleteFile(ExpandConstant('{app}\data.path.txt'));
      Exit;
    end;

    if (Pos(':', Target) = 0) and (Pos('\', Target) = 0) then
    begin
      MsgBox('La ruta debe ser completa, por ejemplo D:\MinecraftData.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if not CreatePathDeep(Target) then
    begin
      MsgBox('No se pudo crear la carpeta:' + #13#10 + Target + #13#10#13#10 +
             'Comprueba que la ruta sea válida y que el disco tenga espacio.',
             mbError, MB_OK);
      Result := False;
      Exit;
    end;

    { Copia los datos actuales del launcher si el destino está vacío }
    Source := ExpandConstant('{userappdata}\XDLauncher');
    if DirExists(Source) and (not FileExists(Target + '\settings.json')) then
    begin
      if not CopyTree(Source, Target) then
        Log('No se pudieron copiar algunos datos existentes a ' + Target);
    end;

    { La aplicación lee esta ruta para usar esa carpeta }
    SaveStringToFile(ExpandConstant('{app}\data.path.txt'), Target, False);
  end;
end;
