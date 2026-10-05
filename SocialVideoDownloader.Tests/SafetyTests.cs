using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Files;
using SocialVideoDownloader.Core.Platforms;
using SocialVideoDownloader.Core.Validation;
using SocialVideoDownloader.Infrastructure.Downloaders;
using SocialVideoDownloader.Infrastructure.Services;

namespace SocialVideoDownloader.Tests;

public class SafetyTests
{
    [Theory]
    [InlineData("https://www.instagram.com/reel/abc/", "Instagram", "Instagram")]
    [InlineData("https://instagr.am/p/abc/", "Instagram", "Instagram")]
    [InlineData("https://vm.tiktok.com/Zabc/", "TikTok", "TikTok")]
    [InlineData("https://vt.tiktok.com/Zabc/", "TikTok", "TikTok")]
    [InlineData("https://twitter.com/user/status/1", "Twitter", "Twitter")]
    [InlineData("https://x.com/user/status/1", "X", "Twitter")]
    [InlineData("https://example.com/video", "Diğer", "Other")]
    public void Platform_detection_maps_known_hosts(string url, string display, string folder)
    {
        var platform = PlatformResolver.Detect(url);
        Assert.Equal(display, PlatformResolver.GetDisplayName(platform));
        Assert.Equal(folder, PlatformResolver.GetFolderName(platform));
    }

    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("localhost", "127.0.0.1")]
    [InlineData(" 127.0.0.1 ", "127.0.0.1")]
    public void Listen_address_accepts_loopback(string input, string expected) =>
        Assert.Equal(expected, ListenAddressGuard.Normalize(input));

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    public void Listen_address_rejects_non_local_values(string input)
    {
        Assert.Throws<Core.Exceptions.DownloadException>(() => ListenAddressGuard.Normalize(input));
    }

    [Theory]
    [InlineData("https://www.instagram.com/reel/abc/")]
    [InlineData("http://example.com/watch")]
    public void Url_guard_accepts_public_http_urls(string url) =>
        Assert.True(UrlGuard.TryNormalize(url, out _));

    [Theory]
    [InlineData("file:///C:/secret.mp4")]
    [InlineData("https://127.0.0.1/video")]
    [InlineData("https://192.168.1.10/video")]
    [InlineData("https://user:pass@example.com/video")]
    [InlineData("https://example.com/video\n--exec")]
    [InlineData("notaurl")]
    public void Url_guard_rejects_unsafe_urls(string url) =>
        Assert.False(UrlGuard.TryNormalize(url, out _));

    [Fact]
    public void File_names_drop_windows_invalid_characters()
    {
        var sanitized = FileNameSanitizer.Sanitize("a/b:c*?\"<>|d");
        Assert.Equal("abcd", sanitized);
        Assert.DoesNotContain(Path.GetInvalidFileNameChars(), character => sanitized.Contains(character));
    }

    [Fact]
    public void Paths_cannot_escape_the_download_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "svd-root");
        Directory.CreateDirectory(root);
        Assert.Equal(Path.GetFullPath(root), PathSafety.RequireDownloadRoot(root));
        Assert.Throws<Core.Exceptions.DownloadException>(() => PathSafety.EnsureUnderRoot(root, Path.Combine(root, "..", "outside.txt")));
    }

    [Fact]
    public void File_template_rejects_traversal() =>
        Assert.Throws<Core.Exceptions.DownloadException>(() => OutputPathBuilder.ValidateTemplate(@"..\secret.mp4"));

    [Fact]
    public void Download_arguments_keep_the_url_as_one_value()
    {
        var url = "https://www.instagram.com/reel/abc/?x=1 --exec calc";
        var arguments = YtDlpArgumentBuilder.BuildDownloadArguments(
            new Core.DTOs.DownloadRequest
            {
                Url = url,
                OutputTemplate = Path.Combine(Path.GetTempPath(), AppConstants.DefaultFileNameTemplate),
                Platform = Core.Enums.Platform.Instagram,
            },
            ffmpegDirectory: null,
            cookieFile: null);

        Assert.Equal("--", arguments[^2]);
        Assert.Equal(url, arguments[^1]);
        Assert.Contains("-f", arguments);
        Assert.Contains("b", arguments);
    }

    [Theory]
    [InlineData("ERROR: Private video", "Video private.")]
    [InlineData("Use --cookies-from-browser or --cookies for the authentication", "Video giriş gerektiriyor.")]
    [InlineData("Video unavailable", "Video artık mevcut değil.")]
    [InlineData("Unsupported URL: https://example.test", "Platform desteklenmiyor.")]
    [InlineData("ERROR: ffmpeg not found", "FFmpeg bulunamadı.")]
    [InlineData("ERROR: Unable to extract video", "Video bulunamadı.")]
    [InlineData("something else failed", "yt-dlp hatası.")]
    public void Yt_dlp_errors_are_translated(string output, string expected) =>
        Assert.Equal(expected, YtDlpErrorTranslator.Translate(output));

    [Fact]
    public void Progress_lines_are_parsed()
    {
        var parsed = YtDlpOutputParser.TryParseProgress("SVDPROGRESS  72.5%|18400000|25500000|NA", out var progress);
        Assert.True(parsed);
        Assert.Equal(72.5, progress.Percent);
        Assert.Equal(18400000, progress.BytesDownloaded);
        Assert.Equal(25500000, progress.TotalBytes);
        Assert.True(YtDlpOutputParser.TryParseOutputPath(@"SVDOUT C:\Videos\clip.mp4", out var path));
        Assert.Equal(@"C:\Videos\clip.mp4", path);
    }
}
