using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;
using SocialVideoDownloader.Core.Enums;
using SocialVideoDownloader.Core.Gist;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Validation;
using SocialVideoDownloader.Infrastructure.Data;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Tests;

public class GistQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "svd-gist-" + Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly FakeGistClient _gist = new();

    public GistQueueTests()
    {
        Directory.CreateDirectory(_root);
        var database = Path.Combine(_root, "test.db");
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={database}"));
        collection.AddSingleton<DownloadCancellationRegistry>();
        collection.AddSingleton<VideoInfoCache>();
        collection.AddSingleton<IVideoDownloader, UniqueFakeDownloader>();
        collection.AddSingleton<IDownloaderBinaryManager, IdleBinaries>();
        collection.AddSingleton<WindowsServiceCoordinator>();
        collection.AddSingleton<IGistClient>(_gist);
        collection.AddScoped<ISettingsService, SettingsService>();
        collection.AddScoped<ICategoryService, CategoryService>();
        collection.AddScoped<IDownloadJobService, DownloadJobService>();
        collection.AddScoped<IUrlListImportService, UrlListImportService>();
        collection.AddScoped<IGistImportService, GistImportService>();
        _services = collection.BuildServiceProvider();
        using var scope = _services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public void Parser_skips_comments_blanks_and_invalid_lines()
    {
        var lines = GistListParser.Parse("""
            # Instagram
            https://instagram.com/reel/AAA

            abc123
            https://www.tiktok.com/@user/video/BBB/
            """);

        Assert.Equal(3, lines.Count);
        Assert.True(lines[0].IsValid);
        Assert.Equal("https://instagram.com/reel/AAA", lines[0].CanonicalUrl);
        Assert.False(lines[1].IsValid);
        Assert.Equal("https://tiktok.com/@user/video/BBB", lines[2].CanonicalUrl);
    }

    [Theory]
    [InlineData("https://gist.github.com/username/abcdef1234567890", "https://gist.githubusercontent.com/username/abcdef1234567890/raw")]
    [InlineData("https://gist.githubusercontent.com/username/abcdef1234567890/raw", "https://gist.githubusercontent.com/username/abcdef1234567890/raw")]
    [InlineData("https://gist.github.com/username/abcdef1234567890/raw/list.txt", "https://gist.githubusercontent.com/username/abcdef1234567890/raw/list.txt")]
    public void Resolver_accepts_public_gist_addresses(string input, string expected) =>
        Assert.True(GistUrlResolver.TryGetRawUrl(input, out var raw) && raw == expected);

    [Theory]
    [InlineData("https://example.com/username/abcdef1234567890")]
    [InlineData("http://gist.github.com/username/abcdef1234567890")]
    [InlineData("https://gist.github.com/username")]
    public void Resolver_rejects_other_hosts(string input) =>
        Assert.False(GistUrlResolver.TryGetRawUrl(input, out _));

    [Fact]
    public void Canonical_url_ignores_www_and_trailing_slash()
    {
        Assert.True(UrlGuard.TryCanonical("https://www.instagram.com/reel/ABC/", out var left));
        Assert.True(UrlGuard.TryCanonical("https://instagram.com/reel/ABC", out var right));
        Assert.Equal(left, right);
    }

    [Fact]
    public async Task New_gist_url_becomes_a_pending_job()
    {
        await SaveSettingsAsync();
        _gist.Content = "https://instagram.com/reel/ABC";

        var result = await CheckAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Added);
        var jobs = await JobsAsync();
        var job = Assert.Single(jobs);
        Assert.Equal(DownloadSource.Gist, job.Source);
        Assert.Equal(DownloadStatus.Pending, job.Status);
        Assert.Equal("https://instagram.com/reel/ABC", job.NormalizedUrl);
    }

    [Fact]
    public async Task The_same_gist_url_is_not_queued_again()
    {
        await SaveSettingsAsync();
        _gist.Content = "https://www.instagram.com/reel/ABC/";
        await CheckAsync();
        _gist.Content = "https://instagram.com/reel/ABC";

        var result = await CheckAsync();

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.AlreadyQueued);
        Assert.Single(await JobsAsync());
    }

    [Fact]
    public async Task Only_the_new_url_is_added_on_the_next_check()
    {
        await SaveSettingsAsync();
        _gist.Content = "https://instagram.com/reel/ABC";
        await CheckAsync();
        _gist.Content = """
            https://instagram.com/reel/ABC
            https://www.tiktok.com/@user/video/XYZ
            """;

        var result = await CheckAsync();

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.AlreadyQueued);
        var jobs = await JobsAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, job => job.NormalizedUrl == "https://tiktok.com/@user/video/XYZ");
    }

    [Fact]
    public async Task Invalid_lines_are_skipped()
    {
        await SaveSettingsAsync();
        _gist.Content = """
            abc123
            # yorum
            https://instagram.com/reel/ABC
            """;

        var result = await CheckAsync();

        Assert.Equal(1, result.Invalid);
        Assert.Equal(1, result.Added);
        Assert.Single(await JobsAsync());
    }

    [Fact]
    public async Task A_failed_url_is_retried_on_the_same_job()
    {
        await SaveSettingsAsync();
        await using (var scope = _services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.DownloadJobs.Add(new DownloadJob
            {
                Id = Guid.NewGuid(),
                Url = "https://instagram.com/reel/ABC",
                NormalizedUrl = "https://instagram.com/reel/ABC",
                Source = DownloadSource.Gist,
                Platform = Platform.Instagram,
                Title = "Ornek video",
                VideoId = "reel-abc",
                OutputDirectory = _root,
                Status = DownloadStatus.Failed,
                RetryCount = 1,
                ErrorMessage = "geçici",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        _gist.Content = "https://instagram.com/reel/ABC";
        var result = await CheckAsync();

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Retried);
        var job = Assert.Single(await JobsAsync());
        Assert.Equal(DownloadStatus.Pending, job.Status);
        Assert.Equal(2, job.RetryCount);
    }

    [Fact]
    public async Task A_failed_url_stops_after_three_retries()
    {
        await SaveSettingsAsync();
        await using (var scope = _services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            db.DownloadJobs.Add(new DownloadJob
            {
                Id = Guid.NewGuid(),
                Url = "https://instagram.com/reel/ABC",
                NormalizedUrl = "https://instagram.com/reel/ABC",
                Source = DownloadSource.Gist,
                Platform = Platform.Instagram,
                Title = "Ornek video",
                VideoId = "reel-abc",
                OutputDirectory = _root,
                Status = DownloadStatus.Failed,
                RetryCount = 3,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        _gist.Content = "https://instagram.com/reel/ABC";
        var result = await CheckAsync();

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Retried);
        var job = Assert.Single(await JobsAsync());
        Assert.Equal(DownloadStatus.Failed, job.Status);
    }

    [Fact]
    public async Task Each_category_downloads_from_its_own_gist()
    {
        await SaveSettingsAsync(includeGist: false);
        int sadId;
        int shitId;
        await using (var scope = _services.CreateAsyncScope())
        {
            var categories = scope.ServiceProvider.GetRequiredService<ICategoryService>();
            var sad = await categories.CreateAsync("sad_post", CancellationToken.None);
            var shit = await categories.CreateAsync("shitpost", CancellationToken.None);
            sadId = sad.Id;
            shitId = shit.Id;
            await categories.SetGistAsync(sadId, "https://gist.github.com/username/aaaaaaaaaaaaaaaa", CancellationToken.None);
            await categories.SetGistAsync(shitId, "https://gist.github.com/username/bbbbbbbbbbbbbbbb", CancellationToken.None);
        }

        _gist.ByUrl["https://gist.github.com/username/aaaaaaaaaaaaaaaa"] = "https://instagram.com/reel/SAD";
        _gist.ByUrl["https://gist.github.com/username/bbbbbbbbbbbbbbbb"] = "https://tiktok.com/@user/video/SHIT";

        var result = await CheckAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Added);
        Assert.Contains("sad_post", result.Message);
        Assert.Contains("shitpost", result.Message);
        var jobs = await JobsAsync();
        Assert.Contains(jobs, job => job.CategoryName == "sad_post" && job.NormalizedUrl == "https://instagram.com/reel/SAD");
        Assert.Contains(jobs, job => job.CategoryName == "shitpost" && job.NormalizedUrl == "https://tiktok.com/@user/video/SHIT");
    }

    [Fact]
    public async Task An_unreachable_gist_does_not_throw()
    {
        await SaveSettingsAsync();
        _gist.Fail = true;

        var result = await CheckAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Gist dosyasına erişilemedi.", result.Message);
        Assert.Empty(await JobsAsync());
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

    private async Task SaveSettingsAsync(bool includeGist = true)
    {
        await using var scope = _services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        db.ApplicationSettings.Add(new ApplicationSettings
        {
            Id = 1,
            DownloadDirectory = _root,
            FileNameTemplate = "%(title)s.%(ext)s",
            WebPort = 5180,
            GistUrl = includeGist ? "https://gist.github.com/username/abcdef1234567890" : null,
            GistPollingEnabled = true,
            GistPollIntervalMinutes = 60,
        });
        await db.SaveChangesAsync();
    }

    private async Task<GistImportResult> CheckAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<IGistImportService>();
        return await importer.CheckNowAsync(CancellationToken.None);
    }

    private async Task<List<DownloadJob>> JobsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        return await db.DownloadJobs.AsNoTracking().ToListAsync();
    }

    private sealed class FakeGistClient : IGistClient
    {
        public string Content { get; set; } = string.Empty;

        public Dictionary<string, string> ByUrl { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Fail { get; set; }

        public Task<string> GetContentAsync(string gistUrl, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new HttpRequestException("offline");

            if (ByUrl.TryGetValue(gistUrl, out var content))
                return Task.FromResult(content);

            return Task.FromResult(Content);
        }
    }

    private sealed class UniqueFakeDownloader : IVideoDownloader
    {
        public Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(new VideoInfoResult
            {
                Url = url,
                Platform = Platform.Instagram,
                PlatformName = "Instagram",
                Title = "Ornek video",
                Uploader = "user",
                VideoId = url,
                DurationSeconds = 10,
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
