using System.Net;
using System.Text.Json;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Infrastructure.Configuration;

namespace SocialVideoDownloader.Web.Middleware;

public sealed class LocalOnlyMiddleware(RequestDelegate next, ActiveEndpoint endpoint)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        if (remote is null || !IsAllowedRemote(remote) || !IsAllowedHost(context.Request.Host.Host))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var method = context.Request.Method;
        var changesState = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);
        if (changesState &&
            context.Request.Path.StartsWithSegments("/api") &&
            context.Request.Headers[AppConstants.ApiHeaderName] != AppConstants.ApiHeaderValue)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "İstek reddedildi." }));
            return;
        }

        await next(context);
    }

    private bool IsAllowedRemote(IPAddress remote)
    {
        if (IPAddress.IsLoopback(remote))
            return true;

        return IPAddress.TryParse(endpoint.ListenAddress, out var listen) && remote.Equals(listen);
    }

    private bool IsAllowedHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("[::1]", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
        host.Equals(endpoint.ListenAddress, StringComparison.OrdinalIgnoreCase);
}
