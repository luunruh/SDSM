import type { FileSystemEntry } from "./file_system_entry";

let currentPath = "";

async function loadFiles(path: string): Promise<void> {
    currentPath = path;

    document.getElementById("current-path")!.textContent = "/" + path;

    const response = await fetch(`/files/${path}`);
    const entries: FileSystemEntry[] = await response.json();

    const table = document.getElementById("file-table")!;
    table.innerHTML = "";
    const tableHeader = document.createElement("tr");
    const type = document.createElement("th");
    type.textContent = "Type";
    type.style.width = "4em";
    const name = document.createElement("th");
    name.textContent = "Name";
    tableHeader.appendChild(type);
    tableHeader.appendChild(name);
    table.appendChild(tableHeader);

    if (path !== "") {
        const row = document.createElement("tr");
        table.appendChild(row);
        const typeValue = document.createElement("td");
        typeValue.textContent = "d";
        row.appendChild(typeValue);
        const nameValue = document.createElement("td");
        nameValue.textContent = "..";
        nameValue.style.cursor = "pointer";
        row.appendChild(nameValue);

        nameValue.addEventListener("click", () => {
            const parent = path.substring(0, path.lastIndexOf("/"));
            loadFiles(parent);
        });
    }

    for (const entry of entries) {
        const row = document.createElement("tr");
        table.appendChild(row);
        const typeValue = document.createElement("td");
        typeValue.textContent = `${entry.isDirectory ? "d" : "f"}`;
        row.appendChild(typeValue);
        const nameValue = document.createElement("td");
        row.appendChild(nameValue);;

        if (entry.isDirectory) {
            nameValue.style.cursor = "pointer"
            nameValue.textContent = entry.name;
            nameValue.addEventListener("click", () => {
                loadFiles(path ? `${path}/${entry.name}` : entry.name);
            });
        } else {
            const a = document.createElement("a");
            a.href = "downloadfile";
            if (currentPath != "") {
                a.href += "/" + currentPath;
            }
            a.href += "/" + entry.name;
            a.textContent = entry.name;
            nameValue.appendChild(a);
        }

        table.appendChild(row);
    }
}

// Load root on startup
loadFiles("");
