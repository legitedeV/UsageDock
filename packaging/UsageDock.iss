#ifndef AppVersion
  #define AppVersion "0.5.0"
#endif
#ifndef PublishDir
  #error PublishDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
[Setup]
AppId={{D94B107E-0D1D-4749-BD73-B9F68B69758A}
AppName=UsageDock
AppVersion={#AppVersion}
AppPublisher=UsageDock contributors
DefaultDirName={localappdata}\Programs\UsageDock
DefaultGroupName=UsageDock
UninstallDisplayIcon={app}\UsageDock.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=UsageDock-{#AppVersion}-win-x64-setup
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\UsageDock"; Filename: "{app}\UsageDock.exe"
Name: "{autodesktop}\UsageDock"; Filename: "{app}\UsageDock.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\UsageDock.exe"; Description: "{cm:LaunchProgram,UsageDock}"; Flags: nowait postinstall skipifsilent
