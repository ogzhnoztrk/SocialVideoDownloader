const body = document.querySelector("#history-body");

async function loadHistory() {
    const jobs = await api("/api/downloads?take=100");
    body.replaceChildren();
    if (!jobs.length) {
        const row = document.createElement("tr");
        const cell = el("td", "empty", "Henüz indirme yok.");
        cell.colSpan = 5;
        row.append(cell);
        body.append(row);
        return;
    }

    jobs.forEach((job) => {
        const row = document.createElement("tr");
        row.append(
            el("td", null, [job.platformName, job.categoryName, sourceLabel(job.source)].filter(Boolean).join(" · ")),
            cellWith(el("strong", null, job.title), job.fileName ? el("div", "hint", job.fileName) : null),
            el("td", null, formatDate(job.createdAt)),
            statusCell(job),
            actionCell(job));
        body.append(row);
    });
}

function cellWith(...nodes) {
    const cell = document.createElement("td");
    nodes.filter(Boolean).forEach((node) => cell.append(node));
    return cell;
}

function statusCell(job) {
    const cell = document.createElement("td");
    cell.append(el("div", `status status-${job.status}`, statusLabels[job.status] || job.status));
    if (job.errorMessage) cell.append(el("div", "hint", job.errorMessage));
    return cell;
}

function actionCell(job) {
    const cell = document.createElement("td");
    const actions = el("div", "history-actions");
    if (job.canOpenFile) actions.append(actionButton("Dosyayı Aç", () => openJobFile(job)));
    actions.append(actionButton("Klasörü Aç", () => openJobFolder(job)));
    actions.append(actionButton("URL'yi Aç", () => window.open(job.url, "_blank", "noopener")));
    actions.append(actionButton("Tekrar İndir", () => api(`/api/downloads/${job.id}/retry`, { method: "POST" }).then(loadHistory)));
    actions.append(actionButton("Sil", () => removeJob(job.id)));
    cell.append(actions);
    return cell;
}

function actionButton(label, handler) {
    const button = el("button", "btn btn-ghost", label);
    button.type = "button";
    button.addEventListener("click", async () => {
        try {
            await handler();
        } catch (error) {
            notifyError(error);
        }
    });
    return button;
}

async function openJobFile(job) {
    if (await openLocal(job.filePath, true)) return;
    await api(`/api/downloads/${job.id}/open-file`, { method: "POST" });
}

async function openJobFolder(job) {
    const folder = job.filePath ? parentDirectory(job.filePath) : job.outputDirectory;
    if (await openLocal(folder, false)) return;
    await api(`/api/downloads/${job.id}/open-folder`, { method: "POST" });
}

async function removeJob(id) {
    const result = await Swal.fire({
        title: "Kayıt silinsin mi?",
        input: "checkbox",
        inputPlaceholder: "İndirilen dosyayı da sil",
        showCancelButton: true,
        confirmButtonText: "Sil",
        cancelButtonText: "Vazgeç",
    });
    if (!result.isConfirmed) return;
    await api(`/api/downloads/${id}?deleteFile=${result.value === 1}`, { method: "DELETE" });
    await loadHistory();
}

loadHistory();
setInterval(loadHistory, 4000);
