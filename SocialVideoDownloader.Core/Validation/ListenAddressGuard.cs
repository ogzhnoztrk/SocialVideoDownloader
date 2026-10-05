using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Exceptions;

namespace SocialVideoDownloader.Core.Validation;

public static class ListenAddressGuard
{
    public static string Normalize(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? AppConstants.DefaultListenAddress : value.Trim();
        if (text.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return AppConstants.DefaultListenAddress;

        if (!IPAddress.TryParse(text, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new DownloadException("IP adresi geçersiz. Örnek: 127.0.0.1");

        if (IPAddress.IsLoopback(address))
            return AppConstants.DefaultListenAddress;

        if (!IsAssignedToThisComputer(address))
            throw new DownloadException("Bu IP adresi bu bilgisayara ait değil.");

        return address.ToString();
    }

    private static bool IsAssignedToThisComputer(IPAddress address)
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
                continue;

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork && unicast.Address.Equals(address))
                    return true;
            }
        }

        return false;
    }
}
