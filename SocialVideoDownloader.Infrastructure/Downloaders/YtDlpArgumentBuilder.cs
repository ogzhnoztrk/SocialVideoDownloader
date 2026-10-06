using SocialVideoDownloader.Core.DTOs;

namespace SocialVideoDownloader.Infrastructure.Downloaders;

public static class YtDlpArgumentBuilder
{
    public static IReadOnlyList<string> BuildInfoArguments(string url)
    {
        return
        [
            "--dump-single-json",
            "--no-download",
            "--no-warnings",
            "--no-playlist",
            "--no-update",
            "--encoding",
            "utf-8",
            "--",
            url,
        ];
    }

    public static IReadOnlyList<string> BuildDownloadArguments(
        DownloadRequest request,
        string? ffmpegDirectory,
        string? cookieFile)
    {
        var arguments = new List<string>
        {
            "--no-playlist",
            "--no-update",
            "--no-warnings",
            "--newline",
            "--progress",
            "--progress-template",
            "download:SVDPROGRESS %(progress._percent_str)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s",
            "--print",
            "after_move:SVDOUT %(filepath)s",
            "--no-simulate",
            "--windows-filenames",
            "--trim-filenames",
            "80",
            "-o",
            request.OutputTemplate,
        };

        if (!string.IsNullOrWhiteSpace(ffmpegDirectory))
        {
            arguments.Add("-f");
            arguments.Add("bv*+ba/b");
            arguments.Add("--merge-output-format");
            arguments.Add("mp4");
            arguments.Add("--ffmpeg-location");
            arguments.Add(ffmpegDirectory);
        }
        else
        {
            arguments.Add("-f");
            arguments.Add("b");
        }

        arguments.Add(request.Overwrite ? "--force-overwrites" : "--no-overwrites");

        if (!string.IsNullOrWhiteSpace(cookieFile))
        {
            arguments.Add("--cookies");
            arguments.Add(cookieFile);
        }

        arguments.Add("--");
        arguments.Add(request.Url);
        return arguments;
    }
}
