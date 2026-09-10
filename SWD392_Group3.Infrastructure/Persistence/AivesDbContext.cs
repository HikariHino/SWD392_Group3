using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SWD392_Group3.Domain.Common;

namespace SWD392_Group3.Infrastructure.Persistence;

public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options)
    {
    }

    // Khai báo các DbSet ở đây sau này, ví dụ:
    // public DbSet<Question> Questions => Set<Question>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Tự động quét và apply các cấu hình (Fluent API) trong thư mục Persistence/Configurations
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Tự động cập nhật CreatedAt và UpdatedAt mỗi khi SaveChanges được gọi
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
