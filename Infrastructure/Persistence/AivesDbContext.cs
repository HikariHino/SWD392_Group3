using System.Reflection;
using Domain.Entities.Interviews;
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

    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<ExamSessionQuestion> ExamSessionQuestions => Set<ExamSessionQuestion>();
    public DbSet<Transcript> Transcripts => Set<Transcript>();
    public DbSet<TranscriptTurn> TranscriptTurns => Set<TranscriptTurn>();
    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
