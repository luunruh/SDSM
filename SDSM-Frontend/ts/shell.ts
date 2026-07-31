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
    activity: `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2"/></svg>`,
};

function encodePath(path: string): string {
    return path.split("/").map(encodeURIComponent).join("/");
}

interface AuthStatus {
    authenticated: boolean;
    setupRequired: boolean;
    username: string | null;
}

async function checkOk(op: string, response: Response): Promise<Response> {
    if (response.status === 401) {
        // Session expired — back to the login screen
        location.reload();
    }
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

function showAuthForm(setup: boolean): void {
    const container = document.getElementById("plugin-container")!;
    container.innerHTML = `
        <article class="auth-card">
            <h3>${setup ? "Admin-Konto anlegen" : "Anmelden"}</h3>
            <form id="auth-form">
                <input name="username" placeholder="Benutzername" required
                       autocomplete="${setup ? "off" : "username"}">
                <input name="password" type="password" placeholder="Passwort" required
                       ${setup ? 'minlength="8"' : ""}
                       autocomplete="${setup ? "new-password" : "current-password"}">
                <button type="submit">${setup ? "Anlegen" : "Anmelden"}</button>
                <p id="auth-error" class="auth-error"></p>
            </form>
        </article>`;

    const form = container.querySelector<HTMLFormElement>("#auth-form")!;
    const error = container.querySelector<HTMLElement>("#auth-error")!;
    form.addEventListener("submit", async e => {
        e.preventDefault();
        const data = new FormData(form);
        const response = await fetch(`/api/auth/${setup ? "setup" : "login"}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                username: data.get("username"),
                password: data.get("password"),
            }),
        });
        if (response.ok) {
            initShell();
        } else {
            error.textContent = setup
                ? "Anlegen fehlgeschlagen (Passwort: mindestens 8 Zeichen)."
                : "Benutzername oder Passwort falsch.";
        }
    });
}

function showUser(username: string | null): void {
    const item = document.getElementById("topbar-user")!;
    item.hidden = false;
    document.getElementById("topbar-username")!.textContent = username ?? "";
    document.getElementById("logout-btn")!.addEventListener("click", async e => {
        e.preventDefault();
        await fetch("/api/auth/logout", { method: "POST" });
        location.reload();
    });
}

async function initShell(): Promise<void> {
    const status: AuthStatus = await (await fetch("/api/auth/status")).json();
    if (status.setupRequired || !status.authenticated) {
        showAuthForm(status.setupRequired);
        return;
    }
    showUser(status.username);

    const response = await fetch("/api/plugins");
    const plugins: PluginInfo[] = await response.json();
    const nav = document.getElementById("plugin-nav")!;
    nav.innerHTML = "";

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
