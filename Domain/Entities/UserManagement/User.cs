using Domain.Common;
using Domain.Entities.Interviews;

namespace Domain.Entities.UserManagement;

public class User : BaseEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation – ExamSession tạo bởi Lecturer
    public ICollection<ExamSession> CreatedExamSessions { get; set; } = new List<ExamSession>();
    // Navigation – Transcript của sinh viên
    public ICollection<Transcript> Transcripts { get; set; } = new List<Transcript>();
}
