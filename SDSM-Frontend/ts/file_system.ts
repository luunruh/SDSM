import type { FileSystemEntry } from "./file_system_entry";

let currentPath = "";

async function loadFiles(path: string): Promise<void> {
    currentPath = path;

    document.getElementById("current-path")!.textContent = "/" + path;

    const response = await fetch(`/files/${path}`);
    const entries: FileSystemEntry[] = await response.json();

    const list = document.getElementById("file-list")!;
    list.innerHTML = "";

    if (path !== "") {
        const up = document.createElement("li");
        up.textContent = "d ..";
        up.style.cursor = "pointer";
        up.addEventListener("click", () => {
            const parent = path.substring(0, path.lastIndexOf("/"));
            loadFiles(parent);
        });
        list.appendChild(up);
    }

    for (const entry of entries) {
        const li = document.createElement("li");
        li.textContent = `${entry.isDirectory ? "d" : "f"} ${entry.name}`;

        if (entry.isDirectory) {
            li.style.cursor = "pointer";
            li.addEventListener("click", () => {
                loadFiles(path ? `${path}/${entry.name}` : entry.name);
            });
        }

        list.appendChild(li);
    }
}

// Load root on startup
loadFiles("");
