using Domain.Common;

namespace Domain.Entities.QuestionBank;

public class Course : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
