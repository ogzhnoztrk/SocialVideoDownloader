namespace SocialVideoDownloader.Core.Entities;

public sealed class DownloadJob
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? NormalizedUrl { get; set; }

    public Enums.DownloadSource Source { get; set; } = Enums.DownloadSource.Manual;

    public int RetryCount { get; set; }

    public Enums.Platform Platform { get; set; }

    public string CategoryName { get; set; } = "Genel";

    public string Title { get; set; } = string.Empty;

    public string? Uploader { get; set; }

    public string? VideoId { get; set; }

    public string? FileName { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    public Enums.DownloadStatus Status { get; set; }

    public double Progress { get; set; }

    public long? BytesDownloaded { get; set; }

    public long? TotalBytes { get; set; }

    public string? FilePath { get; set; }

    public string? ThumbnailUrl { get; set; }

    public int? DurationSeconds { get; set; }

    public string? Resolution { get; set; }

    public string? Format { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IgnoreDuplicate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
