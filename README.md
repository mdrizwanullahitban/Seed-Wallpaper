# 🌱 Seed Wallpaper

A nature-inspired Windows wallpaper application built with C#, WPF and .NET 10.

## Current status: Phase 2, milestone 1

**Implemented:**
- Forest-green WPF desktop interface and page navigation
- My Garden local image import (JPG, PNG, BMP)
- Persistent local wallpaper collection at `%LOCALAPPDATA%\SeedWallpaper\MyGarden`
- Image preview
- Native Windows desktop wallpaper application with Fill, Fit, Stretch, Center, Tile and Span
- Apply destination dialog: Desktop, Lock screen, Both
- Lock-screen fallback opens Windows Settings (direct setting not implemented)

**Not yet implemented:** online wallpaper discovery, AI Studio, favourites, slideshow, full MVVM refactor, automatic tests and installer.

## Run on Windows

Requires .NET 10 SDK.

```powershell
git pull origin main
dotnet build .\src\SeedWallpaper\SeedWallpaper.csproj
dotnet run --project .\src\SeedWallpaper\SeedWallpaper.csproj
```

## Test

1. Open My Garden and import a JPG or PNG.
2. Select it to see a preview.
3. Click Apply Wallpaper, select Desktop and choose Fill.
4. Confirm that your Windows desktop wallpaper changes.

## Security and permissions

Desktop wallpaper changes normally run under the current Windows user without elevation. The application writes imported images to the current user's local application data directory. Lock-screen changes depend on Windows capabilities and policies, so the current implementation opens the Windows Personalisation settings page instead. No API credentials are included.

**Build verification:** Code was pushed through GitHub but has not been compiled in this environment; validate with `dotnet build` on Windows.


## Install like a normal Windows application

The repository includes an automated Windows installer build at **Actions → Windows installer**. Select **Run workflow**, wait for the Windows build to finish, and download the **SeedWallpaper-Windows-Installer** artifact. Extract the ZIP, then double-click **SeedWallpaper-Setup.exe**. The setup creates a Start menu entry, an optional desktop shortcut, and an uninstaller. Windows may display a SmartScreen warning because this community-built installer is not code-signed.

Alternatively, download the **SeedWallpaper-Portable** artifact and run **SeedWallpaper.exe** from the extracted folder without installing.

To build locally on Windows, install .NET 10 SDK and Inno Setup 6, then run `powershell -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1`. The output installer is `dist\SeedWallpaper-Setup.exe`. The publish command creates a self-contained win-x64 executable, so end users do not need to install .NET separately. Web features may require the Microsoft Edge WebView2 Runtime, normally present on current Windows systems.
