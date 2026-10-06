namespace SocialVideoDownloader.Core.DTOs;

public sealed class GistImportResult
{
    public bool Succeeded { get; set; }

    public string Message { get; set; } = string.Empty;

    public int Found { get; set; }

    public int Added { get; set; }

    public int AlreadyDownloaded { get; set; }

    public int AlreadyQueued { get; set; }

    public int Retried { get; set; }

    public int Invalid { get; set; }
}
