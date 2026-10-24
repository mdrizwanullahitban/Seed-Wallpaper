#define MyAppName "Seed Wallpaper"
#define MyAppVersion "1.0.0"
#define MyAppExeName "SeedWallpaper.exe"

[Setup]
AppId={{B82BC7A6-8359-4A43-B2A6-DF1A4A67398C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Seed Wallpaper
DefaultDirName={autopf}\Seed Wallpaper
DefaultGroupName=Seed Wallpaper
OutputDir=..\dist
OutputBaseFilename=SeedWallpaper-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\SeedWallpaper.exe
SetupIconFile=..\assets\SeedWallpaper.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Icons]
Name: "{group}\Seed Wallpaper"; Filename: "{app}\SeedWallpaper.exe"
Name: "{autodesktop}\Seed Wallpaper"; Filename: "{app}\SeedWallpaper.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SeedWallpaper.exe"; Description: "Launch Seed Wallpaper"; Flags: nowait postinstall skipifsilent
