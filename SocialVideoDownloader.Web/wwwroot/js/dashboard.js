const urlInput = document.querySelector("#video-url");
const infoPanel = document.querySelector("#info-panel");
const categorySelect = document.querySelector("#download-category");
const fileCategorySelect = document.querySelector("#file-category");
let currentInfo = null;
let categories = [];

document.querySelector("#info-button").addEventListener("click", loadInfo);
document.querySelector("#import-file").addEventListener("click", importFile);
document.querySelector("#tab-link").addEventListener("click", () => showPane("link"));
document.querySelector("#tab-file").addEventListener("click", () => showPane("file"));
document.querySelector("#download-button").addEventListener("click", startDownload);
document.querySelector("#change-folder").addEventListener("click", changeFolder);
urlInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter") loadInfo();
});
categorySelect.addEventListener("change", () => {
    localStorage.setItem("svd-category", categorySelect.value);
    if (currentInfo) text("#info-folder", previewFolder(currentInfo.downloadDirectory, currentInfo.platformName));
});
fileCategorySelect.addEventListener("change", () => {
    localStorage.setItem("svd-file-category", fileCategorySelect.value);
});

async function loadInfo() {
    try {
        currentInfo = await api("/api/videos/info", {
            method: "POST",
            body: JSON.stringify({ url: urlInput.value.trim() }),
        });
        renderInfo(currentInfo);
    } catch (error) {
        notifyError(error);
    }
}

function renderInfo(info) {
    infoPanel.classList.remove("d-none");
    text("#info-platform", info.platformName);
    text("#info-title", info.title);
    text("#info-uploader", info.uploader);
    text("#info-duration", formatDuration(info.durationSeconds));
    text("#info-resolution", info.resolution || "Bilinmiyor");
    text("#info-format", info.format || "MP4");
    text("#info-folder", previewFolder(info.downloadDirectory, info.platformName));
    const thumb = document.querySelector("#info-thumb");
    if (info.thumbnailUrl) {
        thumb.src = info.thumbnailUrl;
        thumb.classList.remove("d-none");
    } else {
        thumb.classList.add("d-none");
    }
}

async function importFile() {
    const input = document.querySelector("#url-file");
    const file = input.files && input.files[0];
    if (!file) {
        notifyError(new Error("TXT dosyası seçin."));
        return;
    }

    const category = categories.find((item) => String(item.id) === fileCategorySelect.value);
    if (!category) {
        notifyError(new Error("TXT için bir kategori seçin."));
        return;
    }

    try {
        const body = new FormData();
        body.append("file", file);
        body.append("categoryId", String(category.id));
        Swal.fire({ title: "Liste kuyruğa alınıyor", allowOutsideClick: false, didOpen: () => Swal.showLoading() });
        const response = await fetch("/api/downloads/import", {
            method: "POST",
            headers: { "X-SVD-Request": "1" },
            body,
        });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || "İşlem başarısız.");
        Swal.close();
        input.value = "";
        await Swal.fire({
            icon: data.added > 0 ? "success" : "info",
            title: data.added > 0 ? "Liste kuyruğa alındı" : "Yeni video yok",
            text: `${data.message || ""} Kategori: ${category.name}`.trim(),
            confirmButtonText: "Tamam",
        });
        await refresh();
    } catch (error) {
        Swal.close();
        notifyError(error);
    }
}

async function startDownload() {
    try {
        const created = await api("/api/downloads", {
            method: "POST",
            body: JSON.stringify({
                url: urlInput.value.trim(),
                categoryId: Number(categorySelect.value) || null,
            }),
        });
        if (created.alreadyExists) {
            await Swal.fire({ icon: "info", title: "Bu video daha önce indirilmiş.", confirmButtonText: "Tamam" });
        }
        await refresh();
    } catch (error) {
        notifyError(error);
    }
}

async function changeFolder() {
    const settings = await api("/api/settings");
    const folder = await askForFolder(settings.downloadDirectory);
    if (!folder) return;
    settings.downloadDirectory = folder;
    const saved = await api("/api/settings", { method: "PUT", body: JSON.stringify(settings) });
    text("#info-folder", previewFolder(saved.settings.downloadDirectory, currentInfo?.platformName));
}

async function refresh() {
    const [stats, recent, status] = await Promise.all([
        api("/api/dashboard"),
        api("/api/downloads?take=8"),
        api("/api/status"),
    ]);
    renderStats(stats);
    renderRecent(recent);
    renderGist(status.gist, categories.find((item) => item.isDefault));
    const banner = document.querySelector("#tool-banner");
    if (!status.ytDlpAvailable) {
        banner.classList.remove("d-none");
        banner.textContent = "yt-dlp bulunamadı. Ayarlar > Downloader bölümünden kurulum yapabilirsiniz.";
    } else {
        banner.classList.add("d-none");
    }
}

