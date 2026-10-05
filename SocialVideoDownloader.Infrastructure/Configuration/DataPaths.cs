using System.Text.Json;

namespace SocialVideoDownloader.Infrastructure.Configuration;

public static class DataPaths
{
    static DataPaths()
    {
        Root = ResolveRoot();
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ToolsDirectory);
    }

    public static string Root { get; }

    public static string DatabasePath => Path.Combine(Root, "social-video-downloader.db");

    public static string LogsDirectory => Path.Combine(Root, "Logs");

    public static string ToolsDirectory => Path.Combine(Root, "Tools");

    public static string YtDlpPath => Path.Combine(ToolsDirectory, "yt-dlp", "yt-dlp.exe");

    public static string FfmpegDirectory => Path.Combine(ToolsDirectory, "ffmpeg");

    public static string FfmpegPath => Path.Combine(FfmpegDirectory, "ffmpeg.exe");

    public static string HostSettingsPath => Path.Combine(Root, "host-settings.json");

    public static string ConnectionString => $"Data Source={DatabasePath}";

    private static string ResolveRoot()
    {
        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SocialVideoDownloader");
        if (CanWrite(programData))
            return programData;

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SocialVideoDownloader");
        Directory.CreateDirectory(local);
        return local;
    }

    private static bool CanWrite(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, ".write-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public static class HostSettingsStore
{
    public static HostEndpoint Read(int fallbackPort)
    {
        var port = NormalizePort(fallbackPort);
        var address = Core.Constants.AppConstants.DefaultListenAddress;
        try
        {
            if (!File.Exists(DataPaths.HostSettingsPath))
                return new HostEndpoint(address, port);

            using var document = JsonDocument.Parse(File.ReadAllText(DataPaths.HostSettingsPath));
            var root = document.RootElement;
            if (root.TryGetProperty("webPort", out var portValue) && portValue.TryGetInt32(out var storedPort))
                port = NormalizePort(storedPort);
            if (root.TryGetProperty("listenAddress", out var addressValue))
            {
                var text = addressValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    address = text.Trim();
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new HostEndpoint(Core.Constants.AppConstants.DefaultListenAddress, NormalizePort(fallbackPort));
        }

        return new HostEndpoint(address, port);
    }

    public static void Write(string listenAddress, int port)
    {
        Directory.CreateDirectory(DataPaths.Root);
        var json = JsonSerializer.Serialize(new
        {
            listenAddress,
            webPort = NormalizePort(port),
        });
        File.WriteAllText(DataPaths.HostSettingsPath, json);
    }

    private static int NormalizePort(int port) => port is >= 1024 and <= 65535 ? port : Core.Constants.AppConstants.DefaultPort;
}

public sealed record HostEndpoint(string ListenAddress, int Port);
