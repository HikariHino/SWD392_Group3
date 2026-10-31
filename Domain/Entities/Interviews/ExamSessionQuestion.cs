using Domain.Common;
using Domain.Entities.QuestionBank;

namespace Domain.Entities.Interviews;

/// <summary>
/// Bản ghi câu hỏi được chốt trong ca thi (snapshot tại thời điểm mở ca).
/// Unique: (SessionId, QuestionId).
/// </summary>
public class ExamSessionQuestion : BaseEntity
{
    // FK – RestrictDelete: không xóa câu hỏi khi ca thi đã có bản ghi.
    public Guid SessionId { get; set; }
    public ExamSession? Session { get; set; }

    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>Snapshot điểm tại thời điểm mở ca, độc lập với Question.Points.</summary>
    public decimal Points { get; set; }

    /// <summary>Thứ tự hiển thị câu hỏi trong ca thi.</summary>
    public int DisplayOrder { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public int BloomLevel { get; set; }
    public string? AiModel { get; set; }
    /// <summary>Serialized rubric snapshot: rubricId, criteria, weight, maxScore, aiGradingPrompt. Written when opening the session.</summary>
    public string RubricSnapshotJson { get; set; } = "[]";

    // Navigation
    public ICollection<TranscriptTurn> TranscriptTurns { get; set; } = new List<TranscriptTurn>();
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}
