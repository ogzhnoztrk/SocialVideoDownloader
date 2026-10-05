using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Validation;
using SocialVideoDownloader.Infrastructure.Configuration;
using SocialVideoDownloader.Infrastructure.Data;

namespace SocialVideoDownloader.Infrastructure.Services;

public sealed class SettingsService(
    IDbContextFactory<AppDbContext> dbFactory,
    WindowsServiceCoordinator windowsServices,
    ILogger<SettingsService> logger) : ISettingsService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<AppSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        Map(await GetEntityAsync(cancellationToken));

    public async Task<ApplicationSettings> GetEntityAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.ApplicationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
            return settings;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var write = await dbFactory.CreateDbContextAsync(cancellationToken);
            settings = await write.ApplicationSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is not null)
                return settings;

            settings = ApplicationSettings.CreateDefault();
            write.ApplicationSettings.Add(settings);
            await write.SaveChangesAsync(cancellationToken);
            return settings;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<SettingsSaveResult> UpdateAsync(AppSettingsDto update, CancellationToken cancellationToken)
    {
        if (update.WebPort is < 1024 or > 65535)
            throw new DownloadException("Bağlantı noktası 1024 ile 65535 arasında olmalıdır.");

        var listenAddress = ListenAddressGuard.Normalize(update.ListenAddress);

        var directory = PathSafety.RequireDownloadRoot(update.DownloadDirectory);
        var template = OutputPathBuilder.ValidateTemplate(update.FileNameTemplate);
        Directory.CreateDirectory(directory);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.ApplicationSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = ApplicationSettings.CreateDefault();
            db.ApplicationSettings.Add(settings);
        }

        var previousPort = settings.WebPort;
        var previousAddress = settings.ListenAddress;
        settings.DownloadDirectory = directory;
        settings.FileNameTemplate = template;
        settings.UseDateFolders = update.UseDateFolders;
        settings.DuplicateCheckEnabled = update.DuplicateCheckEnabled;
        settings.YtDlpPath = BlankToNull(update.YtDlpPath);
        settings.FfmpegPath = BlankToNull(update.FfmpegPath);
        settings.AutoUpdateBinaries = update.AutoUpdateBinaries;
        settings.ListenAddress = listenAddress;
        settings.WebPort = update.WebPort;
        settings.OpenWebOnStartup = update.OpenWebOnStartup;
        settings.StartWithWindows = update.StartWithWindows;
        await db.SaveChangesAsync(cancellationToken);
        HostSettingsStore.Write(listenAddress, update.WebPort);

        string? warning = previousPort == update.WebPort &&
            string.Equals(previousAddress, listenAddress, StringComparison.OrdinalIgnoreCase)
            ? null
            : "IP veya port değişikliği yeniden başlatılınca uygulanır.";

        var serviceWarning = windowsServices.TrySetStartup(update.StartWithWindows);
        warning = Join(warning, serviceWarning);
        logger.LogInformation("Settings updated. DownloadDirectory={Directory} Port={Port}", directory, update.WebPort);
        return new SettingsSaveResult { Settings = Map(settings), Warning = warning };
    }

    private static AppSettingsDto Map(ApplicationSettings settings) => new()
    {
        DownloadDirectory = settings.DownloadDirectory,
        FileNameTemplate = settings.FileNameTemplate,
        UseDateFolders = settings.UseDateFolders,
        DuplicateCheckEnabled = settings.DuplicateCheckEnabled,
        YtDlpPath = settings.YtDlpPath,
        FfmpegPath = settings.FfmpegPath,
        AutoUpdateBinaries = settings.AutoUpdateBinaries,
        ListenAddress = settings.ListenAddress,
        WebPort = settings.WebPort,
        OpenWebOnStartup = settings.OpenWebOnStartup,
        StartWithWindows = settings.StartWithWindows,
    };

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Join(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right;
        if (string.IsNullOrWhiteSpace(right))
            return left;
        return left + " " + right;
    }
}
