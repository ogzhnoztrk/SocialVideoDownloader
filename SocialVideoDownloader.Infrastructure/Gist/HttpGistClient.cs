using SocialVideoDownloader.Core.Exceptions;
using SocialVideoDownloader.Core.Gist;
using SocialVideoDownloader.Core.Interfaces;

namespace SocialVideoDownloader.Infrastructure.Gist;

public sealed class HttpGistClient(IHttpClientFactory httpClientFactory) : IGistClient
{
    public const string ClientName = "gist";
    private const int MaxCharacters = 200_000;

    public async Task<string> GetContentAsync(string gistUrl, CancellationToken cancellationToken)
    {
        if (!GistUrlResolver.TryGetRawUrl(gistUrl, out var rawUrl))
            throw new DownloadException("Gist adresi geçersiz. Public gist bağlantısı girin.");

        var client = httpClientFactory.CreateClient(ClientName);
        using var response = await client.GetAsync(rawUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DownloadException("Gist dosyasına erişilemedi.");

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (text.Length > MaxCharacters)
            throw new DownloadException("Gist dosyası çok büyük.");

        return text;
    }
}
