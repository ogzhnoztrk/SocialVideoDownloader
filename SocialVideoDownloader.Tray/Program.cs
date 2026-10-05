using SocialVideoDownloader.Core.Constants;

namespace SocialVideoDownloader.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "SocialVideoDownloader.Tray", out var created);
        if (!created)
        {
            MessageBox.Show("Tepsi uygulaması zaten çalışıyor.", "Social Video Downloader");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
