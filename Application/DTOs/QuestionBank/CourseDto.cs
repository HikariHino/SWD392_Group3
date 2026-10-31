namespace Application.DTOs.QuestionBank;

public class CourseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<RubricDto> Rubrics { get; set; } = new();
}

public abstract class CourseRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateCourseRequest : CourseRequest { }
public class UpdateCourseRequest : CourseRequest { }

public class ReplaceCourseRubricsRequest
{
    public List<CreateRubricRequest> Rubrics { get; set; } = new();
}
