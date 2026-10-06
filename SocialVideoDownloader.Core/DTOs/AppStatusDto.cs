namespace SocialVideoDownloader.Core.DTOs;

public sealed class AppStatusDto
{
    public bool Running { get; set; }

    public int ActiveDownloads { get; set; }

    public int Port { get; set; }

    public string DownloadDirectory { get; set; } = string.Empty;

    public bool OpenWebOnStartup { get; set; }

    public bool StartWithWindows { get; set; }

    public bool YtDlpAvailable { get; set; }

    public bool FfmpegAvailable { get; set; }

    public GistStatusDto Gist { get; set; } = new();
}
