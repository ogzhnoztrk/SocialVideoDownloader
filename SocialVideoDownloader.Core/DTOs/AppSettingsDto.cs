using SocialVideoDownloader.Core.Constants;

namespace SocialVideoDownloader.Core.DTOs;

public sealed class AppSettingsDto
{
    public string DownloadDirectory { get; set; } = string.Empty;

    public string FileNameTemplate { get; set; } = string.Empty;

    public bool UseDateFolders { get; set; }

    public bool DuplicateCheckEnabled { get; set; }

    public string? YtDlpPath { get; set; }

    public string? FfmpegPath { get; set; }

    public bool AutoUpdateBinaries { get; set; }

    public string ListenAddress { get; set; } = AppConstants.DefaultListenAddress;

    public int WebPort { get; set; }

    public bool OpenWebOnStartup { get; set; }

    public bool StartWithWindows { get; set; }

    public string? GistUrl { get; set; }

    public bool GistPollingEnabled { get; set; }

    public int GistPollIntervalMinutes { get; set; } = AppConstants.DefaultGistPollMinutes;
}
