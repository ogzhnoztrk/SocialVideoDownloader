using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Files;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Infrastructure.Data;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Tests;

public class CategoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "svd-cat-" + Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;

    public CategoryTests()
    {
        Directory.CreateDirectory(_root);
        var database = Path.Combine(_root, "test.db");
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={database}"));
        collection.AddSingleton<DownloadCancellationRegistry>();
        collection.AddSingleton<VideoInfoCache>();
        collection.AddSingleton<IVideoDownloader, NamedFakeDownloader>();
        collection.AddSingleton<IDownloaderBinaryManager, IdleBinaries>();
        collection.AddSingleton<WindowsServiceCoordinator>();
        collection.AddScoped<ISettingsService, SettingsService>();
        collection.AddScoped<ICategoryService, CategoryService>();
        collection.AddScoped<IDownloadJobService, DownloadJobService>();
        collection.AddScoped<IUrlListImportService, UrlListImportService>();
        _services = collection.BuildServiceProvider();
        using var scope = _services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Database.EnsureCreated();
        db.ApplicationSettings.Add(new ApplicationSettings
        {
            Id = 1,
            DownloadDirectory = _root,
            FileNameTemplate = "%(title)s.%(ext)s",
            WebPort = 5180,
        });
        db.SaveChanges();
    }

    [Theory]
    [InlineData("turkce_meme", "turkce_meme")]
    [InlineData("  shitpost  ", "shitpost")]
    [InlineData("turkce meme", "turkce_meme")]
    public void Category_names_become_folder_names(string input, string expected) =>
        Assert.Equal(expected, CategoryFolder.Normalize(input));

    [Theory]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("   ")]
    public void Unsafe_category_names_are_rejected(string input) =>
        Assert.Throws<DownloadException>(() => CategoryFolder.Normalize(input));

    [Fact]
    public void Each_category_keeps_a_separate_platform_folder()
    {
        var settings = new ApplicationSettings
        {
            DownloadDirectory = _root,
            FileNameTemplate = "%(title)s.%(ext)s",
        };

        var instagram = OutputPathBuilder.BuildDirectory(settings, Platform.Instagram, DateTimeOffset.Now, "turkce_meme");
        var tiktok = OutputPathBuilder.BuildDirectory(settings, Platform.TikTok, DateTimeOffset.Now, "turkce_meme");
        var twitter = OutputPathBuilder.BuildDirectory(settings, Platform.X, DateTimeOffset.Now, "shitpost");

        Assert.EndsWith(Path.Combine("turkce_meme", "Instagram"), instagram, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("turkce_meme", "TikTok"), tiktok, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("shitpost", "Twitter"), twitter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manual_download_uses_the_selected_category_and_gist_uses_the_default()
    {
        await using var scope = _services.CreateAsyncScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var meme = await categories.CreateAsync("turkce_meme", CancellationToken.None);
        var shitpost = await categories.CreateAsync("shitpost", CancellationToken.None);
        await categories.SetDefaultAsync(shitpost.Id, CancellationToken.None);

        var jobs = scope.ServiceProvider.GetRequiredService<IDownloadJobService>();
        var manual = await jobs.CreateAsync(
            "https://www.instagram.com/reel/abc/",
            CancellationToken.None,
            DownloadSource.Manual,
            meme.Id);
        var bulk = await jobs.CreateAsync(
            "https://www.tiktok.com/@user/video/xyz",
            CancellationToken.None);

        Assert.Equal("turkce_meme", manual.Job.CategoryName);
        Assert.Contains(Path.Combine("turkce_meme", "Instagram"), manual.Job.OutputDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("shitpost", bulk.Job.CategoryName);
        Assert.Contains(Path.Combine("shitpost", "TikTok"), bulk.Job.OutputDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_text_list_queues_new_urls_into_the_selected_category()
    {
        await using var scope = _services.CreateAsyncScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var meme = await categories.CreateAsync("turkce_meme", CancellationToken.None);
        var lists = scope.ServiceProvider.GetRequiredService<IUrlListImportService>();
        const string text = """
            # yorum

            https://www.instagram.com/reel/abc/
            abc123
            https://www.tiktok.com/@user/video/xyz
            """;

        var first = await lists.ImportAsync(text, meme.Id, DownloadSource.File, "[Dosya]", CancellationToken.None);
        var second = await lists.ImportAsync(text, meme.Id, DownloadSource.File, "[Dosya]", CancellationToken.None);

        Assert.Equal(2, first.Added);
        Assert.Equal(1, first.Invalid);
        Assert.Equal(0, second.Added);
        Assert.Equal(2, second.AlreadyQueued);

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var jobs = await db.DownloadJobs.AsNoTracking().ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, job => Assert.Equal(DownloadSource.File, job.Source));
        Assert.All(jobs, job => Assert.Equal("turkce_meme", job.CategoryName));
        Assert.Contains(jobs, job => job.OutputDirectory.Contains(Path.Combine("turkce_meme", "Instagram"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(jobs, job => job.OutputDirectory.Contains(Path.Combine("turkce_meme", "TikTok"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_last_category_cannot_be_deleted()
    {
        await using var scope = _services.CreateAsyncScope();
        var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var only = Assert.Single(await categories.ListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<DownloadException>(() => categories.DeleteAsync(only.Id, CancellationToken.None));
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

    private sealed class NamedFakeDownloader : IVideoDownloader
    {
        public Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoInfoResult
            {
                Url = url,
                Platform = url.Contains("tiktok", StringComparison.OrdinalIgnoreCase) ? Platform.TikTok : Platform.Instagram,
                PlatformName = "Test",
                Title = "Ornek video",
                VideoId = url,
                Format = "MP4",
            });

        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new DownloadResult { FilePath = Path.Combine(Path.GetTempPath(), "unused.mp4") });
    }

    private sealed class IdleBinaries : IDownloaderBinaryManager
    {
        public string? ResolveYtDlpPath(ApplicationSettings settings) => "yt-dlp";
        public string? ResolveFfmpegDirectory(ApplicationSettings settings) => null;
        public Task<BinaryStatusDto> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new BinaryStatusDto());
        public Task InstallYtDlpAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task InstallFfmpegAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateYtDlpIfNeededAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
