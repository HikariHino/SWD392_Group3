namespace Application.DTOs.QuestionBank;

public class RubricDto
{
    public Guid Id { get; set; }
    public string Criteria { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double MaxScore { get; set; }
}

public class CreateRubricRequest
{
    public string Criteria { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double MaxScore { get; set; }
}
