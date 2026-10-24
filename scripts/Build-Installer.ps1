param([switch]$SkipInstaller)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root
New-Item -ItemType Directory -Force assets, publish, dist | Out-Null
& (Join-Path $PSScriptRoot "Generate-Icon.ps1")
if (-not (Test-Path (Join-Path $root "assets\SeedWallpaper.ico"))) { throw "Icon generation failed: icon file was not created" }
dotnet publish .\src\SeedWallpaper\SeedWallpaper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o .\publish
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
if ($SkipInstaller) { Write-Host "Portable application ready at publish\SeedWallpaper.exe"; exit }
$compiler = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) { throw "Install Inno Setup 6 to create the setup EXE, or use -SkipInstaller for a portable build." }
& $compiler .\installer\SeedWallpaper.iss
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Write-Host "Installer ready: dist\SeedWallpaper-Setup.exe"
