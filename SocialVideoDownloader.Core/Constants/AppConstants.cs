namespace SocialVideoDownloader.Core.Constants;

public static class AppConstants
{
    public const int DefaultPort = 5180;
    public const string DefaultListenAddress = "127.0.0.1";
    public const int TrayHelperPort = 5181;
    public const string ServiceName = "SocialVideoDownloader";
    public const string DefaultFileNameTemplate = "%(uploader)s_%(title)s_%(id)s.%(ext)s";
    public const string ApiHeaderName = "X-SVD-Request";
    public const string ApiHeaderValue = "1";
    public const long MinimumFreeBytes = 100L * 1024 * 1024;
    public const int DefaultGistPollMinutes = 60;
    public const int MaxGistRetryCount = 3;
}
