using Domain.Enums;
namespace Application.DTOs.Interviews;
public class CreateSessionRequest
{
    public Guid CourseId { get; set; }
    public string SessionTitle { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Guid> QuestionIds { get; set; } = [];
}
public record SessionDto(Guid Id, Guid CourseId, string SessionTitle, DateTime StartTime, DateTime EndTime, string Status, int Version, Guid CreatedByUserId, IReadOnlyList<Guid> QuestionIds);
