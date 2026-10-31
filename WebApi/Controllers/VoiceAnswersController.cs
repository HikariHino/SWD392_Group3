using Application.Interfaces.ExternalServices;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Student")]
[Route("api/attempts/{attemptId:guid}/voice-answers")]
public sealed class VoiceAnswersController(IVoiceInterviewService service) : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes + 64 * 1024)]
    public async Task<IActionResult> Answer(Guid attemptId, [FromForm] Guid questionId, [FromForm] Guid clientMessageId, IFormFile audio, CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length is <= 0 or > MaxUploadBytes)
            return BadRequest(new { error = "Upload an audio file up to 10 MiB." });
        await using var stream = new MemoryStream((int)audio.Length);
        await audio.CopyToAsync(stream, cancellationToken);
        var clip = new SpeechAudioClip(stream.ToArray(), Path.GetFileName(audio.FileName), audio.ContentType);
        return Ok(await service.AnswerAsync(attemptId, questionId, clientMessageId, clip, cancellationToken));
    }
}
