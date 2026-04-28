using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data;

public sealed class DamYouDbContext : DbContext
{
    public DamYouDbContext(DbContextOptions<DamYouDbContext> options) : base(options) { }

    public DbSet<WatchedFolder> WatchedFolders => Set<WatchedFolder>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PipelineTask> PipelineTasks => Set<PipelineTask>();
    public DbSet<PhotoEmbedding> PhotoEmbeddings => Set<PhotoEmbedding>();
    public DbSet<PhotoDetectedObject> PhotoDetectedObjects => Set<PhotoDetectedObject>();
    public DbSet<PhotoOcrText> PhotoOcrTexts => Set<PhotoOcrText>();
    public DbSet<PhotoColorPalette> PhotoColorPalettes => Set<PhotoColorPalette>();

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

        modelBuilder.Entity<PipelineTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status).HasDatabaseName("IX_PipelineTasks_Status");
            e.HasIndex(x => x.PhotoId).HasDatabaseName("IX_PipelineTasks_PhotoId");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_PipelineTasks_CreatedAt");
            e.HasOne(x => x.Photo)
             .WithMany()
             .HasForeignKey(x => x.PhotoId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        modelBuilder.Entity<PhotoEmbedding>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PhotoId).HasDatabaseName("IX_PhotoEmbeddings_PhotoId");
            e.HasIndex(x => new { x.PhotoId, x.ModelName }).HasDatabaseName("IX_PhotoEmbeddings_PhotoId_ModelName");
            e.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Embedding).IsRequired();
        });

        modelBuilder.Entity<PhotoDetectedObject>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PhotoId).HasDatabaseName("IX_PhotoDetectedObjects_PhotoId");
            e.HasIndex(x => x.Label).HasDatabaseName("IX_PhotoDetectedObjects_Label");
            e.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhotoOcrText>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PhotoId).IsUnique().HasDatabaseName("IX_PhotoOcrText_PhotoId");
            e.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhotoColorPalette>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PhotoId).IsUnique().HasDatabaseName("IX_PhotoColorPalette_PhotoId");
            e.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
