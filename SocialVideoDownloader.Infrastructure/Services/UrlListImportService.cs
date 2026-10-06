using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Gist;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Logging;
using SocialVideoDownloader.Core.Platforms;
using SocialVideoDownloader.Core.Validation;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class UrlListImportService(
    IDownloadJobService jobs,
    ICategoryService categories,
    ISettingsService settingsService,
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<UrlListImportService> logger) : IUrlListImportService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<GistImportResult> ImportAsync(
        string? content,
        int? categoryId,
        DownloadSource source,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            return await ImportCoreAsync(content, categoryId, source, logPrefix, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<GistImportResult> ImportCoreAsync(
        string? content,
        int? categoryId,
        DownloadSource source,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var lines = GistListParser.Parse(content);
        var valid = lines.Where(line => line.IsValid && line.CanonicalUrl is not null).ToList();
        var invalid = lines.Where(line => !line.IsValid).ToList();
        foreach (var line in invalid)
            logger.LogInformation("{Prefix} Geçersiz URL atlandı: {Line}", logPrefix, LogSanitizer.Text(line.Text, 120));

        logger.LogInformation("{Prefix} {Count} URL bulundu.", logPrefix, valid.Count);
        var known = await LoadJobsAsync(cancellationToken);
        var added = 0;
        var downloaded = 0;
        var queued = 0;
        var retried = 0;
        var settings = await settingsService.GetEntityAsync(cancellationToken);

        foreach (var line in valid)
        {
            var canonical = line.CanonicalUrl!;
            var matches = known.Where(job => SameUrl(job, canonical)).ToList();
            if (matches.Any(job => job.Status is DownloadStatus.Pending or DownloadStatus.Downloading))
            {
                queued++;
                continue;
            }

            if (matches.Any(job => job.Status == DownloadStatus.Completed))
            {
                downloaded++;
                continue;
            }

            var latest = matches.OrderByDescending(job => job.CreatedAt).FirstOrDefault();
            if (latest?.Status == DownloadStatus.Cancelled)
            {
                logger.LogInformation("{Prefix} İptal edilmiş URL atlandı.", logPrefix);
                continue;
            }

            if (latest?.Status == DownloadStatus.Failed)
            {
                if (latest.RetryCount >= AppConstants.MaxGistRetryCount)
                {
                    logger.LogInformation("{Prefix} Yeniden deneme sınırına ulaşıldı.", logPrefix);
                    continue;
                }

                await jobs.RetryAsync(latest.Id, cancellationToken);
                await IncrementRetryAsync(latest.Id, cancellationToken);
                latest.Status = DownloadStatus.Pending;
                latest.RetryCount++;
                retried++;
                continue;
            }

            try
            {
                var created = await jobs.CreateAsync(line.Text, cancellationToken, source, categoryId);
                if (created.AlreadyExists)
                    downloaded++;
                else if (created.AlreadyQueued)
                    queued++;
                else
                {
                    added++;
                    known.Add(new DownloadJob
                    {
                        Id = created.Job.Id,
                        Url = line.Text,
                        NormalizedUrl = canonical,
                        Status = DownloadStatus.Pending,
                        Source = source,
                        CreatedAt = DateTime.UtcNow,
                    });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "{Prefix} URL kuyruğa eklenemedi.", logPrefix);
                var failed = await RecordFailureAsync(settings, line.Text, canonical, exception.Message, source, categoryId, cancellationToken);
                known.Add(failed);
            }
        }

        if (added > 0)
            logger.LogInformation("{Prefix} {Count} yeni URL tespit edildi.", logPrefix, added);
        if (downloaded > 0)
            logger.LogInformation("{Prefix} {Count} URL daha önce indirilmiş.", logPrefix, downloaded);
        if (queued > 0)
            logger.LogInformation("{Prefix} {Count} URL zaten kuyrukta.", logPrefix, queued);
        if (added > 0)
            logger.LogInformation("{Prefix} {Count} URL download queue'ya eklendi.", logPrefix, added);

        var message = added == 0
            ? "Yeni video bulunamadı."
            : added == 1 ? "1 yeni video bulundu." : $"{added} yeni video bulundu.";
        return new GistImportResult
        {
            Succeeded = true,
            Message = message,
            Found = valid.Count,
            Added = added,
            AlreadyDownloaded = downloaded,
            AlreadyQueued = queued,
            Retried = retried,
            Invalid = invalid.Count,
        };
    }

    private async Task<List<DownloadJob>> LoadJobsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DownloadJobs.AsNoTracking().ToListAsync(cancellationToken);
    }

    private static bool SameUrl(DownloadJob job, string canonical)
    {
        if (string.Equals(job.NormalizedUrl, canonical, StringComparison.OrdinalIgnoreCase))
            return true;

        return UrlGuard.TryCanonical(job.Url, out var other) &&
            string.Equals(other, canonical, StringComparison.OrdinalIgnoreCase);
    }

    private async Task IncrementRetryAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.DownloadJobs.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return;

        job.RetryCount++;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<DownloadJob> RecordFailureAsync(
        ApplicationSettings settings,
        string url,
        string canonical,
        string message,
        DownloadSource source,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        var category = await categories.ResolveFolderAsync(categoryId, cancellationToken);
        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            Url = url,
            NormalizedUrl = canonical,
            Source = source,
            Platform = PlatformResolver.Detect(url),
            CategoryName = category,
            Title = source == DownloadSource.File ? "Dosya" : "Gist",
            OutputDirectory = settings.DownloadDirectory,
            Status = DownloadStatus.Failed,
            ErrorMessage = message.Length <= 500 ? message : message[..500],
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }
}
