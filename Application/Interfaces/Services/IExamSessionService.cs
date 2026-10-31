using Application.DTOs.Interviews;
namespace Application.Interfaces.Services;
public interface IExamSessionService
{
    Task<SessionDto> Create(CreateSessionRequest request);
    Task<SessionDto> Get(Guid id);
    Task<SessionDto> Transition(Guid id, string action);
}
