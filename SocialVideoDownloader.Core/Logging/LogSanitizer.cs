namespace SocialVideoDownloader.Core.Logging;

public static class LogSanitizer
{
    public static string Url(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "[invalid-url]";

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "[url-with-credentials]";

        return uri.GetLeftPart(UriPartial.Path);
    }

    public static string Text(string? text, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var kept = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line =>
                !line.Contains("cookie", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("authorization", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("access_token", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("password", StringComparison.OrdinalIgnoreCase));

        var joined = string.Join(Environment.NewLine, kept);
        return joined.Length <= maxLength ? joined : joined[..maxLength];
    }
}
