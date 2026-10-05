using Microsoft.Win32;

namespace SocialVideoDownloader.Tray;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SocialVideoDownloader";

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
            return;

        if (enabled)
        {
            var starter = FindStarter() ?? exePath;
            key.SetValue(ValueName, $"\"{starter}\"");
        }
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string? FindStarter()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            var starter = Path.Combine(dir.FullName, "Baslat.cmd");
            if (File.Exists(starter))
                return starter;
        }

        return null;
    }
}
