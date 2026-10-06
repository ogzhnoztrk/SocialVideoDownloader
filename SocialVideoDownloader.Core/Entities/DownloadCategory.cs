namespace SocialVideoDownloader.Core.Entities;

public sealed class DownloadCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = "Genel";

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
}
