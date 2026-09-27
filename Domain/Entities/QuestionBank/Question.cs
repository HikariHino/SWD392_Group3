using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.QuestionBank;

public class Question : AuditableEntity
{
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;

    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    public ICollection<Rubric> Rubrics { get; set; } = new List<Rubric>();
}
