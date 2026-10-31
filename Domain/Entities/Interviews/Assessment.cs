using Domain.Common;
using Domain.Entities.UserManagement;

namespace Domain.Entities.Interviews;

/// <summary>One immutable-question assessment within a student transcript.</summary>
public class Assessment : AuditableEntity
{
    public string StudentAnswer { get; set; } = string.Empty;
    public decimal Points { get; set; }
    public string RubricSnapshotJson { get; set; } = "[]";

    public decimal? AiSuggestedScore { get; set; }
    public string? AiFeedback { get; set; }
    public decimal? AiConfidenceLevel { get; set; }
    public DateTime? EvaluatedAt { get; set; }

    public decimal? FinalScore { get; set; }
    public string? TeacherComment { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public User? FinalizedByUser { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public int Version { get; set; }

    public Guid TranscriptId { get; set; }
    public Transcript? Transcript { get; set; }
    public Guid ExamSessionQuestionId { get; set; }
    public ExamSessionQuestion? ExamSessionQuestion { get; set; }
}
