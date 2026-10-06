using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Configuration;
using SocialVideoDownloader.Infrastructure.Data;
using SocialVideoDownloader.Infrastructure.Downloaders;
using SocialVideoDownloader.Infrastructure.Gist;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSocialVideoDownloader(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DownloaderOptions>(configuration.GetSection(DownloaderOptions.SectionName));
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(DataPaths.ConnectionString)
                .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddHttpClient("binaries", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SocialVideoDownloader/1.0");
        });
        services.AddHttpClient(HttpGistClient.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SocialVideoDownloader/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        });

        services.AddSingleton<ActiveEndpoint>();
        services.AddSingleton<DownloadCancellationRegistry>();
        services.AddSingleton<VideoInfoCache>();
        services.AddSingleton<WindowsServiceCoordinator>();
        services.AddSingleton<ICookieProvider, NullCookieProvider>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<StatusService>();
        services.AddScoped<IDownloaderBinaryManager, DownloaderBinaryManager>();
        services.AddScoped<IVideoDownloader, YtDlpVideoDownloader>();
        services.AddScoped<IDownloadJobService, DownloadJobService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IGistClient, HttpGistClient>();
        services.AddScoped<IUrlListImportService, UrlListImportService>();
        services.AddScoped<IGistImportService, GistImportService>();
        services.AddHostedService<BinaryMaintenanceService>();
        services.AddHostedService<DownloadWorker>();
        services.AddHostedService<GistPollingService>();
        return services;
    }
}
