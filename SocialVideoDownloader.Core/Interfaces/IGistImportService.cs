using SocialVideoDownloader.Core.DTOs;

namespace SocialVideoDownloader.Core.Interfaces;

public interface IGistImportService
{
    Task<GistImportResult> CheckNowAsync(CancellationToken cancellationToken);
}
