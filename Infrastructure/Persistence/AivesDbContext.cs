using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Domain.Common;

namespace Infrastructure.Persistence;

public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options)
    {
    }

    // Khai bÃ¡o cÃ¡c DbSet á»Ÿ Ä‘Ã¢y sau nÃ y, vÃ­ dá»¥:
    // public DbSet<Question> Questions => Set<Question>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tá»± Ä‘á»™ng quÃ©t vÃ  apply cÃ¡c cáº¥u hÃ¬nh (Fluent API) trong thÆ° má»¥c Persistence/Configurations
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Tá»± Ä‘á»™ng cáº­p nháº­t CreatedAt vÃ  UpdatedAt má»—i khi SaveChanges Ä‘Æ°á»£c gá»i
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

