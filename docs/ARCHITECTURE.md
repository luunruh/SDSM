# SDSM-Architektur: Plugin-System

SDSM besteht aus einem schlanken Kern und Plugins. Der Kern hostet das
Dateisystem, lädt Plugins und stellt die Shell-UI (Sidebar, Topbar,
Content-Bereich). Alle Features — auch der Explorer — sind Plugins, die
ausschließlich über die hier beschriebene Plugin-API mit dem System
sprechen.

## Plugin-Modell

Ein Plugin ist **Full-Stack**: ein Ordner mit Manifest, optionalem
Backend-Teil (.NET-Assembly) und optionalem UI-Teil (ES-Module).
Beide Teile sind unabhängig optional — der Explorer z. B. ist UI-only,
eine Movie-Library hätte zusätzlich Backend-Logik (Scanner, Metadaten).

### Paketstruktur

```
plugins/
  <plugin-id>/
    manifest.json          # Pflicht
    backend/<Name>.dll     # optional: .NET-Assembly + Abhängigkeiten
    ui/main.js             # optional: ES-Modul-Einstiegspunkt + Assets
```

`<plugin-id>` ist der Ordnername und muss mit `id` im Manifest
übereinstimmen: nur Kleinbuchstaben, Ziffern, `-`.

### manifest.json

```json
{
  "id": "explorer",
  "name": "Explorer",
  "version": "0.1.0",
  "backend": "backend/SDSM.Plugin.Explorer.dll",
  "ui": "ui/main.js",
  "nav": { "title": "Explorer", "icon": "folder" }
}
```

- `id`, `name`, `version`: Pflicht.
- `backend`: relativer Pfad zur Assembly; weglassen bei UI-only-Plugins.
- `ui`: relativer Pfad zum ES-Modul-Einstieg; weglassen bei Headless-Plugins.
- `nav`: optional; erzeugt einen Sidebar-Eintrag in der Shell.

## Registrierung & Lifecycle

Der `PluginLoader` im Kern scannt beim Serverstart das Plugin-Verzeichnis
(Standard: `<AppDir>/plugins`, überschreibbar via Konfiguration
`Plugins:Directory`). Pro Unterordner:

1. `manifest.json` parsen und validieren. Ungültige Plugins werden mit
   Log-Warnung übersprungen, der Server startet trotzdem.
2. Falls `backend` gesetzt: Assembly in einen eigenen
   `AssemblyLoadContext` laden (mit `AssemblyDependencyResolver`;
   SDK-Assembly wird an den Default-Context delegiert, damit
   Contract-Typen identisch bleiben). Im Assembly wird genau ein Typ
   erwartet, der `ISdsmPlugin` implementiert.
3. `ISdsmPlugin.ConfigureServices(...)` vor dem App-Build aufrufen,
   `ISdsmPlugin.MapEndpoints(...)` danach — die Route-Gruppe ist fest
   auf `/api/plugins/{id}/` gemountet, ein Plugin kann keine Routen
   außerhalb seines Namensraums registrieren.
4. Falls `ui` gesetzt: der Plugin-Ordner wird read-only statisch unter
   `/plugins/{id}/` serviert.

Es gibt keine dynamische (Ent-)Ladung zur Laufzeit; Plugin-Änderungen
erfordern einen Neustart.

### Backend-Contract (SDSM.PluginSdk)

Plugins referenzieren ausschließlich das Projekt `SDSM.PluginSdk`
(Contracts-Assembly), nie den Server selbst:

```csharp
public interface ISdsmPlugin
{
    string Id { get; }                                  // muss Manifest-id entsprechen
    void ConfigureServices(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder group);     // bereits auf /api/plugins/{id}/ gemountet
}
```

### Discovery-API für die Shell

`GET /api/plugins` liefert die geladenen Plugins:

```json
[
  { "id": "explorer", "name": "Explorer", "version": "0.1.0",
    "ui": "/plugins/explorer/ui/main.js",
    "nav": { "title": "Explorer", "icon": "folder" } }
]
```

## Dateisystem-Zugriff

Plugins greifen **nie** direkt mit `System.IO` auf das Dateisystem zu.
Der Kern stellt eine gescopte Filesystem-API bereit.

