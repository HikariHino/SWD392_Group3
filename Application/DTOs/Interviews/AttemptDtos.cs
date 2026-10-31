namespace Application.DTOs.Interviews;
public record AttemptDto(Guid Id, Guid SessionId, Guid UserId, string Status, DateTime StartedAt, DateTime? SubmittedAt, int Version);
public record TurnDto(Guid Id, Guid AttemptId, int Sequence, string Speaker, string ContentText, DateTime Timestamp, Guid? QuestionId, Guid? ClientMessageId);
public record AttemptQuestionDto(Guid QuestionId, int DisplayOrder, string Content, decimal Points);
public class AnswerRequest
{
    public Guid QuestionId { get; set; }
    public string AnswerText { get; set; } = "";
    public Guid ClientMessageId { get; set; }
}
