using SocialVideoDownloader.Core.Exceptions;

namespace SocialVideoDownloader.Core.Files;

public static class CategoryFolder
{
    public const string DefaultName = "Genel";

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new DownloadException("Kategori adı boş olamaz.");

        var builder = new System.Text.StringBuilder(input.Length);
        var previousUnderscore = false;
        foreach (var character in input.Trim())
        {
            if (char.IsWhiteSpace(character) || character is '_' or '-')
            {
                if (builder.Length > 0 && !previousUnderscore)
                {
                    builder.Append('_');
                    previousUnderscore = true;
                }

                continue;
            }

            if (character is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '.' ||
                char.IsControl(character) ||
                Path.GetInvalidFileNameChars().Contains(character))
                continue;

            builder.Append(character);
            previousUnderscore = false;
        }

        var name = builder.ToString().Trim('_');
        if (name.Length is < 1 or > 40)
            throw new DownloadException("Kategori adı 1 ile 40 karakter arasında olmalı.");

        if (IsReserved(name))
            throw new DownloadException("Bu kategori adı kullanılamaz.");

        return name;
    }

    private static bool IsReserved(string name)
    {
        var bare = name.Split('.')[0];
        return bare.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            bare.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            bare.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            bare.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (bare.Length == 4 &&
                (bare.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 bare.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                char.IsDigit(bare[3]));
    }
}
