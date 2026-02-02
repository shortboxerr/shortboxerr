using Microsoft.EntityFrameworkCore;
using Shortboxerr.Core.Entities;

namespace Shortboxerr.Infrastructure.Persistence;

public class ShortboxerrDbContext : DbContext
{
    public ShortboxerrDbContext(DbContextOptions<ShortboxerrDbContext> options)
        : base(options)
    {
    }

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<EditionTitle> EditionTitles => Set<EditionTitle>();
    public DbSet<EditionContent> EditionContents => Set<EditionContent>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Value).HasMaxLength(4096);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // Series
        modelBuilder.Entity<Series>(entity =>
        {
            entity.ToTable("Series");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.SortTitle).HasMaxLength(512);
            entity.Property(e => e.Publisher).HasMaxLength(256);
            entity.Property(e => e.Path).HasMaxLength(1024);
            entity.Property(e => e.ExternalId).HasMaxLength(128);
            entity.Property(e => e.ExternalSource).HasMaxLength(64);
            entity.Property(e => e.Overview).HasMaxLength(4096);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => new { e.ExternalSource, e.ExternalId });
        });

        // Issue
        modelBuilder.Entity<Issue>(entity =>
        {
            entity.ToTable("Issues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssueNumber).HasPrecision(10, 2);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.ExternalId).HasMaxLength(128);
            entity.Property(e => e.ExternalSource).HasMaxLength(64);
            entity.Property(e => e.Overview).HasMaxLength(4096);
            entity.HasIndex(e => new { e.SeriesId, e.IssueNumber });
            entity.HasOne(e => e.Series)
                .WithMany(s => s.Issues)
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.File)
                .WithOne(f => f.Issue)
                .HasForeignKey<FileAsset>(f => f.IssueId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // EditionTitle
        modelBuilder.Entity<EditionTitle>(entity =>
        {
            entity.ToTable("EditionTitles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(512);
            entity.Property(e => e.SortTitle).HasMaxLength(512);
            entity.Property(e => e.Isbn).HasMaxLength(32);
            entity.Property(e => e.Publisher).HasMaxLength(256);
            entity.Property(e => e.ExternalId).HasMaxLength(128);
            entity.Property(e => e.ExternalSource).HasMaxLength(64);
            entity.Property(e => e.Overview).HasMaxLength(4096);
            entity.HasIndex(e => e.Title);
            entity.HasIndex(e => e.Isbn);
            entity.HasOne(e => e.Series)
                .WithMany(s => s.Editions)
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.File)
                .WithOne(f => f.EditionTitle)
                .HasForeignKey<FileAsset>(f => f.EditionTitleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // EditionContent
        modelBuilder.Entity<EditionContent>(entity =>
        {
            entity.ToTable("EditionContents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssueNumber).HasPrecision(10, 2);
            entity.HasIndex(e => new { e.EditionTitleId, e.SortOrder });
            entity.HasOne(e => e.EditionTitle)
                .WithMany(et => et.Contents)
                .HasForeignKey(e => e.EditionTitleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Issue)
                .WithMany()
                .HasForeignKey(e => e.IssueId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Series)
                .WithMany()
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // FileAsset
        modelBuilder.Entity<FileAsset>(entity =>
        {
            entity.ToTable("FileAssets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.RelativePath).HasMaxLength(1024);
            entity.Property(e => e.Hash).HasMaxLength(128);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(16);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.Hash);
        });

        // HistoryEvent
        modelBuilder.Entity<HistoryEvent>(entity =>
        {
            entity.ToTable("HistoryEvents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Data).HasMaxLength(8192);
            entity.Property(e => e.SourcePath).HasMaxLength(1024);
            entity.Property(e => e.DestinationPath).HasMaxLength(1024);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2048);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasOne(e => e.Series)
                .WithMany()
                .HasForeignKey(e => e.SeriesId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Issue)
                .WithMany()
                .HasForeignKey(e => e.IssueId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.EditionTitle)
                .WithMany()
                .HasForeignKey(e => e.EditionTitleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
