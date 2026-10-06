using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class GistImportService(
    IGistClient gistClient,
    IUrlListImportService lists,
    ISettingsService settingsService,
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
        var settings = await settingsService.GetEntityAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.GistUrl))
        {
            var empty = new GistImportResult { Message = "Gist adresi boş." };
            await RememberAsync(empty, cancellationToken);
            logger.LogWarning("[Gist] Gist kontrolü başarısız: Gist adresi boş.");
            return empty;
        }

        string content;
        try
        {
            content = await gistClient.GetContentAsync(settings.GistUrl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "[Gist] Gist kontrolü başarısız: {Message}", exception.Message);
            var failed = new GistImportResult { Message = "Gist dosyasına erişilemedi." };
            await RememberAsync(failed, cancellationToken);
            return failed;
        }

        logger.LogInformation("[Gist] Gist içeriği alındı.");
        var result = await lists.ImportAsync(content, null, DownloadSource.Gist, "[Gist]", cancellationToken);
        await RememberAsync(result, cancellationToken);
        logger.LogInformation("[Gist] Kontrol tamamlandı.");
        return result;
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
