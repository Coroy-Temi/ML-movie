using Microsoft.EntityFrameworkCore;
using ML_MOVIE.Entities;

namespace ML_MOVIE.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RecommendationSession> RecommendationSessions => Set<RecommendationSession>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<HomepageUpdate> HomepageUpdates => Set<HomepageUpdate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(180);
            entity.Property(user => user.DisplayName).HasMaxLength(80);
            entity.Property(user => user.ThemePreference).HasMaxLength(20);
            entity.Property(user => user.VerificationCode).HasMaxLength(12);
            entity.Property(user => user.PendingEmail).HasMaxLength(180);
        });

        modelBuilder.Entity<RecommendationSession>(entity =>
        {
            entity.Property(session => session.DetectedEmotion).HasMaxLength(40);
            entity.Property(session => session.EmotionSource).HasMaxLength(40);
            entity.Property(session => session.GenreFilters).HasMaxLength(240);
            entity.Property(session => session.RecommendedTitles).HasMaxLength(600);
            entity.Property(session => session.CaptureDataUrl).HasMaxLength(4000);

            entity.HasOne(session => session.User)
                .WithMany(user => user.RecommendationSessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.Property(message => message.Subject).HasMaxLength(140);
            entity.Property(message => message.Message).HasMaxLength(2400);

            entity.HasOne(message => message.User)
                .WithMany()
                .HasForeignKey(message => message.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HomepageUpdate>(entity =>
        {
            entity.Property(update => update.Title).HasMaxLength(120);
            entity.Property(update => update.Body).HasMaxLength(400);
            entity.Property(update => update.Category).HasMaxLength(30);
        });
    }
}
