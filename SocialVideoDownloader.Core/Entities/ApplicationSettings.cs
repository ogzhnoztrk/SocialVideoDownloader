using SocialVideoDownloader.Core.Constants;

namespace SocialVideoDownloader.Core.Entities;

public sealed class ApplicationSettings
{
    public int Id { get; set; } = 1;

    public string DownloadDirectory { get; set; } = string.Empty;

    public string FileNameTemplate { get; set; } = AppConstants.DefaultFileNameTemplate;

    public bool UseDateFolders { get; set; }

    public bool DuplicateCheckEnabled { get; set; } = true;

    public string? YtDlpPath { get; set; }

    public string? FfmpegPath { get; set; }

    public bool AutoUpdateBinaries { get; set; }

    public string ListenAddress { get; set; } = AppConstants.DefaultListenAddress;

    public int WebPort { get; set; } = AppConstants.DefaultPort;

    public bool OpenWebOnStartup { get; set; }

    public bool StartWithWindows { get; set; }

    public static ApplicationSettings CreateDefault()
    {
        return new ApplicationSettings
        {
            Id = 1,
            DownloadDirectory = DefaultDownloadDirectory(),
            FileNameTemplate = AppConstants.DefaultFileNameTemplate,
            UseDateFolders = false,
            DuplicateCheckEnabled = true,
            AutoUpdateBinaries = false,
            ListenAddress = AppConstants.DefaultListenAddress,
            WebPort = AppConstants.DefaultPort,
            OpenWebOnStartup = false,
            StartWithWindows = true,
        };
    }

    public static string DefaultDownloadDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Contains("systemprofile", StringComparison.OrdinalIgnoreCase))
        {
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonVideos);
            if (string.IsNullOrWhiteSpace(common))
                common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Videos");

            return Path.Combine(common, "SocialDownloads");
        }

        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(videos))
            videos = Path.Combine(profile, "Videos");

        return Path.Combine(videos, "SocialDownloads");
    }
}
