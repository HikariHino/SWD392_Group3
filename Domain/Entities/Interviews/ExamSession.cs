using Domain.Common;
using Domain.Enums;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;

namespace Domain.Entities.Interviews;

/// <summary>
/// Ca thi do Lecturer tạo. Gắn với một Course, chứa bộ câu hỏi cố định và
/// nhiều bài thi (Transcript) của sinh viên.
/// </summary>
public class ExamSession : AuditableEntity
{
    public string SessionTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ExamSessionStatus Status { get; set; } = ExamSessionStatus.Draft;

    // Concurrency token – tăng mỗi khi trạng thái thay đổi (C14).
    public int Version { get; set; }

    // FK
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }

    // Người tạo (Lecturer)
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    // Navigation
    public ICollection<ExamSessionQuestion> ExamSessionQuestions { get; set; } = new List<ExamSessionQuestion>();
    public ICollection<Transcript> Transcripts { get; set; } = new List<Transcript>();
}
