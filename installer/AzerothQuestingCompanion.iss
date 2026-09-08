#ifndef MyAppVersion
  #define MyAppVersion "0.1.3"
#endif

#define MyAppName "Azeroth Questing Companion"
#define MyAppExeName "AzerothQuestingCompanion.exe"
#define MyAppPublisher "Frostcanvas"
#define MyAppURL "https://github.com/Frostcanvas/AzerothQuesting-Companion"

[Setup]
AppId={{8CF6F5B3-1EA6-4A38-AF8C-6A57D63E5B91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\Azeroth Questing Companion
DefaultGroupName=Azeroth Questing
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\publish\installer
OutputBaseFilename=AzerothQuestingCompanion-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Windows installer

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Azeroth Questing Companion"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Azeroth Questing Companion"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Azeroth Questing Companion"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: IsSilentInstall

[Code]
function IsSilentInstall: Boolean;
begin
  Result := WizardSilent;
end;
