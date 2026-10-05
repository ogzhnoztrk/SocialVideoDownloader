using SocialVideoDownloader.Core.Enums;

namespace SocialVideoDownloader.Core.Platforms;

public static class PlatformResolver
{
    public static Platform Detect(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Platform.Unknown;

        return Detect(uri);
    }

    public static Platform Detect(Uri uri)
    {
        var host = uri.IdnHost.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        if (IsHost(host, "instagram.com") || IsHost(host, "instagr.am"))
            return Platform.Instagram;

        if (IsHost(host, "tiktok.com"))
            return Platform.TikTok;

        if (IsHost(host, "twitter.com"))
            return Platform.Twitter;

        if (IsHost(host, "x.com"))
            return Platform.X;

        return Platform.Unknown;
    }

    public static Platform FromExtractor(string? extractorKey, string url)
    {
        if (!string.IsNullOrWhiteSpace(extractorKey))
        {
            if (extractorKey.Contains("instagram", StringComparison.OrdinalIgnoreCase))
                return Platform.Instagram;

            if (extractorKey.Contains("tiktok", StringComparison.OrdinalIgnoreCase))
                return Platform.TikTok;

            if (extractorKey.Contains("twitter", StringComparison.OrdinalIgnoreCase))
                return Platform.Twitter;
        }

        return Detect(url);
    }

    public static string GetFolderName(Platform platform) => platform switch
    {
        Platform.Instagram => "Instagram",
        Platform.TikTok => "TikTok",
        Platform.Twitter or Platform.X => "Twitter",
        _ => "Other",
    };

    public static string GetDisplayName(Platform platform) => platform switch
    {
        Platform.Instagram => "Instagram",
        Platform.TikTok => "TikTok",
        Platform.Twitter => "Twitter",
        Platform.X => "X",
        _ => "Diğer",
    };

    private static bool IsHost(string host, string domain) =>
        host.Equals(domain, StringComparison.Ordinal) ||
        host.EndsWith("." + domain, StringComparison.Ordinal);
}
