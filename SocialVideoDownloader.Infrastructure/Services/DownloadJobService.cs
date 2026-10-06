using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Logging;
using SocialVideoDownloader.Core.Platforms;
using SocialVideoDownloader.Core.Validation;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class DownloadJobService(
    IDbContextFactory<AppDbContext> dbFactory,
    ISettingsService settingsService,
    ICategoryService categories,
    IVideoDownloader downloader,
    IDownloaderBinaryManager binaries,
    VideoInfoCache cache,
    DownloadCancellationRegistry cancellation,
    ILogger<DownloadJobService> logger) : IDownloadJobService
{
    public async Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken)
    {
        var info = await ResolveInfoAsync(url, cancellationToken);
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        info.DownloadDirectory = settings.DownloadDirectory;
        if (binaries.ResolveFfmpegDirectory(settings) is not null)
            info.Format = "MP4";
        return info;
    }

    public async Task<CreateDownloadResponse> CreateAsync(
        string url,
        CancellationToken cancellationToken,
        DownloadSource source = DownloadSource.Manual,
        int? categoryId = null)
    {
        var category = await categories.ResolveFolderAsync(categoryId, cancellationToken);
        var info = await GetVideoInfoAsync(url, cancellationToken);
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(info.VideoId))
        {
            var existing = await db.DownloadJobs
                .Where(job => job.VideoId == info.VideoId)
                .OrderByDescending(job => job.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                if (existing.Status is DownloadStatus.Pending or DownloadStatus.Downloading)
                    return new CreateDownloadResponse { AlreadyQueued = true, Job = Map(existing) };

                if (settings.DuplicateCheckEnabled &&
                    existing.Status == DownloadStatus.Completed &&
                    !string.IsNullOrWhiteSpace(existing.FilePath) &&
                    File.Exists(existing.FilePath))
                    return new CreateDownloadResponse { AlreadyExists = true, Job = Map(existing) };
            }
        }

        var template = OutputPathBuilder.BuildTemplate(settings, info.Platform, DateTimeOffset.Now, category);
        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            Url = info.Url,
            NormalizedUrl = UrlGuard.TryCanonical(info.Url, out var canonical) ? canonical : null,
            Source = source,
            CategoryName = category,
            Platform = info.Platform,
            Title = Limit(info.Title, 500),
            Uploader = Limit(info.Uploader, 300),
            VideoId = Limit(info.VideoId, 128),
            FileName = null,
            OutputDirectory = Limit(Path.GetDirectoryName(template) ?? settings.DownloadDirectory, 1000),
            Status = DownloadStatus.Pending,
            ThumbnailUrl = info.ThumbnailUrl,
            DurationSeconds = info.DurationSeconds,
            Resolution = info.Resolution,
            Format = info.Format,
            CreatedAt = DateTime.UtcNow,
        };

        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Download queued. JobId={JobId} Url={Url} Platform={Platform}",
            job.Id,
            LogSanitizer.Url(job.Url),
            job.Platform);
        return new CreateDownloadResponse { Job = Map(job) };
    }

    public async Task<DownloadJobDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<IReadOnlyList<DownloadJobDto>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var jobs = await db.DownloadJobs.AsNoTracking()
            .OrderByDescending(job => job.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<DownloadJobDto>> ListActiveAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var jobs = await db.DownloadJobs.AsNoTracking()
            .Where(job => job.Status == DownloadStatus.Pending || job.Status == DownloadStatus.Downloading)
            .OrderBy(job => job.CreatedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(Map).ToList();
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var start = DateTime.Today.ToUniversalTime();
        var end = DateTime.Today.AddDays(1).ToUniversalTime();
        var jobs = await db.DownloadJobs.AsNoTracking()
            .Select(job => new { job.Status, job.CreatedAt })
            .ToListAsync(cancellationToken);

        return new DashboardStatsDto
        {
            Total = jobs.Count,
            Today = jobs.Count(job => job.CreatedAt >= start && job.CreatedAt < end),
            Successful = jobs.Count(job => job.Status == DownloadStatus.Completed),
            Failed = jobs.Count(job => job.Status == DownloadStatus.Failed),
            Active = jobs.Count(job => job.Status is DownloadStatus.Pending or DownloadStatus.Downloading),
        };
    }

    public async Task<DownloadJobDto?> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return null;

        if (job.Status is DownloadStatus.Pending or DownloadStatus.Downloading)
        {
            job.Status = DownloadStatus.Cancelled;
            job.ErrorMessage = UserMessages.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            cancellation.Cancel(id);
            logger.LogInformation("Download cancelled. JobId={JobId} Url={Url}", job.Id, LogSanitizer.Url(job.Url));
        }

        return Map(job);
    }

    public async Task<DownloadJobDto?> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return null;

        job.Status = DownloadStatus.Pending;
        job.Progress = 0;
        job.BytesDownloaded = null;
        job.TotalBytes = null;
        job.ErrorMessage = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.IgnoreDuplicate = true;
        await db.SaveChangesAsync(cancellationToken);
        return Map(job);
    }

    public async Task<bool> DeleteAsync(Guid id, bool deleteFile, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return false;

        if (job.Status == DownloadStatus.Downloading)
            cancellation.Cancel(id);

        if (deleteFile && !string.IsNullOrWhiteSpace(job.FilePath) && !string.IsNullOrWhiteSpace(job.OutputDirectory))
        {
            var full = PathSafety.EnsureUnderRoot(job.OutputDirectory, job.FilePath);
            if (File.Exists(full))
                File.Delete(full);
        }

        db.DownloadJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task OpenFileAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await RequireJobAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(job.FilePath) || string.IsNullOrWhiteSpace(job.OutputDirectory))
            throw new DownloadException("Dosya bulunamadı.");

        LocalFileOpener.OpenFile(job.OutputDirectory, job.FilePath);
    }

    public async Task OpenFolderAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await RequireJobAsync(id, cancellationToken);
        var folder = !string.IsNullOrWhiteSpace(job.FilePath)
            ? Path.GetDirectoryName(job.FilePath)
            : job.OutputDirectory;
        if (string.IsNullOrWhiteSpace(folder))
            throw new DownloadException("Klasör bulunamadı.");

        LocalFileOpener.OpenFolder(folder);
    }

    public async Task OpenDownloadRootAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        LocalFileOpener.OpenFolder(settings.DownloadDirectory);
    }

    public async Task<DownloadJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs
            .Where(item => item.Status == DownloadStatus.Pending)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
            return null;

        job.Status = DownloadStatus.Downloading;
        job.StartedAt = DateTime.UtcNow;
        job.ErrorMessage = null;
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task UpdateProgressAsync(Guid id, DownloadProgress progress, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null || job.Status != DownloadStatus.Downloading)
            return;

        job.Progress = progress.Percent;
        job.BytesDownloaded = progress.BytesDownloaded;
        job.TotalBytes = progress.TotalBytes;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task MarkCompletedAsync(Guid id, string filePath, CancellationToken cancellationToken) =>
        UpdateTerminalAsync(id, DownloadStatus.Completed, filePath, null, cancellationToken);

    public Task MarkFailedAsync(Guid id, string userMessage, CancellationToken cancellationToken) =>
        UpdateTerminalAsync(id, DownloadStatus.Failed, null, userMessage, cancellationToken);

    public Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken) =>
        UpdateTerminalAsync(id, DownloadStatus.Cancelled, null, UserMessages.Cancelled, cancellationToken);

    public async Task MarkPendingAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return;

        job.Status = DownloadStatus.Pending;
        job.StartedAt = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOutputDirectoryAsync(Guid id, string outputDirectory, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return;

        job.OutputDirectory = Limit(outputDirectory, 1000);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<VideoInfoResult> ResolveInfoAsync(string url, CancellationToken cancellationToken)
    {
        if (!UrlGuard.TryNormalize(url, out var normalized))
            throw new DownloadException(UserMessages.InvalidUrl);

        var cached = cache.Get(normalized);
        if (cached is not null)
            return cached;

        var info = await downloader.GetVideoInfoAsync(normalized, cancellationToken);
        cache.Set(normalized, info);
        return info;
    }

    private async Task UpdateTerminalAsync(
        Guid id,
        DownloadStatus status,
        string? filePath,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return;

        job.Status = status;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = Limit(error, 500);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            job.FilePath = Limit(filePath, 1000);
            job.FileName = Limit(Path.GetFileName(filePath), 255);
            job.OutputDirectory = Limit(Path.GetDirectoryName(filePath) ?? job.OutputDirectory, 1000);
            job.Progress = 100;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DownloadJob> RequireJobAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DownloadJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new DownloadException("İndirme bulunamadı.");
    }

    private static DownloadJobDto Map(DownloadJob job) => new()
    {
        Id = job.Id,
        Url = job.Url,
        Source = job.Source,
        CategoryName = string.IsNullOrWhiteSpace(job.CategoryName) ? "Genel" : job.CategoryName,
        Platform = job.Platform,
        PlatformName = PlatformResolver.GetDisplayName(job.Platform),
        Title = job.Title,
        Uploader = job.Uploader,
        VideoId = job.VideoId,
        FileName = job.FileName,
        OutputDirectory = job.OutputDirectory,
        Status = job.Status,
        Progress = job.Progress,
        BytesDownloaded = job.BytesDownloaded,
        TotalBytes = job.TotalBytes,
        FilePath = job.FilePath,
        ThumbnailUrl = job.ThumbnailUrl,
        DurationSeconds = job.DurationSeconds,
        Resolution = job.Resolution,
        Format = job.Format,
        ErrorMessage = job.ErrorMessage,
        CreatedAt = AsUtc(job.CreatedAt),
        StartedAt = job.StartedAt is null ? null : AsUtc(job.StartedAt.Value),
        CompletedAt = job.CompletedAt is null ? null : AsUtc(job.CompletedAt.Value),
        CanOpenFile = job.Status == DownloadStatus.Completed &&
            !string.IsNullOrWhiteSpace(job.FilePath) &&
            File.Exists(job.FilePath),
    };

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static string Limit(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max ? value : value[..max];
    }
}
