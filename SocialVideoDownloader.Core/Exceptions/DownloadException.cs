namespace SocialVideoDownloader.Core.Exceptions;

public sealed class DownloadException : Exception
{
    public DownloadException(string userMessage, string? technicalDetail = null, Exception? innerException = null)
        : base(string.IsNullOrWhiteSpace(technicalDetail) ? userMessage : technicalDetail, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
