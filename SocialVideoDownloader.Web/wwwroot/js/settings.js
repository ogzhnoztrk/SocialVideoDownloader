const form = document.querySelector("#settings-form");
let savedGistUrl = "";

async function loadSettings() {
    const [settings, binaries, service] = await Promise.all([
        api("/api/settings"),
        api("/api/binaries"),
        api("/api/system/service"),
    ]);
    document.querySelector("#download-directory").value = settings.downloadDirectory;
    document.querySelector("#file-template").value = settings.fileNameTemplate;
    document.querySelector("#use-date-folders").checked = settings.useDateFolders;
    document.querySelector("#duplicate-check").checked = settings.duplicateCheckEnabled;
    document.querySelector("#ytdlp-path").value = settings.ytDlpPath || "";
    document.querySelector("#ffmpeg-path").value = settings.ffmpegPath || "";
    document.querySelector("#auto-update").checked = settings.autoUpdateBinaries;
    document.querySelector("#listen-address").value = settings.listenAddress || "127.0.0.1";
    document.querySelector("#web-port").value = settings.webPort;
    document.querySelector("#open-on-startup").checked = settings.openWebOnStartup;
    document.querySelector("#start-with-windows").checked = settings.startWithWindows;
    savedGistUrl = settings.gistUrl || "";
    document.querySelector("#gist-enabled").checked = !!settings.gistPollingEnabled;

    const binary = document.querySelector("#binary-status");
    const yt = binaries.ytDlpInstalled
        ? `yt-dlp ${binaries.ytDlpVersion || ""} ${binaries.ytDlpUpdateAvailable ? "(güncelleme var)" : ""}`.trim()
        : "yt-dlp kurulu değil";
    const ffmpeg = binaries.ffmpegInstalled ? "FFmpeg hazır" : "FFmpeg kurulu değil";
    binary.textContent = `${yt}. ${ffmpeg}.`;
    document.querySelector("#service-status").textContent = service.installed
        ? `Windows Service durumu: ${service.status}`
        : "Windows Service durumu: kurulu değil";
}

form.addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
        const saved = await api("/api/settings", {
            method: "PUT",
            body: JSON.stringify(readForm()),
        });
        if (saved.warning) {
            await Swal.fire({ icon: "info", title: "Kaydedildi", text: saved.warning, confirmButtonText: "Tamam" });
        } else {
            await Swal.fire({ icon: "success", title: "Ayarlar kaydedildi", confirmButtonText: "Tamam" });
        }
        await loadSettings();
    } catch (error) {
        notifyError(error);
    }
});

document.querySelector("#pick-folder").addEventListener("click", async () => {
    const input = document.querySelector("#download-directory");
    const folder = await askForFolder(input.value);
    if (folder) input.value = folder;
});

document.querySelector("#install-ytdlp").addEventListener("click", () => installBinary("/api/binaries/yt-dlp/install", "yt-dlp kuruluyor"));
document.querySelector("#install-ffmpeg").addEventListener("click", () => installBinary("/api/binaries/ffmpeg/install", "FFmpeg kuruluyor"));
document.querySelector("#install-service").addEventListener("click", async () => {
    try {
        const result = await api("/api/system/service/install", { method: "POST" });
        await Swal.fire({ icon: "info", title: result.message, confirmButtonText: "Tamam" });
    } catch (error) {
        notifyError(error);
    }
});
document.querySelector("#restart-service").addEventListener("click", async () => {
    try {
        const response = await fetch("http://127.0.0.1:5181/restart-service", { method: "POST" });
        const data = await response.json();
        await Swal.fire({ icon: data.ok ? "success" : "info", title: data.message, confirmButtonText: "Tamam" });
    } catch {
        notifyError(new Error("Tepsi uygulaması çalışmıyor. Servisi oradan yeniden başlatın."));
    }
});
document.querySelector("#gist-check").addEventListener("click", async () => {
    try {
        const result = await api("/api/gist/check", { method: "POST" });
        await Swal.fire({
            icon: result.succeeded ? "success" : "error",
            title: result.succeeded ? "Gist kontrol edildi" : "Gist kontrolü başarısız",
            text: result.message || "",
            confirmButtonText: "Tamam",
        });
    } catch (error) {
        notifyError(error);
    }
});
document.querySelector("#open-root").addEventListener("click", async () => {
    try {
        const path = document.querySelector("#download-directory").value.trim();
        if (await openLocal(path, false)) return;
        await api("/api/system/open-download-folder", { method: "POST" });
    } catch (error) {
        notifyError(error);
    }
});

