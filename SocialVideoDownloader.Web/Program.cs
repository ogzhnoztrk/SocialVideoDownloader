using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Infrastructure;
using SocialVideoDownloader.Infrastructure.Configuration;
using SocialVideoDownloader.Infrastructure.Data;
using SocialVideoDownloader.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);
var port = HostSettingsStore.ReadPort(builder.Configuration.GetValue("Downloader:WebPort", AppConstants.DefaultPort));
Directory.CreateDirectory(DataPaths.LogsDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Map(
        _ => DateTime.Now.ToString("yyyy-MM-dd"),
        (date, writeTo) => writeTo.File(
            Path.Combine(DataPaths.LogsDirectory, $"app-{date}.log"),
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            shared: true),
        sinkMapCountLimit: 2)
    .CreateLogger();

try
{
    builder.Host.UseSerilog();
    builder.Services.AddWindowsService(options => options.ServiceName = AppConstants.ServiceName);
    builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(port));
    builder.Services.AddSocialVideoDownloader(builder.Configuration);
    builder.Services.AddControllersWithViews().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

    var app = builder.Build();
    app.Services.GetRequiredService<ActiveEndpoint>().Port = port;

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await DbInitializer.InitializeAsync(factory, CancellationToken.None);
    }

    app.UseExceptionHandler(handler =>
    {
        handler.Run(async context =>
        {
            var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SocialVideoDownloader");
            logger.LogError(exception, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                await context.Response.WriteAsJsonAsync(new { message = UserMessages.Unexpected });
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<!DOCTYPE html><html lang=\"tr\"><body><h1>Beklenmeyen bir hata oluştu.</h1></body></html>");
        });
    });

    app.UseMiddleware<LocalOnlyMiddleware>();
    app.UseStaticFiles();
    app.UseRouting();
    app.MapControllers();
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Social Video Downloader stopped");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
