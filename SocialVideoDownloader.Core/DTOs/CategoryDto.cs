namespace SocialVideoDownloader.Core.DTOs;

public sealed class CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? GistUrl { get; set; }
}
