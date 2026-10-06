namespace SocialVideoDownloader.Core.Interfaces;

public interface IGistClient
{
    Task<string> GetContentAsync(string gistUrl, CancellationToken cancellationToken);
}
