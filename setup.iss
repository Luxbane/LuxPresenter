; LuxPresenter Inno Setup Script
#define MyAppName "LuxPresenter"
#define MyAppVersion "1.1.3"
#define MyAppPublisher "Luxbane"
#define MyAppExeName "LuxPresenter.exe"
#define SourceDir "..\Build"

[Setup]
AppId={{D1A3961E-745B-4C6A-914F-94B5E8BC2901}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\Releases
OutputBaseFilename=LuxPresenter_Setup
SetupIconFile=app.ico
LicenseFile=LICENSE
Compression=lzma2/fast
SolidCompression=no
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{userdocs}\{#MyAppName}\Database"

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,display_log.txt,LuxPresenter.exe.WebView2\*,Data\songs.json,*.pptx,*.ppt,*.ppsx,*.pps,*.odp,*.pdf,Data\PresentationCache\*,Data\presentations.json"

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autoprograms}\{#MyAppName}\Songs Database Folder"; Filename: "{userdocs}\{#MyAppName}\Database"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
