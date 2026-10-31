using Domain.Entities.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ExamSessionQuestionConfiguration : IEntityTypeConfiguration<ExamSessionQuestion>
{
    public void Configure(EntityTypeBuilder<ExamSessionQuestion> b)
    {
        b.ToTable("ExamSessionQuestions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).IsRequired().HasMaxLength(2000);
        b.Property(x => x.ExpectedAnswer).HasMaxLength(4000);
        b.Property(x => x.AiModel).HasMaxLength(100);
        b.Property(x => x.RubricSnapshotJson).IsRequired();
        b.Property(x => x.Points).HasPrecision(18, 4);
        b.HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique();
        b.HasIndex(x => new { x.SessionId, x.DisplayOrder }).IsUnique();
        b.HasOne(x => x.Session).WithMany(x => x.ExamSessionQuestions).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Question).WithMany(x => x.ExamSessionQuestions).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TranscriptConfiguration : IEntityTypeConfiguration<Transcript>
{
    public void Configure(EntityTypeBuilder<Transcript> b)
    {
        b.ToTable("Transcripts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("TranscriptId");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.TotalScore).HasPrecision(18, 4);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
        b.HasOne(x => x.Session).WithMany(x => x.Transcripts).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany(x => x.Transcripts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TranscriptTurnConfiguration : IEntityTypeConfiguration<TranscriptTurn>
{
    public void Configure(EntityTypeBuilder<TranscriptTurn> b)
    {
        b.ToTable("TranscriptTurns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("TurnId");
        b.Property(x => x.Speaker).IsRequired().HasMaxLength(20);
        b.Property(x => x.ContentText).IsRequired();
        b.HasIndex(x => new { x.TranscriptId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.TranscriptId, x.ClientMessageId }).IsUnique().HasFilter("[ClientMessageId] IS NOT NULL");
        b.HasOne(x => x.Transcript).WithMany(x => x.Turns).HasForeignKey(x => x.TranscriptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ExamSessionQuestion).WithMany(x => x.TranscriptTurns).HasForeignKey(x => x.ExamSessionQuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> b)
    {
        b.ToTable("Assessments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("AssessmentId");
        b.Property(x => x.StudentAnswer).IsRequired();
        b.Property(x => x.Points).HasPrecision(18, 4);
        b.Property(x => x.RubricSnapshotJson).IsRequired();
        b.Property(x => x.AiSuggestedScore).HasPrecision(18, 4);
        b.Property(x => x.AiConfidenceLevel).HasPrecision(5, 4);
        b.Property(x => x.FinalScore).HasPrecision(18, 4);
        b.Property(x => x.TeacherComment).HasMaxLength(2000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.TranscriptId, x.ExamSessionQuestionId }).IsUnique();
        b.HasOne(x => x.Transcript).WithMany(x => x.Assessments).HasForeignKey(x => x.TranscriptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ExamSessionQuestion).WithMany(x => x.Assessments).HasForeignKey(x => x.ExamSessionQuestionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FinalizedByUser).WithMany(x => x.FinalizedAssessments).HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
