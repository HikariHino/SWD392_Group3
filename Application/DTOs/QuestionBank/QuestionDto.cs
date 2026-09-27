using Domain.Enums;

namespace Application.DTOs.QuestionBank;

public class QuestionDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; }
    public string BloomLevelName => BloomLevel.ToString();

    public Guid CourseId { get; set; }
    public string? CourseCode { get; set; }
    public string? CourseName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<RubricDto> Rubrics { get; set; } = new List<RubricDto>();
}

public class CreateQuestionRequest
{
    public Guid CourseId { get; set; }
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; } = BloomLevel.Understand;
    public List<CreateRubricRequest> Rubrics { get; set; } = new List<CreateRubricRequest>();
}

public class UpdateQuestionRequest
{
    public string Content { get; set; } = string.Empty;
    public BloomLevel BloomLevel { get; set; }
    public List<CreateRubricRequest> Rubrics { get; set; } = new List<CreateRubricRequest>();
}

public class QuestionQueryParameters
{
    public Guid? CourseId { get; set; }
    public BloomLevel? BloomLevel { get; set; }
    public string? SearchTerm { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
