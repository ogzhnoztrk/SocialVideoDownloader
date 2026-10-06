using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Gist;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Configuration;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class StatusService(
    IDbContextFactory<AppDbContext> dbFactory,
    ISettingsService settingsService,
    IDownloaderBinaryManager binaries,
    ActiveEndpoint endpoint)
{
    public async Task<AppStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var active = await db.DownloadJobs.CountAsync(
            job => job.Status == DownloadStatus.Pending || job.Status == DownloadStatus.Downloading,
            cancellationToken);

        return new AppStatusDto
        {
            Running = true,
            ActiveDownloads = active,
            Port = endpoint.Port,
            DownloadDirectory = settings.DownloadDirectory,
            OpenWebOnStartup = settings.OpenWebOnStartup,
            StartWithWindows = settings.StartWithWindows,
            YtDlpAvailable = binaries.ResolveYtDlpPath(settings) is not null,
            FfmpegAvailable = binaries.ResolveFfmpegDirectory(settings) is not null,
            Gist = GistStatusMapper.From(settings),
        };
    }
}
