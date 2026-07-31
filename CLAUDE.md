# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SDSM (Simple Data Storage Manager) is a web-based file browser: an ASP.NET Core backend (net10.0) serves a REST API plus a vanilla-TypeScript frontend styled with Pico.css. The filesystem is exposed as named volumes (configured in the `Volumes` section of appsettings.json as name → host path; a single CLI argument is shorthand for one volume).

## Build & Run

```bash
./build.sh                          # full build: copies frontend assets, compiles TS, builds dotnet — all into ./build
dotnet run --project SDSM-Backend/SDSM-Server -- <rootDir>   # run backend (dev, http://localhost:5024)
dotnet build SDSM-Backend/SDSM-Server/SDSM-Server.csproj     # backend only
tsc -p SDSM-Frontend/ts/tsconfig.json --outDir <dir>         # frontend TS only
```

There are no tests and no linter configured.

Important: the server locates frontend assets **relative to its own assembly location** (`Program.cs` serves `static/`, `css/`, `js/` from the app dir). The app only works fully when run from the `build/` directory produced by `build.sh` — running via `dotnet run` serves the API but not the frontend files.

## Architecture

- **Backend** (`SDSM-Backend/SDSM-Server/`): minimal ASP.NET Core app. `Program.cs` takes the root directory as the single CLI argument and registers it as a singleton `Config { RootDir }`, which controllers receive via parameter injection. Two attribute-routed controllers in `Controllers/`:
  - `GET /files/{*path}` — returns JSON list of `Models.FileSystemEntry` (`name`, `isDirectory`; C# PascalCase serializes to camelCase)
  - `GET /downloadfile/{*path}` — streams the file as `application/octet-stream`
  - Both have known **path-traversal TODOs** — paths are combined with `RootDir` unchecked.
- **Frontend** (`SDSM-Frontend/`): no framework, no bundler, no npm. TypeScript in `ts/` compiles to ES modules loaded directly by `static/index.html`. `file_system.ts` fetches `/files/...` and renders the file table (directories navigate, files link to `/downloadfile/...`). The `FileSystemEntry` interface in `ts/file_system_entry.ts` must stay in sync with the C# model in `Models/FileSystemEntry.cs`. `css/pico.min.css` and `js/minimal-theme-switcher.js` are vendored third-party files — don't edit them.

## Was SDSM ist
NAS-Betriebssystem-Oberfläche: hostet das Dateisystem als Webinterface.
Plugin-basiert — Kern liefert Explorer + System-Diagnostics, alles
Weitere (Movie-Library à la Plex, Watchlist, Musik-Streaming) sind Plugins.

## Architektur-Regeln
- Kernfunktionen kommen nie ins Plugin, Plugin-Logik nie in den Kern
- Jedes Plugin spricht nur über die Plugin-API mit dem System
- Konkrete Schnittstelle (Manifest, `ISdsmPlugin`, `IFileSystemApi`, UI-`mount`-Contract): siehe docs/ARCHITECTURE.md

## Befehle
Build:  ./build.sh                          (alles nach ./build, inkl. Plugins)
Tests:  dotnet test SDSM-Backend/SDSM.sln   (einzelner Test: --filter "FullyQualifiedName~<Name>")
Lint:   keins konfiguriert (tsc strict prüft beim Build)

## Design
Referenz-Mockup: docs/mockups/frontpage.png
