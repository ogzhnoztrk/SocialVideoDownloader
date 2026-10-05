using System.Net;
using System.Net.Sockets;

namespace SocialVideoDownloader.Core.Validation;

public static class UrlGuard
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        if (trimmed.Length is < 12 or > 2000)
            return false;

        foreach (var character in trimmed)
        {
            if (char.IsControl(character) || character is '"' or '`' or '|')
                return false;
        }

        if (!trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not "http" and not "https")
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo) || uri.IsFile || uri.IsUnc)
            return false;

        var host = uri.IdnHost;
        if (string.IsNullOrWhiteSpace(host) || !host.Contains('.', StringComparison.Ordinal))
            return false;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IsBlockedAddress(host))
            return false;

        normalized = uri.AbsoluteUri;
        return true;
    }

    private static bool IsBlockedAddress(string host)
    {
        if (!IPAddress.TryParse(host.Trim('[', ']'), out var address))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 0;
        }

        var ipv6 = address.GetAddressBytes();
        return (ipv6[0] & 0xFE) == 0xFC || (ipv6[0] == 0xFE && (ipv6[1] & 0xC0) == 0x80);
    }
}
