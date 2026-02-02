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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Value).HasMaxLength(4096);
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }
}

