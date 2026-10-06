using System.Diagnostics;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Logging;
using SocialVideoDownloader.Core.Validation;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class DownloadWorker(
    IServiceScopeFactory scopeFactory,
    DownloadCancellationRegistry cancellation,
    ILogger<DownloadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);
                if (!processed)
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Download worker failed while checking the queue");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IDownloadJobService>();
        var downloader = scope.ServiceProvider.GetRequiredService<IVideoDownloader>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var binaries = scope.ServiceProvider.GetRequiredService<IDownloaderBinaryManager>();

        var job = await jobs.ClaimNextAsync(stoppingToken);
        if (job is null)
            return false;

        var jobToken = cancellation.Register(job.Id, stoppingToken);
        var started = Stopwatch.StartNew();
        logger.LogInformation(
            "Download started. JobId={JobId} Url={Url} Platform={Platform}",
            job.Id,
            LogSanitizer.Url(job.Url),
            job.Platform);

        try
        {
            var settings = await settingsService.GetEntityAsync(jobToken);
            var template = OutputPathBuilder.BuildTemplate(settings, job.Platform, DateTimeOffset.Now, job.CategoryName);
            var directory = Path.GetDirectoryName(template) ?? settings.DownloadDirectory;
            Directory.CreateDirectory(directory);
            EnsureDiskSpace(directory);
            await jobs.UpdateOutputDirectoryAsync(job.Id, directory, CancellationToken.None);

            var lastWrite = DateTime.MinValue;
            var progress = new Progress<DownloadProgress>(update =>
            {
                var now = DateTime.UtcNow;
                if (now - lastWrite < TimeSpan.FromSeconds(1) && update.Percent < 100)
                    return;

                lastWrite = now;
                _ = jobs.UpdateProgressAsync(job.Id, update, CancellationToken.None);
            });

            var result = await downloader.DownloadAsync(
                new DownloadRequest
                {
                    Url = job.Url,
                    OutputTemplate = template,
                    Overwrite = job.IgnoreDuplicate,
                    Platform = job.Platform,
                },
                progress,
                jobToken);

            var safePath = PathSafety.EnsureUnderRoot(settings.DownloadDirectory, result.FilePath);
            await jobs.MarkCompletedAsync(job.Id, safePath, CancellationToken.None);
            logger.LogInformation(
                "Download completed. JobId={JobId} Url={Url} Platform={Platform} File={File} ElapsedMs={Elapsed}",
                job.Id,
                LogSanitizer.Url(job.Url),
                job.Platform,
                safePath,
                started.ElapsedMilliseconds);
        }
        catch (DownloadException exception) when (exception.UserMessage == UserMessages.Cancelled && stoppingToken.IsCancellationRequested)
        {
            await jobs.MarkPendingAsync(job.Id, CancellationToken.None);
            logger.LogInformation("Download interrupted by shutdown and returned to the queue. JobId={JobId}", job.Id);
        }
        catch (DownloadException exception) when (exception.UserMessage == UserMessages.Cancelled)
        {
            await jobs.MarkCancelledAsync(job.Id, CancellationToken.None);
            logger.LogInformation(
                "Download cancelled. JobId={JobId} Url={Url} ElapsedMs={Elapsed}",
                job.Id,
                LogSanitizer.Url(job.Url),
                started.ElapsedMilliseconds);
        }
        catch (DownloadException exception)
        {
            await jobs.MarkFailedAsync(job.Id, exception.UserMessage, CancellationToken.None);
            logger.LogError(
                exception,
                "Download failed. JobId={JobId} Url={Url} Platform={Platform} ElapsedMs={Elapsed}",
                job.Id,
                LogSanitizer.Url(job.Url),
                job.Platform,
                started.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            await jobs.MarkFailedAsync(job.Id, UserMessages.Unexpected, CancellationToken.None);
            logger.LogError(exception, "Download failed unexpectedly. JobId={JobId} Url={Url}", job.Id, LogSanitizer.Url(job.Url));
        }
        finally
        {
            cancellation.Complete(job.Id);
        }

        return true;
    }

    private static void EnsureDiskSpace(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrWhiteSpace(root))
            return;

        var drive = new DriveInfo(root);
        if (drive.IsReady && drive.AvailableFreeSpace < AppConstants.MinimumFreeBytes)
            throw new DownloadException(UserMessages.DiskFull);
    }
}
