using SocialVideoDownloader.Core.Constants;

namespace SocialVideoDownloader.Core.Gist;

public static class GistSchedule
{
    public static TimeSpan Interval(int minutes)
    {
        var value = minutes < 1 ? AppConstants.DefaultGistPollMinutes : minutes;
        return TimeSpan.FromMinutes(Math.Clamp(value, 15, 24 * 60));
    }
}
