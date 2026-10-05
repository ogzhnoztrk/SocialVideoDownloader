namespace SocialVideoDownloader.Infrastructure.Configuration;

public sealed class ActiveEndpoint
{
    public string ListenAddress { get; set; } = Core.Constants.AppConstants.DefaultListenAddress;

    public int Port { get; set; } = Core.Constants.AppConstants.DefaultPort;
}
