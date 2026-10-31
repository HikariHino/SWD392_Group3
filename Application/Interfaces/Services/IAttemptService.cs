using Application.DTOs.Interviews;
namespace Application.Interfaces.Services;
public interface IAttemptService
{
    Task<(AttemptDto Attempt, bool Created)> Start(Guid sessionId);
    Task<AttemptDto> Get(Guid id);
    Task<IReadOnlyList<AttemptQuestionDto>> Questions(Guid id);
    Task<IReadOnlyList<TurnDto>> Turns(Guid id);
    Task<(TurnDto Turn, bool Created)> Answer(Guid id, AnswerRequest request);
    Task<AttemptDto> Submit(Guid id);
}
