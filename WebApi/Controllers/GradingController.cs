using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer")]
[Route("api/attempts/{attemptId:guid}/grade")]
public sealed class GradingController(IGradingService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> Grade(Guid attemptId, CancellationToken cancellationToken) =>
        Accepted(await service.GradeAttemptAsync(attemptId, cancellationToken));
}
