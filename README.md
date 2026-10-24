# 🌱 Seed Wallpaper

A nature-inspired C# WPF wallpaper application for Windows.

## Phase 1 — Foundation
This first milestone is a navigable application shell, not a complete wallpaper engine. It includes a dark forest-green theme and placeholder pages for Home, Discover, Seed Studio, My Garden and Settings.

## Requirements
- Windows 10/11
- .NET 10 SDK
- Visual Studio with .NET desktop development workload, or `dotnet` CLI

## Run
```powershell
cd src/SeedWallpaper
dotnet restore
dotnet run
```

## Roadmap
1. WPF application foundation and navigation
2. Wallpaper discovery and local image library
3. Windows desktop wallpaper integration and destination dialog
4. AI Studio with provider adapters and reference images
5. Security, caching, testing and packaging

**Notes:** Direct Windows lock-screen changes depend on platform capabilities and policy. AI providers may impose fees, usage restrictions or provenance markers. Never commit API keys.

The project has not been compiled on Windows in this environment.
