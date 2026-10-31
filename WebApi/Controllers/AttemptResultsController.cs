using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer,Student")]
public sealed class AttemptResultsController(IGradingService grading) : ControllerBase
{
    [HttpGet("api/attempts/{attemptId:guid}/result")]
    [ProducesResponseType(typeof(AttemptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttemptResultDto>> Get(Guid attemptId, CancellationToken cancellationToken) =>
        Ok(await grading.GetAttemptResultAsync(attemptId, cancellationToken));
}
