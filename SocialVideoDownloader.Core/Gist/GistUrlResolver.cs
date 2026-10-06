namespace SocialVideoDownloader.Core.Gist;

public static class GistUrlResolver
{
    public static bool TryGetRawUrl(string? input, out string rawUrl)
    {
        rawUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !IsName(segments[0]) || !IsId(segments[1]))
            return false;

        var user = segments[0];
        var id = segments[1];
        if (uri.Host.Equals("gist.github.com", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length == 2)
            {
                rawUrl = $"https://gist.githubusercontent.com/{user}/{id}/raw";
                return true;
            }

            if (segments[2].Equals("raw", StringComparison.OrdinalIgnoreCase))
            {
                rawUrl = $"https://gist.githubusercontent.com/{user}/{id}/{string.Join('/', segments.Skip(2))}";
                return true;
            }

            return false;
        }

        if (!uri.Host.Equals("gist.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            return false;

        if (segments.Length < 3 || !segments[2].Equals("raw", StringComparison.OrdinalIgnoreCase))
            return false;

        rawUrl = $"https://gist.githubusercontent.com/{user}/{id}/{string.Join('/', segments.Skip(2))}";
        return true;
    }

    private static bool IsName(string value) =>
        value.Length is >= 1 and <= 80 && value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private static bool IsId(string value) =>
        value.Length is >= 8 and <= 64 && value.All(char.IsLetterOrDigit);
}
