namespace SocialVideoDownloader.Core.Files;

public static class FileNameSanitizer
{
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "video";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var character in name.Trim())
        {
            if (character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ||
                invalid.Contains(character) ||
                char.IsControl(character))
                continue;

            builder.Append(character);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(result))
            return "video";

        return result.Length <= 80 ? result : result[..80].TrimEnd();
    }
}
