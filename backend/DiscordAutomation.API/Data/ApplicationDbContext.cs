using Microsoft.EntityFrameworkCore;
using DiscordAutomation.API.Models;

namespace DiscordAutomation.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Guild> Guilds { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Rule> Rules { get; set; }
        public DbSet<LogEntry> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Guild configuration
            modelBuilder.Entity<Guild>(entity =>
            {
                entity.HasIndex(g => g.Name);
                entity.HasIndex(g => g.IsActive);
                entity.HasIndex(g => g.PremiumTier);
                entity.HasIndex(g => g.CreatedAt);
                
                entity.Property(g => g.Id).ValueGeneratedNever();
                
                entity.HasMany(g => g.Modules)
                    .WithOne(m => m.Guild)
                    .HasForeignKey(m => m.GuildId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasMany(g => g.Rules)
                    .WithOne(r => r.Guild)
                    .HasForeignKey(r => r.GuildId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasMany(g => g.Logs)
                    .WithOne(l => l.Guild)
                    .HasForeignKey(l => l.GuildId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Module configuration
            modelBuilder.Entity<Module>(entity =>
            {
                entity.HasIndex(m => new { m.GuildId, m.Type });
                entity.HasIndex(m => m.IsEnabled);
                
                entity.Property(m => m.Configuration)
                    .HasColumnType("jsonb");
            });

            // Rule configuration
            modelBuilder.Entity<Rule>(entity =>
            {
                entity.HasIndex(r => new { r.GuildId, r.ModuleId });
                entity.HasIndex(r => r.IsEnabled);
                entity.HasIndex(r => r.Priority);
                entity.HasIndex(r => r.CreatedAt);
                
                entity.Property(r => r.TriggerConditions)
                    .HasColumnType("jsonb");
                    
                entity.Property(r => r.ActionParameters)
                    .HasColumnType("jsonb");
            });

            // LogEntry configuration
            modelBuilder.Entity<LogEntry>(entity =>
            {
                entity.HasIndex(l => new { l.GuildId, l.CreatedAt });
                entity.HasIndex(l => l.Severity);
                entity.HasIndex(l => l.Source);
                
                entity.Property(l => l.Details)
                    .HasColumnType("jsonb");
                    
                entity.HasOne(l => l.Guild)
                    .WithMany(g => g.Logs)
                    .HasForeignKey(l => l.GuildId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity && 
                    (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    ((BaseEntity)entry.Entity).CreatedAt = DateTime.UtcNow;
                }
                ((BaseEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // Base entity for timestamps
    public abstract class BaseEntity
    {
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}