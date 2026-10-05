using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Validation;

namespace SocialVideoDownloader.Infrastructure.Services;

public static class LocalFileOpener
{
    public static void OpenFile(string root, string path)
    {
        var full = PathSafety.EnsureUnderRoot(root, path);
        if (!File.Exists(full))
            throw new DownloadException("Dosya bulunamadı.");

        StartExplorer($"/select,\"{full}\"");
    }

    public static void OpenFolder(string path)
    {
        var full = PathSafety.RequireDownloadRoot(path);
        if (!Directory.Exists(full))
            Directory.CreateDirectory(full);

        StartExplorer(full);
    }

    private static void StartExplorer(string argument) => InteractiveExplorer.Start(argument);
}
