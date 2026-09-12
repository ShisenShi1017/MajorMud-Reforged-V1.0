#ifndef MyAppVersion
  #define MyAppVersion "0.1.0-beta"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\Release\RealmGate-Public"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\Release\Packages"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "RealmGate-Beta-Setup"
#endif

[Setup]
AppId={{A1C1FD5E-3E6F-4C1A-BF65-1E765B94C4A8}
AppName=RealmGate
AppVersion={#MyAppVersion}
AppVerName=RealmGate {#MyAppVersion}
AppPublisher=RealmGate
DefaultDirName={localappdata}\Programs\RealmGate
DefaultGroupName=RealmGate
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\..\Apps\Source\RealmGate.App\Assets\Branding\RealmGate.ico
UninstallDisplayIcon={app}\RealmGate.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\RealmGate"; Filename: "{app}\RealmGate.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\RealmGate"; Filename: "{app}\RealmGate.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\RealmGate.exe"; Description: "Launch RealmGate"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
