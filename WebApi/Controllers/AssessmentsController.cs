using Application.DTOs.Interviews;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer")]
public sealed class AssessmentsController(IGradingService service) : ControllerBase
{
    [HttpGet("api/attempts/{attemptId:guid}/assessments")]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> GetAttemptAssessments(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetAttemptAssessmentsAsync(attemptId, cancellationToken));

    [HttpPut("api/assessments/{assessmentId:guid}/final-score")]
    public async Task<ActionResult<AssessmentDto>> FinalizeScore(
        Guid assessmentId,
        [FromBody] FinalizeAssessmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.FinalizeAssessmentAsync(assessmentId, request, cancellationToken));
}
