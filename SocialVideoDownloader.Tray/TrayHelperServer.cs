using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SocialVideoDownloader.Tray;

internal sealed class TrayHelperServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<string?> _browse;
    private readonly Action _restart;
    private readonly Action<string, bool> _openPath;

    public TrayHelperServer(Func<string?> browse, Action restart, Action<string, bool> openPath)
    {
        _browse = browse;
        _restart = restart;
        _openPath = openPath;
        _listener = new TcpListener(IPAddress.Loopback, Core.Constants.AppConstants.TrayHelperPort);
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => Handle(client));
        }
    }

    private void Handle(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            stream.ReadTimeout = 5000;
            var requestBytes = ReadRequest(stream);
            var headerEnd = Find(requestBytes, requestBytes.Length, "\r\n\r\n"u8);
            var headLength = headerEnd < 0 ? requestBytes.Length : headerEnd;
            var request = Encoding.ASCII.GetString(requestBytes, 0, headLength);
            var body = headerEnd < 0 ? "" : Encoding.UTF8.GetString(requestBytes, headerEnd + 4, requestBytes.Length - headerEnd - 4);
            var lines = request.Split("\r\n");
            var requestLine = lines.FirstOrDefault() ?? string.Empty;
            var origin = lines.FirstOrDefault(line => line.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))?[7..].Trim();
            if (!IsAllowedOrigin(origin))
            {
                Write(stream, 403, origin, """{"message":"rejected"}""");
                return;
            }

            var parts = requestLine.Split(' ');
            var method = parts.ElementAtOrDefault(0) ?? string.Empty;
            var path = parts.ElementAtOrDefault(1) ?? string.Empty;
            if (method == "OPTIONS")
            {
                Write(stream, 204, origin, string.Empty);
                return;
            }

            if (method == "POST" && path.StartsWith("/browse-folder", StringComparison.Ordinal))
            {
                var selected = _browse();
                Write(stream, 200, origin, JsonSerializer.Serialize(new { path = selected }));
                return;
            }

            if (method == "POST" && path.StartsWith("/open-path", StringComparison.Ordinal))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        Write(stream, 400, origin, """{"message":"Klasör isteği boş geldi."}""");
                        return;
                    }

                    var payload = JsonSerializer.Deserialize<OpenPathRequest>(body, JsonOptions);
                    if (string.IsNullOrWhiteSpace(payload?.Path))
                    {
                        Write(stream, 400, origin, """{"message":"Klasör bulunamadı."}""");
                        return;
                    }

                    _openPath(payload.Path, payload.Select);
                    Write(stream, 200, origin, """{"ok":true}""");
                }
                catch (Exception exception)
                {
                    var message = JsonSerializer.Serialize(new { message = exception.Message });
                    Write(stream, 400, origin, message);
                }

                return;
            }

            if (method == "POST" && path.StartsWith("/restart-service", StringComparison.Ordinal))
            {
                try
                {
                    _restart();
                    Write(stream, 200, origin, """{"ok":true,"message":"Yeniden başlatma istendi."}""");
                }
                catch (Exception)
                {
                    Write(stream, 200, origin, """{"ok":false,"message":"Servis yeniden başlatılamadı."}""");
                }
                return;
            }

            Write(stream, 404, origin, """{"message":"not found"}""");
        }
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != "http")
            return false;

        if (uri.Host is "localhost" or "127.0.0.1" or "::1")
            return true;

        return IPAddress.TryParse(uri.Host, out var address) && IsLocalAddress(address);
    }

    private static bool IsLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                continue;

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.Equals(address))
                    return true;
            }
        }

        return false;
    }

    private static byte[] ReadRequest(NetworkStream stream)
    {
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        var headerEnd = -1;
        while (headerEnd < 0 && data.Length < 65536)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            data.Write(buffer, 0, read);
            headerEnd = Find(data.GetBuffer(), (int)data.Length, "\r\n\r\n"u8);
        }

        if (headerEnd < 0)
            return data.ToArray();

        var header = Encoding.ASCII.GetString(data.GetBuffer(), 0, headerEnd);
        var contentLength = 0;
        foreach (var line in header.Split("\r\n"))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line.AsSpan("Content-Length:".Length).Trim(), out var length))
                contentLength = length;
        }

        var bodyStart = headerEnd + 4;
        while (data.Length - bodyStart < contentLength && data.Length < 65536)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            data.Write(buffer, 0, read);
        }

        return data.ToArray();
    }

    private static int Find(byte[] buffer, int length, ReadOnlySpan<byte> pattern)
    {
        var limit = length - pattern.Length;
        for (var index = 0; index <= limit; index++)
        {
            if (buffer.AsSpan(index, pattern.Length).SequenceEqual(pattern))
                return index;
        }

        return -1;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class OpenPathRequest
    {
        public string? Path { get; set; }

        public bool Select { get; set; }
    }

    private static void Write(NetworkStream stream, int status, string? origin, string body)
    {
        var reason = status switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            403 => "Forbidden",
            _ => "Not Found",
        };
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = new StringBuilder()
            .Append("HTTP/1.1 ").Append(status).Append(' ').Append(reason).Append("\r\n")
            .Append("Content-Type: application/json; charset=utf-8\r\n")
            .Append("Content-Length: ").Append(bytes.Length).Append("\r\n")
            .Append("Access-Control-Allow-Origin: ").Append(origin).Append("\r\n")
            .Append("Access-Control-Allow-Methods: POST, OPTIONS\r\n")
            .Append("Access-Control-Allow-Headers: Content-Type\r\n")
            .Append("Vary: Origin\r\n")
            .Append("Connection: close\r\n\r\n");
        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        stream.Write(headerBytes);
        if (status != 204)
            stream.Write(bytes);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
    }
}
