using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace WebApi.Controllers;
[ApiController]
[Authorize(Roles = "Lecturer")]
[Route("api/exam-sessions")]
public class ExamSessionsController(IExamSessionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> List() => Ok(await service.ListMine());
    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create(CreateSessionRequest request)
    {
        var result = await service.Create(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> Get(Guid id) => Ok(await service.Get(id));
    [HttpPost("{id:guid}/open")]
    public async Task<ActionResult<SessionDto>> Open(Guid id) => Ok(await service.Transition(id, "open"));
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<SessionDto>> Close(Guid id) => Ok(await service.Transition(id, "close"));
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SessionDto>> Cancel(Guid id) => Ok(await service.Transition(id, "cancel"));
}
