using Domain.Enums;

namespace Application.DTOs.QuestionBank;

public class AiGenerateQuestionRequest
{
    public Guid CourseId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Analyze;
}

public class AiGeneratedQuestionDto
{
    public Guid CourseId { get; set; }
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Analyze;
    public string BloomLevelName => BloomLevel.ToString();
    public string SourceContext { get; set; } = string.Empty;
    public double AiConfidence { get; set; } = 96.0;
    public List<CreateRubricRequest> Rubrics { get; set; } = new();
}

public class ImportQuestionsRequest
{
    public Guid CourseId { get; set; }
    public List<CreateQuestionRequest> Questions { get; set; } = new();
}