function readForm() {
    return {
        downloadDirectory: document.querySelector("#download-directory").value.trim(),
        fileNameTemplate: document.querySelector("#file-template").value.trim(),
        useDateFolders: document.querySelector("#use-date-folders").checked,
        duplicateCheckEnabled: document.querySelector("#duplicate-check").checked,
        ytDlpPath: document.querySelector("#ytdlp-path").value.trim(),
        ffmpegPath: document.querySelector("#ffmpeg-path").value.trim(),
        autoUpdateBinaries: document.querySelector("#auto-update").checked,
        listenAddress: document.querySelector("#listen-address").value.trim(),
        webPort: Number(document.querySelector("#web-port").value),
        openWebOnStartup: document.querySelector("#open-on-startup").checked,
        startWithWindows: document.querySelector("#start-with-windows").checked,
        gistUrl: savedGistUrl,
        gistPollingEnabled: document.querySelector("#gist-enabled").checked,
        gistPollIntervalMinutes: 60,
    };
}

async function installBinary(url, title) {
    Swal.fire({ title, allowOutsideClick: false, didOpen: () => Swal.showLoading() });
    try {
        await api(url, { method: "POST" });
        Swal.close();
        await Swal.fire({ icon: "success", title: "Kurulum tamamlandı", confirmButtonText: "Tamam" });
        await loadSettings();
    } catch (error) {
        Swal.close();
        notifyError(error);
    }
}

loadSettings().catch(notifyError);
loadCategories().catch(notifyError);

document.querySelector("#add-category").addEventListener("click", addCategory);
document.querySelector("#category-name").addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
        event.preventDefault();
        addCategory();
    }
});

async function loadCategories() {
    const items = await api("/api/categories");
    const root = document.querySelector("#category-list");
    root.replaceChildren();
    items.forEach((item) => {
        const block = el("div", "category-block");
        const row = el("div", "category-row");
        const title = el("strong", null, item.name);
        row.append(title);
        if (item.isDefault) row.append(el("span", "hint", "Varsayılan"));
        const actions = el("div", "action-row");
        actions.style.marginTop = "0";
        if (!item.isDefault) {
            const makeDefault = el("button", "btn btn-ghost", "Varsayılan yap");
            makeDefault.type = "button";
            makeDefault.addEventListener("click", async () => {
                await api(`/api/categories/${item.id}/default`, { method: "POST" });
                await loadCategories();
            });
            actions.append(makeDefault);
        }
        const remove = el("button", "btn btn-ghost", "Sil");
        remove.type = "button";
        remove.addEventListener("click", async () => {
            const confirmed = await Swal.fire({
                icon: "warning",
                title: "Kategori silinsin mi?",
                text: "İndirilmiş dosyalar klasörde kalır. Yeni indirmeler bu adı kullanmaz.",
                showCancelButton: true,
                confirmButtonText: "Sil",
                cancelButtonText: "Vazgeç",
            });
            if (!confirmed.isConfirmed) return;
            try {
                await api(`/api/categories/${item.id}`, { method: "DELETE" });
                await loadCategories();
            } catch (error) {
                notifyError(error);
            }
        });
        actions.append(remove);
        row.append(actions);
        block.append(row);

        const gistRow = el("div", "inline-field category-gist");
        const gist = document.createElement("input");
        gist.type = "url";
        gist.placeholder = "https://gist.github.com/kullanici/xxxxxxxx";
        gist.value = item.gistUrl || "";
        gist.setAttribute("aria-label", `${item.name} gist adresi`);
        const save = el("button", "btn btn-ghost", "Gist kaydet");
        save.type = "button";
        save.addEventListener("click", async () => {
            try {
                await api(`/api/categories/${item.id}/gist`, {
                    method: "PUT",
                    body: JSON.stringify({ gistUrl: gist.value.trim() }),
                });
                await loadCategories();
            } catch (error) {
                notifyError(error);
            }
        });
        gistRow.append(gist, save);
        block.append(gistRow);
        root.append(block);
    });
}

async function addCategory() {
    const input = document.querySelector("#category-name");
    try {
        await api("/api/categories", {
            method: "POST",
            body: JSON.stringify({ name: input.value.trim() }),
        });
        input.value = "";
        await loadCategories();
    } catch (error) {
        notifyError(error);
    }
}
