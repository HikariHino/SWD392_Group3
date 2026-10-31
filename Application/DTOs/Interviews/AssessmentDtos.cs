namespace Application.DTOs.Interviews;

public record RubricSnapshotCriterion(Guid RubricId, string Criteria, decimal Weight, decimal MaxScore, string? AiGradingPrompt);
public record RubricEarnedScore(Guid RubricId, decimal EarnedScore);

public class AssessmentDto
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public decimal? AiSuggestedScore { get; set; }
    public string? AiFeedback { get; set; }
    public decimal? AiConfidenceLevel { get; set; }
    public decimal? FinalScore { get; set; }
    public string? TeacherComment { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public int Version { get; set; }
}
