using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class GistImportService(
    IGistClient gistClient,
    IUrlListImportService lists,
    ICategoryService categories,
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<GistImportService> logger) : IGistImportService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<GistImportResult> CheckNowAsync(CancellationToken cancellationToken)
    {
        if (!await Gate.WaitAsync(0, cancellationToken))
            return new GistImportResult { Succeeded = true, Message = "Kontrol zaten sürüyor." };

        try
        {
            return await CheckCoreAsync(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<GistImportResult> CheckCoreAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[Gist] Kontrol başlatıldı.");
        var sources = (await categories.ListAsync(cancellationToken))
            .Where(category => !string.IsNullOrWhiteSpace(category.GistUrl))
            .ToList();
        if (sources.Count == 0)
        {
            var empty = new GistImportResult { Message = "Gist adresi boş." };
            await RememberAsync(empty, cancellationToken);
            logger.LogWarning("[Gist] Gist kontrolü başarısız: Gist adresi boş.");
            return empty;
        }

        var aggregate = new GistImportResult { Succeeded = true };
        var messages = new List<string>();
        foreach (var source in sources)
        {
            logger.LogInformation("[Gist] {Category} kontrol ediliyor.", source.Name);
            string content;
            try
            {
                content = await gistClient.GetContentAsync(source.GistUrl!, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "[Gist] {Category} kontrolü başarısız: {Message}", source.Name, exception.Message);
                aggregate.Succeeded = false;
                messages.Add(sources.Count == 1
                    ? "Gist dosyasına erişilemedi."
                    : $"{source.Name}: Gist dosyasına erişilemedi.");
                continue;
            }

            logger.LogInformation("[Gist] {Category} içeriği alındı.", source.Name);
            var result = await lists.ImportAsync(
                content,
                source.Id,
                DownloadSource.Gist,
                $"[Gist] {source.Name}",
                cancellationToken);
            aggregate.Found += result.Found;
            aggregate.Added += result.Added;
            aggregate.AlreadyDownloaded += result.AlreadyDownloaded;
            aggregate.AlreadyQueued += result.AlreadyQueued;
            aggregate.Retried += result.Retried;
            aggregate.Invalid += result.Invalid;
            messages.Add(sources.Count == 1 ? result.Message : $"{source.Name}: {result.Message}");
        }

        aggregate.Message = string.Join(" ", messages);
        await RememberAsync(aggregate, cancellationToken);
        logger.LogInformation("[Gist] Kontrol tamamlandı.");
        return aggregate;
    }

    private async Task RememberAsync(GistImportResult result, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.ApplicationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
            return;

        settings.GistLastCheckedUtc = DateTime.UtcNow;
        settings.GistLastSucceeded = result.Succeeded;
        settings.GistLastMessage = result.Message.Length <= 300 ? result.Message : result.Message[..300];
        await db.SaveChangesAsync(cancellationToken);
    }
}
