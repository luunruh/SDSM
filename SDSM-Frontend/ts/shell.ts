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

interface SdsmPluginUi {
    mount(container: HTMLElement, ctx: PluginUiContext): void | (() => void);
}

interface PluginInfo {
    id: string;
    name: string;
    version: string;
    ui: string | null;
    nav: { title: string; icon: string | null } | null;
}

const NAV_ICONS: Record<string, string> = {
    folder: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></svg>`,
};

function encodePath(path: string): string {
    return path.split("/").map(encodeURIComponent).join("/");
}

async function checkOk(op: string, response: Response): Promise<Response> {
    if (!response.ok) {
        throw new Error(`${op} failed: ${response.status}`);
    }
    return response;
}

function makeContext(pluginId: string): PluginUiContext {
    return {
        pluginBase: `/api/plugins/${pluginId}`,
        assetBase: `/plugins/${pluginId}`,
        fs: {
            async list(path: string): Promise<FileSystemEntry[]> {
                const url = path === "" ? "/api/fs/list" : `/api/fs/list/${encodePath(path)}`;
                return (await checkOk(`fs.list(${path})`, await fetch(url))).json();
            },
            downloadUrl(path: string): string {
                return `/api/fs/download/${encodePath(path)}`;
            },
            async mkdir(path: string): Promise<void> {
                await checkOk(`fs.mkdir(${path})`,
                    await fetch(`/api/fs/mkdir/${encodePath(path)}`, { method: "POST" }));
            },
            async upload(path: string, content: Blob): Promise<void> {
                await checkOk(`fs.upload(${path})`,
                    await fetch(`/api/fs/upload/${encodePath(path)}`, { method: "PUT", body: content }));
            },
            async delete(path: string): Promise<void> {
                await checkOk(`fs.delete(${path})`,
                    await fetch(`/api/fs/delete/${encodePath(path)}`, { method: "DELETE" }));
            },
            async rename(path: string, newName: string): Promise<void> {
                await checkOk(`fs.rename(${path})`,
                    await fetch(`/api/fs/rename/${encodePath(path)}`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ newName }),
                    }));
            },
        },
    };
}

let cleanup: (() => void) | null = null;

async function activate(plugin: PluginInfo, navLink: HTMLElement): Promise<void> {
    const module: { default: SdsmPluginUi } = await import(plugin.ui!);

    cleanup?.();
    cleanup = null;
    document.querySelectorAll("#plugin-nav a").forEach(a => a.classList.remove("active"));
    navLink.classList.add("active");

    const container = document.getElementById("plugin-container")!;
    container.innerHTML = "";
    const result = module.default.mount(container, makeContext(plugin.id));
    if (typeof result === "function") {
        cleanup = result;
    }
}

async function initShell(): Promise<void> {
    const response = await fetch("/api/plugins");
    const plugins: PluginInfo[] = await response.json();
    const nav = document.getElementById("plugin-nav")!;

    let first = true;
    for (const plugin of plugins.filter(p => p.ui !== null)) {
        const item = document.createElement("li");
        const link = document.createElement("a");
        link.href = "#";
        link.innerHTML = NAV_ICONS[plugin.nav?.icon ?? ""] ?? "";
        link.appendChild(document.createTextNode(plugin.nav?.title ?? plugin.name));
        link.addEventListener("click", e => {
            e.preventDefault();
            activate(plugin, link);
        });
        item.appendChild(link);
        nav.appendChild(item);

        if (first) {
            first = false;
            activate(plugin, link);
        }
    }
}

initShell();
