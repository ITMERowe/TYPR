#define AppVersion GetEnv("APP_VERSION")

[Setup]
AppId={{26BFC7A4-E90F-4C64-9D35-924A3EE6E5B0}
AppName=TYPR
AppVersion={#AppVersion}
AppPublisher=ITMERowe
AppPublisherURL=https://github.com/ITMERowe/TYPR
AppSupportURL=https://github.com/ITMERowe/TYPR/issues
AppUpdatesURL=https://github.com/ITMERowe/TYPR/releases/latest
DefaultDirName={autopf}\TYPR
DefaultGroupName=TYPR
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
LicenseFile=..\LICENSE
OutputDir=..\artifacts\release
OutputBaseFilename=TYPR-Setup-{#AppVersion}-win-x64
UninstallDisplayIcon={app}\TYPR.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
VersionInfoVersion={#AppVersion}.0
CloseApplications=yes
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "..\artifacts\release\TYPR-Portable-win-x64.exe"; DestDir: "{app}"; DestName: "TYPR.exe"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\TYPR"; Filename: "{app}\TYPR.exe"
Name: "{autodesktop}\TYPR"; Filename: "{app}\TYPR.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TYPR.exe"; Description: "Launch TYPR"; Flags: postinstall nowait skipifsilent
