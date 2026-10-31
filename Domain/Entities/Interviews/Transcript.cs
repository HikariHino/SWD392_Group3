using Domain.Common;
using Domain.Enums;
using Domain.Entities.UserManagement;

namespace Domain.Entities.Interviews;

/// <summary>
/// Bài thi của một sinh viên trong một ca thi.
/// Unique: (SessionId, UserId) – MVP cho phép mỗi sinh viên một bài/ca.
/// </summary>
public class Transcript : AuditableEntity
{
    public TranscriptStatus Status { get; set; } = TranscriptStatus.InProgress;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }

    /// <summary>Tổng điểm chính thức sau khi Finalized. Null khi chưa chốt.</summary>
    public decimal? TotalScore { get; set; }

    // Concurrency token
    public int Version { get; set; }

    // FK
    public Guid SessionId { get; set; }
    public ExamSession? Session { get; set; }

    /// <summary>Sinh viên làm bài (lấy từ principal, không phải client).</summary>
    public Guid UserId { get; set; }
    public User? Student { get; set; }

    // Navigation
    public ICollection<TranscriptTurn> Turns { get; set; } = new List<TranscriptTurn>();
    public ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}
