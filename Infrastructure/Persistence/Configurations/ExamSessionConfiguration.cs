using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.Entities.Interviews;
using Domain.Enums;

namespace Infrastructure.Persistence.Configurations;

public class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.ToTable("ExamSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("SessionId");

        builder.Property(s => s.SessionTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.StartTime).IsRequired();
        builder.Property(s => s.EndTime).IsRequired();

        // Status lưu dạng string trong DB (C03 contract)
        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ExamSessionStatus>(v));

        // Concurrency token
        builder.Property(s => s.Version).IsConcurrencyToken();

        // FK – Course
        builder.HasOne(s => s.Course)
            .WithMany(c => c.ExamSessions)
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK – Lecturer (người tạo)
        builder.HasOne(s => s.CreatedByUser)
            .WithMany(u => u.CreatedExamSessions)
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete – C03: không cascade xóa lịch sử
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
