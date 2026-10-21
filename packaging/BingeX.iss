#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif
#define AppVersion "0.13.8"

[Setup]
AppId={{9B6F26D3-7049-4C3B-AD24-24B593DA4768}
AppName=BingeX
AppVersion={#AppVersion}
AppVerName=BingeX {#AppVersion} preview
AppPublisher=BingeX contributors
AppPublisherURL=https://github.com/noctistt/BingeX
DefaultDirName={localappdata}\Programs\BingeX
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\BingeX.exe
SetupIconFile=..\src\BingeX\Assets\BingeX.ico
OutputDir={#OutputDir}
OutputBaseFilename=BingeX-{#AppVersion}-Setup-x64
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
CloseApplications=yes
CloseApplicationsFilter=BingeX.exe
RestartApplications=no

[Tasks]
Name: "openwith"; Description: "Show BingeX in Open with for supported media files"; GroupDescription: "Windows integration:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\BingeX"; Filename: "{app}\BingeX.exe"
Name: "{autodesktop}\BingeX"; Filename: "{app}\BingeX.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BingeX.exe"; Description: "Launch BingeX"; Flags: nowait postinstall skipifsilent

[Registry]
; Register an option; the user chooses their default player in Windows.
Root: HKCU; Subkey: "Software\Classes\BingeX.Media"; ValueType: string; ValueData: "BingeX media file"; Flags: uninsdeletekey; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\BingeX.Media\DefaultIcon"; ValueType: string; ValueData: """{app}\BingeX.exe"",0"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\BingeX.Media\shell\open\command"; ValueType: string; ValueData: """{app}\BingeX.exe"" ""%1"""; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "BingeX"; Flags: uninsdeletekey; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\shell\open\command"; ValueType: string; ValueData: """{app}\BingeX.exe"" ""%1"""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "BingeX"; Flags: uninsdeletekey; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Play local videos and audio with BingeX."; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: """{app}\BingeX.exe"",0"; Tasks: openwith
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "BingeX"; ValueData: "Software\BingeX\Capabilities"; Flags: uninsdeletevalue; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mp4\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mp4"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp4"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mkv\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mkv"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mkv"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.avi\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".avi"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".avi"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mov\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mov"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mov"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.webm\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".webm"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".webm"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.m4v\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".m4v"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4v"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mpg\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mpg"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpg"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mpeg\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mpeg"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mpeg"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.ts\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".ts"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ts"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mts\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mts"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mts"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.m2ts\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".m2ts"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2ts"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.wmv\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".wmv"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wmv"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.flv\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".flv"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flv"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.ogv\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".ogv"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogv"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.3gp\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".3gp"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".3gp"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.vob\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".vob"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".vob"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.m2v\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".m2v"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m2v"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.divx\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".divx"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".divx"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.mp3\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".mp3"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".mp3"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.flac\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".flac"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".flac"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.wav\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".wav"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wav"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.ogg\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".ogg"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".ogg"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.opus\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".opus"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".opus"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.m4a\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".m4a"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".m4a"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.aac\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".aac"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aac"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.wma\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".wma"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".wma"; ValueData: "BingeX.Media"; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\.aiff\OpenWithProgids"; ValueType: string; ValueName: "BingeX.Media"; ValueData: ""; Flags: uninsdeletevalue uninsdeletekeyifempty; Tasks: openwith
Root: HKCU; Subkey: "Software\Classes\Applications\BingeX.exe\SupportedTypes"; ValueType: string; ValueName: ".aiff"; ValueData: ""; Tasks: openwith
Root: HKCU; Subkey: "Software\BingeX\Capabilities\FileAssociations"; ValueType: string; ValueName: ".aiff"; ValueData: "BingeX.Media"; Tasks: openwith