function renderStats(stats) {
    const root = document.querySelector("#stats");
    root.replaceChildren();
    [
        [stats.total, "Toplam"],
        [stats.today, "Bugün"],
        [stats.successful, "Başarılı"],
        [stats.failed, "Başarısız"],
    ].forEach(([value, label]) => {
        const card = el("article", "stat-card");
        card.append(el("strong", null, String(value)), el("span", null, label));
        root.append(card);
    });
}

function renderRecent(jobs) {
    const root = document.querySelector("#recent-list");
    root.replaceChildren();
    if (!jobs.length) {
        root.append(el("div", "empty", "Henüz indirme yok."));
        return;
    }

    jobs.forEach((job) => {
        const card = el("article", "download-card");
        const label = [job.platformName, job.categoryName, sourceLabel(job.source)]
            .filter(Boolean)
            .join(" · ");
        card.append(el("div", "section-label", label), el("strong", null, job.title));
        if (job.fileName) card.append(el("div", null, job.fileName));
        if (job.status === "downloading" || job.status === "pending") {
            const bar = el("div", "progress");
            const fill = document.createElement("span");
            fill.style.width = `${Math.max(0, Math.min(100, job.progress || 0))}%`;
            bar.append(fill);
            card.append(bar);
            const sizes = job.bytesDownloaded != null && job.totalBytes
                ? `${formatBytes(job.bytesDownloaded)} / ${formatBytes(job.totalBytes)}`
                : "";
            card.append(el("div", null, `${Math.round(job.progress || 0)}% ${sizes}`.trim()));
        }
        card.append(el("div", `status status-${job.status}`, statusLabels[job.status] || job.status));
        if (job.errorMessage) card.append(el("div", "hint", job.errorMessage));
        if (job.status === "downloading" || job.status === "pending") {
            const button = el("button", "btn btn-ghost", "İptal");
            button.type = "button";
            button.addEventListener("click", async () => {
                await api(`/api/downloads/${job.id}/cancel`, { method: "POST" });
                refresh();
            });
            card.append(button);
        }
        root.append(card);
    });
}

function previewFolder(root, platformName) {
    const platform = platformFolder(platformName);
    const category = selectedCategoryName();
    if (!root) return category;
    return platform ? `${root}\\${category}\\${platform}` : `${root}\\${category}`;
}

function platformFolder(name) {
    if (name === "Instagram") return "Instagram";
    if (name === "TikTok") return "TikTok";
    if (name === "Twitter" || name === "X") return "Twitter";
    if (!name) return "";
    return "Other";
}

function selectedCategoryName() {
    const selected = categories.find((item) => String(item.id) === categorySelect.value);
    return selected?.name || "Genel";
}

function showPane(name) {
    const link = name === "link";
    document.querySelector("#pane-link").classList.toggle("d-none", !link);
    document.querySelector("#pane-file").classList.toggle("d-none", link);
    document.querySelector("#tab-link").classList.toggle("is-active", link);
    document.querySelector("#tab-file").classList.toggle("is-active", !link);
    document.querySelector("#tab-link").setAttribute("aria-selected", link ? "true" : "false");
    document.querySelector("#tab-file").setAttribute("aria-selected", link ? "false" : "true");
    if (!link) infoPanel.classList.add("d-none");
    else if (currentInfo) infoPanel.classList.remove("d-none");
}

function fillCategorySelect(select, savedId) {
    const current = savedId || select.value;
    select.replaceChildren();
    categories.forEach((item) => {
        const option = document.createElement("option");
        option.value = String(item.id);
        option.textContent = item.isDefault ? `${item.name} (varsayılan)` : item.name;
        select.append(option);
    });
    const preferred = categories.find((item) => String(item.id) === current)
        || categories.find((item) => item.isDefault)
        || categories[0];
    if (preferred) select.value = String(preferred.id);
}

async function loadCategories() {
    categories = await api("/api/categories");
    fillCategorySelect(categorySelect, categorySelect.value || localStorage.getItem("svd-category"));
    fillCategorySelect(fileCategorySelect, fileCategorySelect.value || localStorage.getItem("svd-file-category"));
}

function renderGist(gist, defaultCategory) {
    const root = document.querySelector("#gist-status");
    if (!gist) {
        root.textContent = "Durum: Kapalı";
        return;
    }

    const lines = [`Durum: ${gist.status || "Kapalı"}`];
    lines.push(`Son kontrol: ${gist.lastCheckedAt ? formatDate(gist.lastCheckedAt) : "henüz yok"}`);
    if (gist.enabled && gist.nextCheckAt)
        lines.push(`Sonraki kontrol: ${formatDate(gist.nextCheckAt)}`);
    if (gist.status === "Hata")
        lines.push(`Hata: ${gist.message || "Gist dosyasına erişilemedi"}`);
    else if (gist.message)
        lines.push(`Son kontrol sonucu: ${gist.message}`);
    if (defaultCategory)
        lines.push(`Varsayılan kategori: ${defaultCategory.name}`);
    root.style.whiteSpace = "pre-line";
    root.textContent = lines.join("\n");
}

function text(selector, value) {
    document.querySelector(selector).textContent = value || "";
}

loadCategories().then(refresh).catch(notifyError);
setInterval(refresh, 2000);
