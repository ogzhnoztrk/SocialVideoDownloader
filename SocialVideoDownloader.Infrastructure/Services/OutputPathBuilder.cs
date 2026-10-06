using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Files;
using SocialVideoDownloader.Core.Platforms;
using SocialVideoDownloader.Core.Validation;

namespace SocialVideoDownloader.Infrastructure.Services;

public static class OutputPathBuilder
{
    public static string BuildDirectory(ApplicationSettings settings, Platform platform, DateTimeOffset timestamp, string? category)
    {
        var root = PathSafety.RequireDownloadRoot(settings.DownloadDirectory);
        var folder = CategoryFolder.Normalize(string.IsNullOrWhiteSpace(category) ? CategoryFolder.DefaultName : category);
        var directory = Path.Combine(root, folder, PlatformResolver.GetFolderName(platform));
        if (settings.UseDateFolders)
        {
            var local = timestamp.ToLocalTime();
            directory = Path.Combine(directory, local.ToString("yyyy"), local.ToString("MM"), local.ToString("dd"));
        }

        return PathSafety.EnsureUnderRoot(root, directory);
    }

    public static string BuildTemplate(ApplicationSettings settings, Platform platform, DateTimeOffset timestamp, string? category) =>
        Path.Combine(BuildDirectory(settings, platform, timestamp, category), ValidateTemplate(settings.FileNameTemplate));

    public static string ValidateTemplate(string? template)
    {
        var value = string.IsNullOrWhiteSpace(template)
            ? AppConstants.DefaultFileNameTemplate
            : template.Trim();

        if (value.Length is < 3 or > 180 ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('/') ||
            value.Contains('\\') ||
            value.Contains(':') ||
            Path.IsPathRooted(value))
            throw new DownloadException(UserMessages.InvalidFileTemplate);

        return value;
    }
}
