using Domain.Common;

namespace Domain.Entities.QuestionBank;

public class Rubric : AuditableEntity
{
    public string Criteria { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double MaxScore { get; set; }
    public string? AiGradingPrompt { get; set; }

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }
}
