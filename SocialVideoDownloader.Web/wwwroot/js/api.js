async function api(url, options = {}) {
    const headers = { "X-SVD-Request": "1", ...(options.headers || {}) };
    if (options.body) headers["Content-Type"] = "application/json";
    const response = await fetch(url, { ...options, headers });
    if (response.status === 204) return null;
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
        const error = new Error(data.message || "İşlem başarısız.");
        error.status = response.status;
        error.data = data;
        throw error;
    }
    return data;
}

function notifyError(error) {
    return Swal.fire({
        icon: "error",
        title: "İşlem tamamlanamadı",
        text: error.message || "Beklenmeyen bir hata oluştu.",
        confirmButtonText: "Tamam",
    });
}

function formatBytes(value) {
    if (value == null) return "";
    const units = ["B", "KB", "MB", "GB"];
    let size = Number(value);
    let index = 0;
    while (size >= 1024 && index < units.length - 1) {
        size /= 1024;
        index += 1;
    }
    return `${size.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

function formatDuration(seconds) {
    if (seconds == null) return "Bilinmiyor";
    const total = Math.max(0, Math.floor(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const secs = total % 60;
    const pad = (value) => String(value).padStart(2, "0");
    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(secs)}` : `${pad(minutes)}:${pad(secs)}`;
}

function formatDate(value) {
    return new Intl.DateTimeFormat("tr-TR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}

function sourceLabel(source) {
    if (source === "gist") return "Gist";
    if (source === "file") return "Dosya";
    return "";
}

const statusLabels = {
    pending: "Bekliyor",
    downloading: "İndiriliyor...",
    completed: "Tamamlandı",
    failed: "Başarısız",
    cancelled: "İptal edildi",
};

function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
}

function parentDirectory(path) {
    if (!path) return "";
    const index = Math.max(path.lastIndexOf("\\"), path.lastIndexOf("/"));
    return index > 0 ? path.slice(0, index) : path;
}

async function openLocal(path, select) {
    if (!path) throw new Error("Klasör bulunamadı.");
    try {
        const response = await fetch("http://127.0.0.1:5181/open-path", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ path, select: !!select }),
        });
        if (response.ok) return true;
        const data = await response.json().catch(() => ({}));
        if (response.status === 404) return false;
        throw new Error(data.message || "Klasör açılamadı.");
    } catch (error) {
        if (error instanceof TypeError) return false;
        throw error;
    }
}

async function browseFolder() {
    try {
        const response = await fetch("http://127.0.0.1:5181/browse-folder", { method: "POST" });
        if (!response.ok) return null;
        const data = await response.json();
        return data.path || null;
    } catch {
        return null;
    }
}

async function askForFolder(current) {
    const picked = await browseFolder();
    if (picked) return picked;
    const result = await Swal.fire({
        title: "İndirme klasörü",
        input: "text",
        inputValue: current || "",
        showCancelButton: true,
        confirmButtonText: "Kaydet",
        cancelButtonText: "Vazgeç",
    });
    return result.isConfirmed ? result.value : null;
}
