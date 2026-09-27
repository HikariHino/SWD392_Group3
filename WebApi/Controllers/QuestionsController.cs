using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Application.Common;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/question")] // Alias route for backwards compatibility
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
    [ProducesResponseType(typeof(PagedResponse<QuestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuestions([FromQuery] QuestionQueryParameters query)
    {
        var result = await _questionBankService.GetQuestionsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một câu hỏi kèm toàn bộ tiêu chí Rubric
    /// </summary>
    [HttpGet("{id:guid}")]
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
    /// Import câu hỏi từ file
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportQuestions()
    {
        var result = await _questionBankService.ImportQuestionsAsync("dummyPath");
        return Ok(new { success = result, message = "Import câu hỏi thành công!" });
    }
}
