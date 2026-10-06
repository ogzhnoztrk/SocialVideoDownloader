using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.DTOs;
using SocialVideoDownloader.Core.Entities;

namespace SocialVideoDownloader.Core.Gist;

public static class GistStatusMapper
{
    public static GistStatusDto From(ApplicationSettings settings)
    {
        var minutes = settings.GistPollIntervalMinutes < 1
            ? AppConstants.DefaultGistPollMinutes
            : settings.GistPollIntervalMinutes;
        DateTimeOffset? last = settings.GistLastCheckedUtc is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(settings.GistLastCheckedUtc.Value, DateTimeKind.Utc));
        var enabled = settings.GistPollingEnabled;
        var status = !enabled
            ? "Kapalı"
            : settings.GistLastCheckedUtc is null || settings.GistLastSucceeded
                ? "Aktif"
                : "Hata";

        return new GistStatusDto
        {
            Enabled = enabled,
            Status = status,
            LastCheckedAt = last,
            NextCheckAt = enabled && last is not null ? last.Value.AddMinutes(minutes) : null,
            Message = settings.GistLastMessage,
            IntervalMinutes = minutes,
        };
    }
}
