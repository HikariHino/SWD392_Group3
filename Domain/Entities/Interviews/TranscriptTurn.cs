using Domain.Common;

namespace Domain.Entities.Interviews;

/// <summary>
/// Một lượt hội thoại trong bài thi (câu hỏi AI hoặc câu trả lời sinh viên).
/// Unique: (TranscriptId, Sequence).
/// </summary>
public class TranscriptTurn : BaseEntity
{
    /// <summary>Số thứ tự tăng dần trong bài thi.</summary>
    public int Sequence { get; set; }

    /// <summary>"AI" hoặc "Student".</summary>
    public string Speaker { get; set; } = string.Empty;

    public string ContentText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>clientMessageId do client gửi để retry idempotent.</summary>
    public Guid? ClientMessageId { get; set; }

    // FK
    public Guid TranscriptId { get; set; }
    public Transcript? Transcript { get; set; }

    /// <summary>Câu hỏi liên quan (nullable: lượt AI follow-up có thể không gắn câu hỏi cụ thể).</summary>
    public Guid? ExamSessionQuestionId { get; set; }
    public ExamSessionQuestion? ExamSessionQuestion { get; set; }
}
