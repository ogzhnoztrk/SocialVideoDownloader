using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;

namespace SocialVideoDownloader.Core.Interfaces;

public interface IDownloadJobService
{
    Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken);

    Task<CreateDownloadResponse> CreateAsync(
        string url,
        CancellationToken cancellationToken,
        DownloadSource source = DownloadSource.Manual,
        int? categoryId = null);

    Task<DownloadJobDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadJobDto>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadJobDto>> ListActiveAsync(CancellationToken cancellationToken);

    Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    Task<DownloadJobDto?> CancelAsync(Guid id, CancellationToken cancellationToken);

    Task<DownloadJobDto?> RetryAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, bool deleteFile, CancellationToken cancellationToken);

    Task OpenFileAsync(Guid id, CancellationToken cancellationToken);

    Task OpenFolderAsync(Guid id, CancellationToken cancellationToken);

    Task OpenDownloadRootAsync(CancellationToken cancellationToken);

    Task<DownloadJob?> ClaimNextAsync(CancellationToken cancellationToken);

    Task UpdateProgressAsync(Guid id, DownloadProgress progress, CancellationToken cancellationToken);

    Task MarkCompletedAsync(Guid id, string filePath, CancellationToken cancellationToken);

    Task MarkFailedAsync(Guid id, string userMessage, CancellationToken cancellationToken);

    Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken);

    Task MarkPendingAsync(Guid id, CancellationToken cancellationToken);

    Task UpdateOutputDirectoryAsync(Guid id, string outputDirectory, CancellationToken cancellationToken);
}
