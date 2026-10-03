using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Domain.Common;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;

namespace Infrastructure.Persistence;

public class AivesDbContext : DbContext
{
    public AivesDbContext(DbContextOptions<AivesDbContext> options) : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();

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
