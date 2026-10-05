using System.Diagnostics;
using System.Text;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Exceptions;

namespace SocialVideoDownloader.Infrastructure.Downloaders;

internal sealed record YtDlpRunResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class YtDlpProcessRunner
{
    public async Task<YtDlpRunResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        Action<string>? onOutput,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable) ||
            !File.Exists(executable) ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new DownloadException(UserMessages.YtDlpMissing);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        foreach (var argument in arguments)
        {
            if (argument.Contains('\0') || argument.Contains('\r') || argument.Contains('\n'))
                throw new DownloadException(UserMessages.InvalidUrl);

            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new DownloadException(UserMessages.YtDlpError, "yt-dlp process did not start.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutTask = PumpAsync(process.StandardOutput, stdout, onOutput, cancellationToken);
        var stderrTask = PumpAsync(process.StandardError, stderr, onOutput, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new DownloadException(UserMessages.Cancelled);
        }

        return new YtDlpRunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StringBuilder buffer,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                return;

            lock (buffer)
                buffer.AppendLine(line);

            onLine?.Invoke(line);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
