using SocialVideoDownloader.Core.Gist;
using SocialVideoDownloader.Core.Interfaces;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class GistPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<GistPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromHours(1);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var current = await settings.GetEntityAsync(stoppingToken);
                delay = GistSchedule.Interval(current.GistPollIntervalMinutes);
                if (current.GistPollingEnabled)
                {
                    var importer = scope.ServiceProvider.GetRequiredService<IGistImportService>();
                    await importer.CheckNowAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "[Gist] Gist kontrolü başarısız: {Message}", exception.Message);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
