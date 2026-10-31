; Inno Setup script for the Windows installer: Temnorid-Craft-Setup.exe
; Build the player first (Unity: Temnorid > Craft > Build Windows Player), then compile this with Inno Setup:
;   "ISCC.exe" installer\Craft.iss
; The installer is written to Builds\Installer.

#define AppName "Temnorid Craft"
#define AppVersion "0.4.1"
#define AppExe "TemnoridCraft.exe"

[Setup]
AppId={{2F9C6A41-8B7D-4E3A-A1C5-5D0E7B9F3C22}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Temnorid
AppPublisherURL=https://craft.temnorid.com
DefaultDirName={autopf}\Temnorid Craft
DefaultGroupName=Temnorid Craft
DisableProgramGroupPage=yes
OutputDir=..\Builds\Installer
OutputBaseFilename=Temnorid-Craft-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Files]
Source: "..\Builds\Windows\TemnoridCraft\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; No skipifsilent: when the game updates itself the installer runs silently and must start the game again.
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall
