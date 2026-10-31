using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/attempts/{attemptId:guid}/follow-up")]
public sealed class InterviewController(IInterviewService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<FollowUpTurnDto>> Generate(Guid attemptId, CancellationToken cancellationToken) =>
        Created($"/api/attempts/{attemptId}/turns", await service.GenerateFollowUpAsync(attemptId, cancellationToken));
}
