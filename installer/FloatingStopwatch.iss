; Inno Setup 6.3+ script. Build the exe first (build.bat), then open this file in Inno Setup and press Compile.
; The installer is written to ..\installer-output\FloatingStopwatch-Setup.exe

[Setup]
AppId={{B7A3C1E0-5D2F-4C8A-9E41-6F0A2D7B1C33}
AppName=Floating Stopwatch
AppVersion=1.0.0
AppPublisher=Floating Stopwatch
DefaultDirName={autopf}\Floating Stopwatch
DefaultGroupName=Floating Stopwatch
OutputDir=..\installer-output
OutputBaseFilename=FloatingStopwatch-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
UninstallDisplayIcon={app}\FloatingStopwatch.exe

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Floating Stopwatch when Windows starts"; Flags: unchecked

[Files]
Source: "..\dist\FloatingStopwatch.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Floating Stopwatch"; Filename: "{app}\FloatingStopwatch.exe"
Name: "{autodesktop}\Floating Stopwatch"; Filename: "{app}\FloatingStopwatch.exe"; Tasks: desktopicon
Name: "{userstartup}\Floating Stopwatch"; Filename: "{app}\FloatingStopwatch.exe"; Tasks: startup

[Run]
Filename: "{app}\FloatingStopwatch.exe"; Description: "Launch Floating Stopwatch"; Flags: nowait postinstall skipifsilent
