using SocialVideoDownloader.Core.Validation;

namespace SocialVideoDownloader.Core.Gist;

public sealed record GistLine(string Text, string? CanonicalUrl, bool IsValid);

public static class GistListParser
{
    public static IReadOnlyList<GistLine> Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        var lines = new List<GistLine>();
        foreach (var raw in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (UrlGuard.TryCanonical(line, out var canonical))
                lines.Add(new GistLine(line, canonical, true));
            else
                lines.Add(new GistLine(line, null, false));
        }

        return lines;
    }
}
