using Domain.Common;

namespace Domain.Entities.QuestionBank;

public class Rubric : AuditableEntity
{
    public string Criteria { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double MaxScore { get; set; }

    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
}
