using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Data;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Tests;

public class DownloadFlowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "svd-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _database;
    private readonly ServiceProvider _services;

    public DownloadFlowTests()
    {
        Directory.CreateDirectory(_root);
        _database = Path.Combine(_root, "test.db");
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={_database}"));
        collection.AddSingleton<DownloadCancellationRegistry>();
        collection.AddSingleton<VideoInfoCache>();
        collection.AddSingleton(new FakeDownloader());
        collection.AddSingleton<IVideoDownloader>(provider => provider.GetRequiredService<FakeDownloader>());
        collection.AddSingleton<IDownloaderBinaryManager, FakeBinaries>();
        collection.AddSingleton<WindowsServiceCoordinator>();
        collection.AddScoped<ISettingsService, SettingsService>();
        collection.AddScoped<IDownloadJobService, DownloadJobService>();
        _services = collection.BuildServiceProvider();
        using var scope = _services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Creating_a_job_queues_it_without_downloading_inline()
    {
        await using var scope = _services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.ApplicationSettings.Add(new ApplicationSettings
            {
                Id = 1,
                DownloadDirectory = _root,
                FileNameTemplate = "%(title)s.%(ext)s",
                DuplicateCheckEnabled = true,
                WebPort = 5180,
            });
            await db.SaveChangesAsync();
        }

        var jobs = scope.ServiceProvider.GetRequiredService<IDownloadJobService>();
        var created = await jobs.CreateAsync("https://www.instagram.com/reel/abc/", CancellationToken.None);

        Assert.False(created.AlreadyExists);
        Assert.Equal(DownloadStatus.Pending, created.Job.Status);
        Assert.Equal("Ornek video", created.Job.Title);

        var claimed = await jobs.ClaimNextAsync(CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.Equal(DownloadStatus.Downloading, claimed.Status);
        Assert.Null(await jobs.ClaimNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Completed_video_is_not_queued_again()
    {
        var file = Path.Combine(_root, "existing.mp4");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        await using var scope = _services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        db.DownloadJobs.Add(new DownloadJob
        {
            Id = Guid.NewGuid(),
            Url = "https://www.instagram.com/reel/abc/",
            Platform = Platform.Instagram,
            Title = "Ornek video",
            VideoId = "abc",
            OutputDirectory = _root,
            FilePath = file,
            Status = DownloadStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        });
        db.ApplicationSettings.Add(new ApplicationSettings
        {
            Id = 1,
            DownloadDirectory = _root,
            FileNameTemplate = "%(title)s.%(ext)s",
            DuplicateCheckEnabled = true,
            WebPort = 5180,
        });
        await db.SaveChangesAsync();

        var jobs = scope.ServiceProvider.GetRequiredService<IDownloadJobService>();
        var created = await jobs.CreateAsync("https://www.instagram.com/reel/abc/", CancellationToken.None);
        Assert.True(created.AlreadyExists);
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakeDownloader : IVideoDownloader
    {
        public Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoInfoResult
            {
                Url = url,
                Platform = Platform.Instagram,
                PlatformName = "Instagram",
                Title = "Ornek video",
                Uploader = "user",
                VideoId = "abc",
                DurationSeconds = 37,
                Resolution = "1080x1920",
                Format = "MP4",
            });

        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new DownloadResult { FilePath = Path.Combine(Path.GetTempPath(), "unused.mp4") });
    }

    private sealed class FakeBinaries : IDownloaderBinaryManager
    {
        public string? ResolveYtDlpPath(ApplicationSettings settings) => null;
        public string? ResolveFfmpegDirectory(ApplicationSettings settings) => null;
        public Task<BinaryStatusDto> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new BinaryStatusDto());
        public Task InstallYtDlpAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task InstallFfmpegAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateYtDlpIfNeededAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
