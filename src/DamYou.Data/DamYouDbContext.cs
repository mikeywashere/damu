using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data;

public sealed class DamYouDbContext : DbContext
{
    public DamYouDbContext(DbContextOptions<DamYouDbContext> options) : base(options) { }

    public DbSet<WatchedFolder> WatchedFolders => Set<WatchedFolder>();
    public DbSet<Photo> Photos => Set<Photo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchedFolder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Path).IsUnique();
            e.Property(x => x.Path).IsRequired();
            e.Property(x => x.DateAdded).IsRequired();
        });

        modelBuilder.Entity<Photo>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.FilePath).IsUnique();
            e.HasIndex(x => x.WatchedFolderId).HasDatabaseName("IX_Photos_WatchedFolderId");
            e.HasIndex(x => x.DateTaken).HasDatabaseName("IX_Photos_DateTaken");
            e.HasIndex(x => x.FileHash).HasDatabaseName("IX_Photos_FileHash");
            e.Property(x => x.FileName).IsRequired();
            e.Property(x => x.FilePath).IsRequired();
            e.HasOne(x => x.WatchedFolder)
             .WithMany()
             .HasForeignKey(x => x.WatchedFolderId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
