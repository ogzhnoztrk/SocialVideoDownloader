const form = document.querySelector("#settings-form");

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
