namespace Application.DTOs.Interviews;

public record RubricSnapshotCriterion(Guid RubricId, string Criteria, decimal Weight, decimal MaxScore, string? AiGradingPrompt);
public record RubricEarnedScore(Guid RubricId, decimal EarnedScore);
public class AiAssessmentRequest
{
    public string QuestionContent { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public string StudentAnswer { get; set; } = string.Empty;
    public IReadOnlyList<RubricSnapshotCriterion> Rubric { get; set; } = [];
}
public class AiAssessmentResponse
{
    public IReadOnlyList<RubricEarnedScore> RubricScores { get; set; } = [];
    public string Feedback { get; set; } = string.Empty;
    public decimal? ConfidenceLevel { get; set; }
}

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

public sealed class FinalizeAssessmentRequest
{
    public decimal FinalScore { get; set; }
    public string? TeacherComment { get; set; }
    public int? Version { get; set; }
}

public sealed class AttemptResultDto
{
    public Guid AttemptId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? AiSuggestedTotal { get; set; }
    public decimal? TotalScore { get; set; }
    public DateTime? GradedAt { get; set; }
    public bool IsComplete { get; set; }
    public int PendingQuestionCount { get; set; }
    public IReadOnlyList<AssessmentDto> Assessments { get; set; } = [];
}
