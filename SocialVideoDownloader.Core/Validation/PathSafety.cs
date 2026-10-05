using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Exceptions;

namespace SocialVideoDownloader.Core.Validation;

public static class PathSafety
{
    public static string RequireDownloadRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new DownloadException(UserMessages.InvalidDownloadFolder);

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new DownloadException(UserMessages.InvalidDownloadFolder);
        }

        if (!Path.IsPathRooted(full) || full.Contains('"', StringComparison.Ordinal))
            throw new DownloadException(UserMessages.InvalidDownloadFolder);

        RejectBlocked(full);
        return full;
    }

    public static string EnsureUnderRoot(string root, string candidate)
    {
        var fullRoot = RequireDownloadRoot(root);
        string full;
        try
        {
            full = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new DownloadException(UserMessages.InvalidPath);
        }

        if (full.Contains('"', StringComparison.Ordinal))
            throw new DownloadException(UserMessages.InvalidPath);

        var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new DownloadException(UserMessages.InvalidPath);

        RejectLinkEscape(fullRoot, full);
        return full;
    }

    public static void RejectBlocked(string fullPath)
    {
        string?[] blocked =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ];

        foreach (var item in blocked)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            var blockedRoot = Path.GetFullPath(item);
            var prefix = blockedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.Equals(blockedRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new DownloadException(UserMessages.BlockedFolder);
        }
    }

    private static void RejectLinkEscape(string root, string fullPath)
    {
        FileSystemInfo info = File.Exists(fullPath) ? new FileInfo(fullPath) : new DirectoryInfo(fullPath);
        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        if (target is null)
            return;

        var targetPath = Path.GetFullPath(target.FullName);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!targetPath.Equals(root, StringComparison.OrdinalIgnoreCase) &&
            !targetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new DownloadException(UserMessages.InvalidPath);
    }
}
