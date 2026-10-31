using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Application.Common;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using WebApi.Importing;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer,Student")]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionBankService _questionBankService;
    private readonly IValidator<CreateQuestionRequest> _createValidator;
    private readonly IValidator<UpdateQuestionRequest> _updateValidator;

    public QuestionsController(
        IQuestionBankService questionBankService,
        IValidator<CreateQuestionRequest> createValidator,
        IValidator<UpdateQuestionRequest> updateValidator)
    {
        _questionBankService = questionBankService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Lấy danh sách câu hỏi có phân trang, tìm kiếm và lọc theo Bloom Level / Môn học
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(PagedResponse<QuestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQuestions([FromQuery] QuestionQueryParameters query)
    {
        var result = await _questionBankService.GetQuestionsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một câu hỏi kèm toàn bộ tiêu chí Rubric
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestionById(Guid id)
    {
        var question = await _questionBankService.GetQuestionByIdAsync(id);
        if (question == null)
        {
            return NotFound(new { message = $"Không tìm thấy câu hỏi với mã ID: {id}" });
        }
        return Ok(question);
    }

    /// <summary>
    /// Tạo mới một câu hỏi kèm danh sách tiêu chí Rubric (Tổng trọng số Rubric phải = 100%)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = validationResult.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage })
            });
        }

        var created = await _questionBankService.CreateQuestionAsync(request);
        return CreatedAtAction(nameof(GetQuestionById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Cập nhật câu hỏi và tiêu chí Rubric
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuestion(Guid id, [FromBody] UpdateQuestionRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                message = "Dữ liệu không hợp lệ.",
                errors = validationResult.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage })
            });
        }

        var updated = await _questionBankService.UpdateQuestionAsync(id, request);
        if (updated == null)
        {
            return NotFound(new { message = $"Không tìm thấy câu hỏi với mã ID: {id} để cập nhật." });
        }

        return Ok(updated);
    }

    /// <summary>
    /// Xóa câu hỏi (Soft Delete) khỏi ngân hàng đề
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuestion(Guid id)
    {
        var success = await _questionBankService.DeleteQuestionAsync(id);
        if (!success)
        {
            return NotFound(new { message = $"Không tìm thấy câu hỏi với mã ID: {id} để xóa." });
        }

        return NoContent();
    }

    /// <summary>
    /// Lấy danh sách môn học để hiển thị lên Dropdown chọn lọc trên giao diện
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourses()
    {
        var courses = await _questionBankService.GetCoursesAsync();
        return Ok(courses);
    }

    /// <summary>
    /// Dùng AI Gemini sinh câu hỏi vấn đáp theo chủ đề; rubric được cấu hình ở cấp Course.
    /// </summary>
    [HttpPost("ai-generate")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(AiGeneratedQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateQuestionWithAi([FromBody] AiGenerateQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return BadRequest(new { message = "Vui lòng nhập chủ đề câu hỏi cần AI sinh." });
        }

        var result = await _questionBankService.GenerateQuestionWithAiAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Import CSV UTF-8 theo dạng toàn bộ hoặc không import.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportQuestions(IFormFile? file, [FromForm] Guid courseId, CancellationToken cancellationToken)
    {
        const long maxFileBytes = 5 * 1024 * 1024;
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Choose a non-empty CSV file.", errors = new[] { new { line = 0, field = "file", message = "File is required." } } });
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .csv files are supported.", errors = new[] { new { line = 0, field = "file", message = "Upload a .csv file." } } });
        if (file.Length > maxFileBytes)
            return BadRequest(new { message = "CSV file exceeds the 5 MB limit.", errors = new[] { new { line = 0, field = "file", message = "Maximum file size is 5 MB." } } });
        if (courseId == Guid.Empty)
            return BadRequest(new { message = "A courseId is required.", errors = new[] { new { line = 0, field = "courseId", message = "Choose a valid course." } } });

        string csv;
        try
        {
            await using var stream = new MemoryStream((int)file.Length);
            await file.CopyToAsync(stream, cancellationToken);
            csv = new UTF8Encoding(false, true).GetString(stream.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return BadRequest(new { message = "CSV must be valid UTF-8.", errors = new[] { new { line = 0, field = "file", message = "File contains invalid UTF-8 bytes." } } });
        }

        var parsed = QuestionCsvParser.Parse(csv, courseId);
        var errors = parsed.Errors.ToList();
        foreach (var row in parsed.Rows)
        {
            var validation = await _createValidator.ValidateAsync(row.Request, cancellationToken);
            errors.AddRange(validation.Errors.Select(error => new QuestionCsvError(row.Line, error.PropertyName, error.ErrorMessage)));
        }
        if (errors.Count > 0)
            return BadRequest(new { message = "No questions were imported. Fix the listed CSV rows and retry.", errors });

        var count = await _questionBankService.ImportQuestionsBatchAsync(new ImportQuestionsRequest
        {
            CourseId = courseId,
            Questions = parsed.Rows.Select(row => row.Request).ToList()
        });
        return StatusCode(StatusCodes.Status201Created, new
        {
            importedCount = count,
            policy = "all-or-nothing",
            message = $"Imported {count} questions."
        });
    }
}
