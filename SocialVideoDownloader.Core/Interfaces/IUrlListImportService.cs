using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Enums;

namespace SocialVideoDownloader.Core.Interfaces;

public interface IUrlListImportService
{
    Task<GistImportResult> ImportAsync(
        string? content,
        int? categoryId,
        DownloadSource source,
        string logPrefix,
        CancellationToken cancellationToken);
}
