namespace SocialVideoDownloader.Core.Constants;

public static class UserMessages
{
    public const string VideoNotFound = "Video bulunamadı.";
    public const string InvalidUrl = "URL geçersiz.";
    public const string UnsupportedPlatform = "Platform desteklenmiyor.";
    public const string PrivateVideo = "Video private.";
    public const string LoginRequired = "Video giriş gerektiriyor.";
    public const string VideoGone = "Video artık mevcut değil.";
    public const string YtDlpError = "yt-dlp hatası.";
    public const string YtDlpMissing = "yt-dlp bulunamadı. Ayarlar > Downloader bölümünden kurulum yapabilirsiniz.";
    public const string FfmpegMissing = "FFmpeg bulunamadı.";
    public const string DiskFull = "Disk alanı yetersiz.";
    public const string Cancelled = "İndirme iptal edildi.";
    public const string Duplicate = "Bu video daha önce indirilmiş.";
    public const string InvalidPath = "Dosya yolu geçersiz.";
    public const string InvalidDownloadFolder = "İndirme klasörü geçerli değil.";
    public const string BlockedFolder = "Bu klasör indirme için kullanılamaz.";
    public const string InvalidFileTemplate = "Dosya adlandırma şablonu geçersiz.";
    public const string Unexpected = "Beklenmeyen bir hata oluştu.";
}
