using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace WebApi.Controllers;
[ApiController]
[Authorize(Roles = "Lecturer,Student")]
[Route("api/attempts")]
public class AttemptsController(IAttemptService service) : ControllerBase
{
    [HttpPost("/api/exam-sessions/{id:guid}/attempts")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<AttemptDto>> Start(Guid id)
    {
        var (attempt, created) = await service.Start(id);
        return created ? CreatedAtAction(nameof(Get), new { id = attempt.Id }, attempt) : Ok(attempt);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AttemptDto>> Get(Guid id) => Ok(await service.Get(id));
    [HttpGet("{id:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<AttemptQuestionDto>>> Questions(Guid id) => Ok(await service.Questions(id));
    [HttpGet("{id:guid}/turns")]
    public async Task<ActionResult<IReadOnlyList<TurnDto>>> Turns(Guid id) => Ok(await service.Turns(id));
    [HttpPost("{id:guid}/answers")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<TurnDto>> Answer(Guid id, AnswerRequest request)
    {
        var (turn, created) = await service.Answer(id, request);
        return created ? CreatedAtAction(nameof(Turns), new { id }, turn) : Ok(turn);
    }
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<AttemptDto>> Submit(Guid id) => Ok(await service.Submit(id));
}
