using System.Diagnostics;
using System.Text.Json;
using SocialVideoDownloader.Core.Constants;

namespace SocialVideoDownloader.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly HttpClient _gistHttp = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly Icon _runningIcon = StatusIcon.Create(Color.FromArgb(15, 110, 86));
    private readonly Icon _stoppedIcon = StatusIcon.Create(Color.FromArgb(159, 45, 45));
    private readonly ToolStripMenuItem _statusItem = new("Durum: Durdu") { Enabled = false };
    private readonly ToolStripMenuItem _downloadItem = new("İndirme: 0") { Enabled = false };
    private readonly ToolStripMenuItem _gistItem = new("Gist: Kapalı") { Enabled = false };
    private TrayHelperServer? _helper;
    private bool _openedWeb;
    private int _port = AppConstants.DefaultPort;
    private string? _downloadDirectory;

    public TrayApplicationContext()
    {
        try
        {
            _helper = new TrayHelperServer(BrowseFolder, RestartService, OpenPath);
        }
        catch (Exception)
        {
            _helper = null;
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Web Panelini Aç", null, (_, _) => OpenWeb());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(_downloadItem);
        menu.Items.Add(_gistItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("İndirme Klasörünü Aç", null, (_, _) => OpenFolder());
        menu.Items.Add("Gist'i Şimdi Kontrol Et", null, (_, _) => _ = CheckGistAsync());
        menu.Items.Add("Ayarlar", null, (_, _) => OpenWeb("/Settings"));
        menu.Items.Add("Servisi Yeniden Başlat", null, (_, _) => RestartService());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = _stoppedIcon,
            Visible = true,
            Text = "Social Video Downloader",
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenWeb();
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _port = ReadConfiguredPort();
        _ = StartAsync();
    }

    private static int ReadConfiguredPort()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SocialVideoDownloader",
                "host-settings.json");
            if (!File.Exists(path))
                return AppConstants.DefaultPort;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("webPort", out var port) &&
                port.TryGetInt32(out var value) &&
                value is >= 1024 and <= 65535)
                return value;
        }
        catch (Exception)
        {
        }

        return AppConstants.DefaultPort;
    }

    private async Task StartAsync()
    {
        try
        {
            if (!await IsWebReachableAsync())
            {
                var startedWeb = false;
                for (var attempt = 0; attempt < 30 && !await IsWebReachableAsync(); attempt++)
                {
                    if (!startedWeb && attempt == 4 && !WebLaunchAlreadyRequested())
                    {
                        StartWebProcess();
                        startedWeb = true;
                    }

                    await Task.Delay(500);
                }
            }
        }
        catch (Exception)
        {
        }

        await RefreshAsync();
    }

    private async Task<bool> IsWebReachableAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"http://127.0.0.1:{_port}/api/status");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool WebLaunchAlreadyRequested() =>
        string.Equals(Environment.GetEnvironmentVariable("SVD_WEB_LAUNCHED"), "1", StringComparison.Ordinal) ||
        Process.GetProcessesByName("SocialVideoDownloader.Web").Length > 0;

    private static void StartWebProcess()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "SocialVideoDownloader.Web.exe");
        var published = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "service", "SocialVideoDownloader.Web.exe"));
        var exe = File.Exists(beside) ? beside : File.Exists(published) ? published : null;
        if (exe is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return;
        }

        var project = FindWebProject();
        if (project is null)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{project}\"",
            WorkingDirectory = Path.GetDirectoryName(project)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static string? FindWebProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            var project = Path.Combine(dir.FullName, "SocialVideoDownloader.Web", "SocialVideoDownloader.Web.csproj");
            if (File.Exists(project))
                return project;
        }

        return null;
    }

    private string? BrowseFolder()
    {
        string? selected = null;
        var thread = new Thread(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "İndirme klasörünü seçin",
                UseDescriptionForTitle = true,
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                selected = dialog.SelectedPath;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return selected;
    }

    private void RestartService()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Restart-Service -Name SocialVideoDownloader -ErrorAction Stop\"",
                UseShellExecute = true,
                Verb = "runas",
            });
            process?.WaitForExit();
            if (process is null || process.ExitCode != 0)
                MessageBox.Show("Servis yeniden başlatılamadı. Servis kurulu olmayabilir.", "Social Video Downloader");
        }
        catch (Exception)
        {
            MessageBox.Show("Servis yeniden başlatma iptal edildi veya başarısız oldu.", "Social Video Downloader");
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"http://127.0.0.1:{_port}/api/status");
            if (!response.IsSuccessStatusCode && _port != AppConstants.DefaultPort)
            {
                using var fallback = await _http.GetAsync($"http://127.0.0.1:{AppConstants.DefaultPort}/api/status");
                if (!fallback.IsSuccessStatusCode)
                    throw new HttpRequestException();
                await ApplyStatus(fallback);
                return;
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException();

            await ApplyStatus(response);
        }
        catch (Exception)
        {
            _icon.Icon = _stoppedIcon;
            _statusItem.Text = "Durum: Durdu";
            _downloadItem.Text = "İndirme: 0";
            _gistItem.Text = "Gist: Kapalı";
            _icon.Text = "Social Video Downloader - Durdu";
        }
    }

    private async Task ApplyStatus(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        _port = root.TryGetProperty("port", out var port) && port.TryGetInt32(out var value) ? value : _port;
        var active = root.TryGetProperty("activeDownloads", out var downloads) ? downloads.GetInt32() : 0;
        _downloadDirectory = root.TryGetProperty("downloadDirectory", out var directory) ? directory.GetString() : null;
        var openWeb = root.TryGetProperty("openWebOnStartup", out var open) && open.GetBoolean();
        var startWithWindows = root.TryGetProperty("startWithWindows", out var startup) && startup.GetBoolean();
        StartupRegistration.Apply(startWithWindows, Application.ExecutablePath);

        _icon.Icon = _runningIcon;
        _statusItem.Text = "Durum: Çalışıyor";
        _downloadItem.Text = $"İndirme: {active}";
        _gistItem.Text = GistMenuText(root);
        _icon.Text = $"Social Video Downloader - Çalışıyor ({active})";
        if (openWeb && !_openedWeb)
        {
            _openedWeb = true;
            OpenWeb();
        }
    }

    private static string GistMenuText(JsonElement root)
    {
        if (!root.TryGetProperty("gist", out var gist))
            return "Gist: Kapalı";

        var status = gist.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : "Kapalı";
        var mark = status == "Aktif" ? "🟢" : status == "Hata" ? "🔴" : "⚪";
        var last = "—";
        if (gist.TryGetProperty("lastCheckedAt", out var checkedAt) &&
            checkedAt.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(checkedAt.GetString(), out var time))
            last = time.ToLocalTime().ToString("HH:mm");

        return $"Gist: {mark} {status}, Son kontrol: {last}";
    }

    private async Task CheckGistAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_port}/api/gist/check");
            request.Headers.TryAddWithoutValidation("X-SVD-Request", "1");
            using var response = await _gistHttp.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var message = document.RootElement.TryGetProperty("message", out var value)
                ? value.GetString()
                : "Gist kontrolü tamamlandı.";
            MessageBox.Show(message ?? "Gist kontrolü tamamlandı.", "Social Video Downloader");
        }
        catch (Exception)
        {
            MessageBox.Show("Gist kontrolü gönderilemedi. Program çalışıyor mu?", "Social Video Downloader");
        }
    }

    private void OpenWeb(string path = "/")
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"http://localhost:{_port}{path}",
            UseShellExecute = true,
        });
    }

    private void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(_downloadDirectory))
        {
            MessageBox.Show("İndirme klasörü henüz bilinmiyor.", "Social Video Downloader");
            return;
        }

        try
        {
            OpenPath(_downloadDirectory, selectFile: false);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Social Video Downloader");
        }
    }

    private static void OpenPath(string path, bool selectFile)
    {
        var full = Path.GetFullPath(path);
        if (selectFile)
        {
            if (!File.Exists(full))
                throw new InvalidOperationException("Dosya bulunamadı.");

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{full}\"",
                UseShellExecute = false,
            });
            return;
        }

        if (!Directory.Exists(full))
            Directory.CreateDirectory(full);

        Process.Start(new ProcessStartInfo
        {
            FileName = full,
            UseShellExecute = true,
        });
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _icon.Visible = false;
        _icon.Dispose();
        _helper?.Dispose();
        _http.Dispose();
        _runningIcon.Dispose();
        _stoppedIcon.Dispose();
        base.ExitThreadCore();
    }
}

internal static class StatusIcon
{
    public static Icon Create(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.DrawString("↓", font, Brushes.White, 7, 5);
        return Icon.FromHandle(bitmap.GetHicon());
    }
}
