; Script generated for Shift - Pomodoro Installer
#define MyAppName "Shift - Pomodoro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Shift Pomodoro Community"
#define MyAppURL "https://github.com/Shift_Pomodoro"
#define MyAppExeName "FlowWindows.App.exe"
#define SourceDir "..\dist\FlowWindows-x64"

[Setup]
AppId={{5E4B281A-4F3D-4A8E-8E0E-D9F57367B0A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Shift - Pomodoro
DisableProgramGroupPage=yes
OutputBaseFilename=Shift-Pomodoro-Setup-x64
OutputDir=..\dist\installer
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern
SetupIconFile=..\dist\FlowWindows-x64\Assets\AppIcon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Note: User data is kept in %LOCALAPPDATA%\FlowWindows and is never overwritten or deleted by installer

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

