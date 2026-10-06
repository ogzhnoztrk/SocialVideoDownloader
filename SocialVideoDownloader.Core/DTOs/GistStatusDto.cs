namespace SocialVideoDownloader.Core.DTOs;

public sealed class GistStatusDto
{
    public bool Enabled { get; set; }

    public string Status { get; set; } = "Kapalı";

    public DateTimeOffset? LastCheckedAt { get; set; }

    public DateTimeOffset? NextCheckAt { get; set; }

    public string? Message { get; set; }

    public int IntervalMinutes { get; set; }
}
