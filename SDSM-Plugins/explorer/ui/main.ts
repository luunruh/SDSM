// Keep in sync with the contract in docs/ARCHITECTURE.md
interface FileSystemEntry {
    name: string;
    isDirectory: boolean;
    sizeBytes: number | null;
    kind: string;
    modifiedUtc: string;
}

interface PluginUiContext {
    pluginBase: string;
    assetBase: string;
    fs: {
        list(path: string): Promise<FileSystemEntry[]>;
        downloadUrl(path: string): string;
        mkdir(path: string): Promise<void>;
        upload(path: string, content: Blob): Promise<void>;
        delete(path: string): Promise<void>;
        rename(path: string, newName: string): Promise<void>;
    };
}

const FOLDER_ICON = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></svg>`;
const FILE_ICON = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/></svg>`;
const TRASH_ICON = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>`;

function formatSize(bytes: number | null): string {
    if (bytes === null) {
        return "–";
    }
    const units = ["B", "KB", "MB", "GB", "TB"];
    let value = bytes;
    let unit = 0;
    while (value >= 1000 && unit < units.length - 1) {
        value /= 1000;
        unit++;
    }
    const rounded = unit > 0 && value < 10 ? value.toFixed(1) : Math.round(value).toString();
    return `${rounded} ${units[unit]}`;
}

function formatDate(iso: string): string {
    const date = new Date(iso);
    const pad = (n: number) => n.toString().padStart(2, "0");
    return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
}

function mount(container: HTMLElement, ctx: PluginUiContext): () => void {
    const style = document.createElement("link");
    style.rel = "stylesheet";
    style.href = `${ctx.assetBase}/ui/style.css`;
    document.head.appendChild(style);

    container.innerHTML = `
        <header class="explorer-header">
            <nav id="explorer-breadcrumb" class="explorer-breadcrumb"></nav>
            <span id="explorer-stats" class="explorer-stats"></span>
            <span class="explorer-toolbar" id="explorer-toolbar">
                <label role="button" class="secondary explorer-btn">
                    Hochladen<input id="explorer-upload" type="file" multiple hidden>
                </label>
                <button id="explorer-newdir" class="explorer-btn">+ Neu</button>
            </span>
        </header>
        <table class="explorer-table">
            <thead>
                <tr><th class="explorer-col-icon"></th><th>Name</th><th>Größe</th><th>Typ</th><th>Geändert</th><th class="explorer-col-actions"></th></tr>
            </thead>
            <tbody id="explorer-rows"></tbody>
        </table>`;
    const breadcrumb = container.querySelector<HTMLElement>("#explorer-breadcrumb")!;
    const stats = container.querySelector<HTMLElement>("#explorer-stats")!;
    const rows = container.querySelector<HTMLElement>("#explorer-rows")!;
    const toolbar = container.querySelector<HTMLElement>("#explorer-toolbar")!;
    const uploadInput = container.querySelector<HTMLInputElement>("#explorer-upload")!;
    const newDirButton = container.querySelector<HTMLButtonElement>("#explorer-newdir")!;

    let currentPath = "";

    uploadInput.addEventListener("change", async () => {
        try {
            for (const file of uploadInput.files ?? []) {
                await ctx.fs.upload(`${currentPath}/${file.name}`, file);
            }
        } catch (e) {
            alert(`Hochladen fehlgeschlagen: ${e}`);
        }
        uploadInput.value = "";
        load(currentPath);
    });

    newDirButton.addEventListener("click", async () => {
        const name = prompt("Name des neuen Ordners:");
        if (!name) {
            return;
        }
        try {
            await ctx.fs.mkdir(`${currentPath}/${name}`);
        } catch (e) {
            alert(`Anlegen fehlgeschlagen: ${e}`);
        }
        load(currentPath);
    });

    function renderBreadcrumb(path: string): void {
        breadcrumb.innerHTML = "";
        const root = document.createElement("a");
        root.href = "#";
        root.textContent = "/";
        root.addEventListener("click", e => { e.preventDefault(); load(""); });
        breadcrumb.appendChild(root);

        const segments = path === "" ? [] : path.split("/");
        segments.forEach((segment, i) => {
            if (i > 0) {
                breadcrumb.appendChild(document.createTextNode(" / "));
            }
            const target = segments.slice(0, i + 1).join("/");
            const a = document.createElement("a");
            a.href = "#";
            a.textContent = segment;
            a.addEventListener("click", e => { e.preventDefault(); load(target); });
            breadcrumb.appendChild(a);
        });
    }

    function addRow(icon: string, name: HTMLElement, size: string, kind: string, modified: string,
                    deleteTarget: string | null): HTMLTableRowElement {
        const row = document.createElement("tr");
        const iconCell = document.createElement("td");
        iconCell.className = "explorer-col-icon";
        iconCell.innerHTML = icon;
        row.appendChild(iconCell);
        const nameCell = document.createElement("td");
        nameCell.appendChild(name);
        row.appendChild(nameCell);
        for (const text of [size, kind, modified]) {
            const cell = document.createElement("td");
            cell.className = "explorer-col-meta";
            cell.textContent = text;
            row.appendChild(cell);
        }
        const actionCell = document.createElement("td");
        actionCell.className = "explorer-col-actions";
        if (deleteTarget !== null) {
            const button = document.createElement("button");
            button.className = "explorer-delete";
            button.title = "Löschen";
            button.innerHTML = TRASH_ICON;
            button.addEventListener("click", async () => {
                const name = deleteTarget.substring(deleteTarget.lastIndexOf("/") + 1);
                if (!confirm(`„${name}" wirklich löschen?`)) {
                    return;
                }
                try {
                    await ctx.fs.delete(deleteTarget);
                } catch (e) {
                    alert(`Löschen fehlgeschlagen: ${e}`);
                }
                load(currentPath);
            });
            actionCell.appendChild(button);
        }
        row.appendChild(actionCell);
        rows.appendChild(row);
        return row;
    }

    function folderLink(label: string, target: string): HTMLElement {
        const a = document.createElement("a");
        a.href = "#";
        a.textContent = label;
        a.addEventListener("click", e => { e.preventDefault(); load(target); });
        return a;
    }

    async function load(path: string): Promise<void> {
        const entries = await ctx.fs.list(path);
        currentPath = path;
        renderBreadcrumb(path);

        const dirCount = entries.filter(e => e.isDirectory).length;
        stats.textContent = `${entries.length} Objekte · ${dirCount} Ordner`;

        // No uploads/folders/deletes at the volume level
        const atRoot = path === "";
        toolbar.style.display = atRoot ? "none" : "";

        rows.innerHTML = "";
        if (!atRoot) {
            const parent = path.substring(0, path.lastIndexOf("/"));
            addRow(FOLDER_ICON, folderLink("..", parent), "", "Ordner", "", null);
        }
        for (const entry of entries) {
            const target = path ? `${path}/${entry.name}` : entry.name;
            let name: HTMLElement;
            if (entry.isDirectory) {
                name = folderLink(entry.name, target);
            } else {
                const a = document.createElement("a");
                a.href = ctx.fs.downloadUrl(target);
                a.textContent = entry.name;
                name = a;
            }
            addRow(entry.isDirectory ? FOLDER_ICON : FILE_ICON, name,
                formatSize(entry.sizeBytes), entry.kind, formatDate(entry.modifiedUtc),
                atRoot ? null : target);
        }
    }

    load("");

    return () => {
        style.remove();
        container.innerHTML = "";
    };
}

export default { mount };
