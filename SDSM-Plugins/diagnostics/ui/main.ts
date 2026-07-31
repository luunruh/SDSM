// Keep in sync with the contract in docs/ARCHITECTURE.md
interface PluginUiContext {
    pluginBase: string;
    assetBase: string;
}

interface VolumeInfo {
    name: string;
    totalBytes: number;
    availableBytes: number;
}

interface Stats {
    volumes: VolumeInfo[];
    uptimeSeconds: number;
    loadAverage: number[];
}

function formatBytes(bytes: number): string {
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

function formatUptime(seconds: number): string {
    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return days > 0 ? `${days}d ${hours}h ${minutes}m` : `${hours}h ${minutes}m`;
}

function mount(container: HTMLElement, ctx: PluginUiContext): () => void {
    const style = document.createElement("link");
    style.rel = "stylesheet";
    style.href = `${ctx.assetBase}/ui/style.css`;
    document.head.appendChild(style);

    container.innerHTML = `
        <header class="diag-header">
            <strong>System</strong>
            <span id="diag-info" class="diag-info"></span>
        </header>
        <div id="diag-volumes" class="diag-volumes"></div>`;
    const info = container.querySelector<HTMLElement>("#diag-info")!;
    const volumesDiv = container.querySelector<HTMLElement>("#diag-volumes")!;

    async function refresh(): Promise<void> {
        const response = await fetch(`${ctx.pluginBase}/stats`);
        if (!response.ok) {
            info.textContent = `Statusabfrage fehlgeschlagen (${response.status})`;
            return;
        }
        const stats: Stats = await response.json();

        const load = stats.loadAverage.map(l => l.toFixed(2)).join(" ");
        info.textContent = `Uptime ${formatUptime(stats.uptimeSeconds)}`
            + (load ? ` · Load ${load}` : "");

        volumesDiv.innerHTML = "";
        for (const volume of stats.volumes) {
            const used = volume.totalBytes - volume.availableBytes;
            const percent = volume.totalBytes > 0 ? Math.round(used / volume.totalBytes * 100) : 0;

            const card = document.createElement("article");
            card.className = "diag-volume";
            const label = document.createElement("div");
            label.className = "diag-volume-label";
            const name = document.createElement("span");
            name.textContent = volume.name;
            const percentText = document.createElement("span");
            percentText.className = "diag-volume-percent";
            percentText.textContent = `${formatBytes(used)} / ${formatBytes(volume.totalBytes)} · ${percent}%`;
            label.append(name, percentText);
            const bar = document.createElement("progress");
            bar.max = 100;
            bar.value = percent;
            card.append(label, bar);
            volumesDiv.appendChild(card);
        }
    }

    refresh();
    const timer = setInterval(refresh, 5000);

    return () => {
        clearInterval(timer);
        style.remove();
        container.innerHTML = "";
    };
}

export default { mount };
