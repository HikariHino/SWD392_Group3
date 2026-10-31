namespace Application.DTOs.Interviews;

public record AiConversationTurn(string Speaker, string ContentText);

public class AiFollowUpRequest
{
    public string CourseName { get; set; } = string.Empty;
    public string QuestionContent { get; set; } = string.Empty;
    public string RubricSnapshotJson { get; set; } = "[]";
    public IReadOnlyList<AiConversationTurn> Conversation { get; set; } = [];
}

public class AiFollowUpDto
{
    public string Content { get; set; } = string.Empty;
    public string SourceContext { get; set; } = string.Empty;
}

public record FollowUpTurnDto(Guid Id, Guid AttemptId, int Sequence, string ContentText, DateTime Timestamp, Guid QuestionId);
