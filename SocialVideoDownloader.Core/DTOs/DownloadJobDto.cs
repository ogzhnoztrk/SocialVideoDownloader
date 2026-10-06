using SocialVideoDownloader.Core.Enums;

namespace SocialVideoDownloader.Core.DTOs;

public sealed class DownloadJobDto
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public DownloadSource Source { get; set; }

    public string CategoryName { get; set; } = "Genel";

    public Platform Platform { get; set; }

    public string PlatformName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Uploader { get; set; }

    public string? VideoId { get; set; }

    public string? FileName { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    public DownloadStatus Status { get; set; }

    public double Progress { get; set; }

    public long? BytesDownloaded { get; set; }

    public long? TotalBytes { get; set; }

    public string? FilePath { get; set; }

    public string? ThumbnailUrl { get; set; }

    public int? DurationSeconds { get; set; }

    public string? Resolution { get; set; }

    public string? Format { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool CanOpenFile { get; set; }
}
