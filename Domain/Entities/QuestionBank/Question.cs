using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.QuestionBank;

public class Question : AuditableEntity
{
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;

    // Legacy question bank columns retained by the existing Azure SQL schema.
    public string? ExpectedAnswer { get; set; }
    public string QuestionType { get; set; } = "Essay";
    public decimal Points { get; set; } = 10m;
    public string? AiModel { get; set; } = "gpt-4";

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
}
