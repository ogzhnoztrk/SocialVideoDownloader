using System.Text.Json;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Interfaces;
using SocialVideoDownloader.Core.Logging;
using SocialVideoDownloader.Core.Platforms;
using SocialVideoDownloader.Core.Validation;

namespace SocialVideoDownloader.Infrastructure.Downloaders;

public sealed class YtDlpVideoDownloader(
    ISettingsService settingsService,
    IDownloaderBinaryManager binaries,
    ICookieProvider cookies,
    ILogger<YtDlpVideoDownloader> logger) : IVideoDownloader
{
    private readonly YtDlpProcessRunner _runner = new();

    public async Task<VideoInfoResult> GetVideoInfoAsync(string url, CancellationToken cancellationToken)
    {
        if (!UrlGuard.TryNormalize(url, out var normalized))
            throw new DownloadException(UserMessages.InvalidUrl);

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var executable = binaries.ResolveYtDlpPath(settings) ?? throw new DownloadException(UserMessages.YtDlpMissing);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));

        YtDlpRunResult result;
        try
        {
            result = await _runner.RunAsync(executable, YtDlpArgumentBuilder.BuildInfoArguments(normalized), null, timeout.Token);
        }
        catch (DownloadException exception) when (exception.UserMessage == UserMessages.Cancelled && !cancellationToken.IsCancellationRequested)
        {
            throw new DownloadException(UserMessages.YtDlpError, "yt-dlp info timed out.");
        }

        if (result.ExitCode != 0)
        {
            var technical = LogSanitizer.Text(result.StandardError + Environment.NewLine + result.StandardOutput);
            logger.LogWarning("yt-dlp info failed for {Url}. {Detail}", LogSanitizer.Url(normalized), technical);
            throw new DownloadException(YtDlpErrorTranslator.Translate(technical), technical);
        }

        return ParseInfo(result.StandardOutput, normalized, binaries.ResolveFfmpegDirectory(settings) is not null);
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!UrlGuard.TryNormalize(request.Url, out var normalized))
            throw new DownloadException(UserMessages.InvalidUrl);

        var settings = await settingsService.GetEntityAsync(cancellationToken);
        var executable = binaries.ResolveYtDlpPath(settings) ?? throw new DownloadException(UserMessages.YtDlpMissing);
        var ffmpegDirectory = binaries.ResolveFfmpegDirectory(settings);
        var cookieFile = ValidateCookieFile(cookies.GetCookieFilePath(request.Platform));
        var safeRequest = new DownloadRequest
        {
            Url = normalized,
            OutputTemplate = request.OutputTemplate,
            Overwrite = request.Overwrite,
            Platform = request.Platform,
        };

        string? outputPath = null;
        var startedAt = DateTime.UtcNow.AddSeconds(-2);
        var result = await _runner.RunAsync(
            executable,
            YtDlpArgumentBuilder.BuildDownloadArguments(safeRequest, ffmpegDirectory, cookieFile),
            line =>
            {
                if (YtDlpOutputParser.TryParseProgress(line, out var update))
                    progress?.Report(update);
                if (YtDlpOutputParser.TryParseOutputPath(line, out var path))
                    outputPath = path;
            },
            cancellationToken);

        if (result.ExitCode != 0)
        {
            var technical = LogSanitizer.Text(result.StandardError + Environment.NewLine + result.StandardOutput);
            logger.LogWarning("yt-dlp download failed for {Url}. {Detail}", LogSanitizer.Url(normalized), technical);
            throw new DownloadException(YtDlpErrorTranslator.Translate(technical), technical);
        }

        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
        {
            var fallback = FindNewestFile(Path.GetDirectoryName(request.OutputTemplate), startedAt);
            if (!string.IsNullOrWhiteSpace(fallback))
                outputPath = fallback;
        }

        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
            throw new DownloadException(UserMessages.VideoNotFound, "yt-dlp finished without an output file.");

        return new DownloadResult { FilePath = outputPath };
    }

    private static VideoInfoResult ParseInfo(string stdout, string url, bool ffmpegAvailable)
    {
        var json = ExtractJson(stdout);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var title = Clean(ReadString(root, "title")) ?? "Başlıksız";
        var uploader = Clean(ReadString(root, "uploader"))
            ?? Clean(ReadString(root, "channel"))
            ?? Clean(ReadString(root, "uploader_id"))
            ?? "Bilinmiyor";
        var width = ReadInt(root, "width");
        var height = ReadInt(root, "height");
        var extension = ReadString(root, "ext");
        var duration = ReadDouble(root, "duration");
        var platform = PlatformResolver.FromExtractor(ReadString(root, "extractor_key"), url);
        var format = ffmpegAvailable || string.Equals(extension, "mp4", StringComparison.OrdinalIgnoreCase)
            ? "MP4"
            : string.IsNullOrWhiteSpace(extension) ? "MP4" : extension.ToUpperInvariant();

        return new VideoInfoResult
        {
            Url = url,
            Platform = platform,
            PlatformName = PlatformResolver.GetDisplayName(platform),
            Title = title,
            Uploader = uploader,
            VideoId = ReadString(root, "id"),
            DurationSeconds = duration is null ? null : (int)Math.Round(duration.Value),
            Resolution = width is > 0 && height is > 0 ? $"{width}x{height}" : null,
            ThumbnailUrl = NormalizeThumbnail(ReadString(root, "thumbnail")),
            Format = format,
        };
    }

    private static string ExtractJson(string stdout)
    {
        var start = stdout.IndexOf('{');
        var end = stdout.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new DownloadException(UserMessages.YtDlpError, "yt-dlp did not return JSON.");

        return stdout[start..(end + 1)];
    }

    private static string? FindNewestFile(string? directory, DateTimeOffset notBeforeUtc)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        return Directory.EnumerateFiles(directory)
            .Select(path => new FileInfo(path))
            .Where(file =>
                file.Length > 0 &&
                file.LastWriteTimeUtc >= notBeforeUtc.UtcDateTime &&
                file.Extension is not ".part" and not ".ytdl" and not ".tmp")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static string? ValidateCookieFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\r') || path.Contains('\n') || path.Contains('"'))
            return null;

        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private static string? NormalizeThumbnail(string? thumbnail)
    {
        if (string.IsNullOrWhiteSpace(thumbnail) || thumbnail.Length > 2000)
            return null;

        return UrlGuard.TryNormalize(thumbnail, out var normalized) ? normalized : null;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        var text = builder.ToString().Trim();
        return text.Length <= 500 ? text : text[..500];
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            return value;

        return null;
    }

    private static double? ReadDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Number)
            return null;

        return property.TryGetDouble(out var value) ? value : null;
    }
}
