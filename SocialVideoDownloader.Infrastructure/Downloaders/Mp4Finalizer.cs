using System.Diagnostics;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Logging;

namespace SocialVideoDownloader.Infrastructure.Downloaders;

public static class Mp4Finalizer
{
    public static async Task<string> EnsureAsync(
        string path,
        string? ffmpegDirectory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            return path;

        var ffmpeg = ResolveFfmpeg(ffmpegDirectory);
        var target = Path.ChangeExtension(path, ".mp4");
        var temporary = Path.Combine(
            Path.GetDirectoryName(target) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(target) + ".converting.mp4");
        if (File.Exists(temporary))
            File.Delete(temporary);

        logger.LogInformation("Converting download to mp4. Source={Source}", LogSanitizer.Text(Path.GetFileName(path), 180));
        var remuxed = await RunAsync(
            ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-y", "-i", path, "-c", "copy", "-movflags", "+faststart", temporary],
            cancellationToken);

        if (remuxed != 0 || !File.Exists(temporary) || new FileInfo(temporary).Length == 0)
        {
            if (File.Exists(temporary))
                File.Delete(temporary);

            var recoded = await RunAsync(
                ffmpeg,
                ["-hide_banner", "-loglevel", "error", "-y", "-i", path, "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-c:a", "aac", "-movflags", "+faststart", temporary],
                cancellationToken);
            if (recoded != 0 || !File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new DownloadException("Video MP4 olarak kaydedilemedi.");
        }

        if (File.Exists(target))
            File.Delete(target);

        File.Move(temporary, target);
        if (!string.Equals(path, target, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            File.Delete(path);

        return target;
    }

    private static string ResolveFfmpeg(string? ffmpegDirectory)
    {
        if (string.IsNullOrWhiteSpace(ffmpegDirectory))
            throw new DownloadException(UserMessages.FfmpegMissing);

        var ffmpeg = Path.Combine(ffmpegDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpeg))
            throw new DownloadException(UserMessages.FfmpegMissing);

        return ffmpeg;
    }

    private static async Task<int> RunAsync(string ffmpeg, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            return -1;

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
            }

            throw new DownloadException(UserMessages.Cancelled);
        }
    }
}
