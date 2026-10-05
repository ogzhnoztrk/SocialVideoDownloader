using Microsoft.EntityFrameworkCore;
using SocialVideoDownloader.Core.Constants;
using SocialVideoDownloader.Core.Entities;

namespace SocialVideoDownloader.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DownloadJob> DownloadJobs => Set<DownloadJob>();

    public DbSet<ApplicationSettings> ApplicationSettings => Set<ApplicationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DownloadJob>(entity =>
        {
            entity.HasKey(job => job.Id);
            entity.Property(job => job.Url).HasMaxLength(2000).IsRequired();
            entity.Property(job => job.Title).HasMaxLength(500).IsRequired();
            entity.Property(job => job.Uploader).HasMaxLength(300);
            entity.Property(job => job.VideoId).HasMaxLength(128);
            entity.Property(job => job.FileName).HasMaxLength(255);
            entity.Property(job => job.OutputDirectory).HasMaxLength(1000);
            entity.Property(job => job.FilePath).HasMaxLength(1000);
            entity.Property(job => job.ThumbnailUrl).HasMaxLength(2000);
            entity.Property(job => job.Resolution).HasMaxLength(32);
            entity.Property(job => job.Format).HasMaxLength(16);
            entity.Property(job => job.ErrorMessage).HasMaxLength(500);
            entity.Property(job => job.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(job => job.Platform).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(job => job.Status);
            entity.HasIndex(job => job.CreatedAt);
            entity.HasIndex(job => new { job.VideoId, job.Status });
        });

        modelBuilder.Entity<ApplicationSettings>(entity =>
        {
            entity.HasKey(settings => settings.Id);
            entity.Property(settings => settings.DownloadDirectory).HasMaxLength(1000).IsRequired();
            entity.Property(settings => settings.ListenAddress).HasMaxLength(45).IsRequired().HasDefaultValue(AppConstants.DefaultListenAddress);
            entity.Property(settings => settings.FileNameTemplate).HasMaxLength(180).IsRequired();
            entity.Property(settings => settings.YtDlpPath).HasMaxLength(1000);
            entity.Property(settings => settings.FfmpegPath).HasMaxLength(1000);
        });
    }
}