Das Dateisystem ist eine Menge **benannter Volumes** (konfiguriert in
der `Volumes`-Section von appsettings.json als `Name → Hostpfad`; ein
einzelnes CLI-Argument ist die Kurzform für genau ein Volume, benannt
nach dem Verzeichnis). Pfade in der API sind `Volumename/rel/pfad`;
der Leerstring listet die Volumes selbst (als Einträge mit
`kind: "Volume"`). Jeder Pfad wird kanonisiert und gegen die Wurzel
seines Volumes geprüft — Path-Traversal wird zentral hier abgefangen
(`ArgumentException`/400), unbekannte Volumes ergeben
`DirectoryNotFoundException`/404.

### Backend: `IFileSystemApi` (per DI injizierbar)

```csharp
public interface IFileSystemApi
{
    IReadOnlyList<VolumeInfo> GetVolumes();   // Name, TotalBytes, AvailableBytes
    IReadOnlyList<FileSystemEntry> List(string relPath);
    FileSystemEntry Stat(string relPath);
    Stream OpenRead(string relPath);

    // Schreiboperationen verlangen einen Pfad *innerhalb* eines Volumes —
    // Volume-Wurzeln selbst sind unveränderlich.
    void CreateDirectory(string relPath);
    void Delete(string relPath);              // Datei oder Ordner (rekursiv)
    void Rename(string relPath, string newName);
    Task SaveAsync(string relPath, Stream content);
}

public class FileSystemEntry
{
    public required string Name { get; set; }
    public required bool IsDirectory { get; set; }
    public long? SizeBytes { get; set; }        // null bei Ordnern
    public required string Kind { get; set; }   // z. B. "Ordner", "Matroska Video"
    public DateTime ModifiedUtc { get; set; }
}
```

### Frontend: `/api/fs`-Endpoints (Kern)

- `GET /api/fs/list/{*path}` → JSON-Array von `FileSystemEntry`
  (camelCase: `name`, `isDirectory`, `sizeBytes`, `kind`, `modifiedUtc`)
- `GET /api/fs/download/{*path}` → Datei-Stream
- `POST /api/fs/mkdir/{*path}` → 204
- `PUT /api/fs/upload/{*path}` (Body = Dateiinhalt, roh) → 204
- `DELETE /api/fs/delete/{*path}` → 204
- `POST /api/fs/rename/{*path}` (Body: `{"newName": "…"}`) → 204;
  409, wenn das Ziel existiert

Fehlerkonvention aller Endpoints: 400 bei ungültigem Pfad/Namen
(inkl. Traversal), 404 bei unbekanntem Volume oder fehlendem Eintrag.

## UI-Auslieferung

Die Shell (Kern-Frontend) baut die Sidebar aus `GET /api/plugins` und
lädt beim Aktivieren eines Plugins dessen `ui`-Modul per dynamischem
`import()`. Das Modul exportiert default ein Objekt mit `mount`:

```ts
interface SdsmPluginUi {
    // Rückgabewert (optional): Cleanup-Funktion, wird beim Wechsel
    // zu einem anderen Plugin aufgerufen.
    mount(container: HTMLElement, ctx: PluginUiContext): void | (() => void);
}

interface PluginUiContext {
    pluginBase: string;   // "/api/plugins/<id>"  — eigene Backend-Endpoints
    assetBase: string;    // "/plugins/<id>"      — eigene statische Dateien
    fs: {                 // Client für die Kern-Filesystem-API
        list(path: string): Promise<FileSystemEntry[]>;
        downloadUrl(path: string): string;
        mkdir(path: string): Promise<void>;
        upload(path: string, content: Blob): Promise<void>;
        delete(path: string): Promise<void>;
        rename(path: string, newName: string): Promise<void>;
    };
}
```

Das Plugin rendert ausschließlich in den übergebenen `container`
(Content-Bereich der Shell) und spricht Backends nur über `ctx` an —
keine hartkodierten Kern-URLs im Plugin-Code.

## Repo-Layout & Build

```
SDSM-Backend/
  SDSM-Server/        # Kern: Shell-Hosting, PluginLoader, /api/fs, /api/plugins
  SDSM.PluginSdk/     # Contracts: ISdsmPlugin, IFileSystemApi, Modelle
SDSM-Frontend/        # Shell-UI (Sidebar, Topbar, Plugin-Mounting)
SDSM-Plugins/
  explorer/           # Erstes Plugin (UI-only): manifest.json + ui/*.ts
```

`build.sh` baut alles nach `build/`: Server + SDK via `dotnet build`,
Shell-TypeScript nach `build/js`, jedes Plugin nach
`build/plugins/<id>/` (Manifest kopieren, `ui/*.ts` mit tsc
kompilieren, ggf. Backend-Projekt dorthin publishen). Der Server
serviert `build/plugins` relativ zu seinem Assembly-Verzeichnis.
