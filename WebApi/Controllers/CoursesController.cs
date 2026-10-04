using Application.DTOs.QuestionBank;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Controllers;

[ApiController]
[Authorize(Roles = "Lecturer,Student")]
[Route("api/courses")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _service;
    public CoursesController(ICourseService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourseDto>>> GetCourses() => Ok(await _service.GetCoursesAsync());

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<CourseDto>> GetCourseById(Guid id) => Ok(await _service.GetCourseByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(CourseDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<CourseDto>> CreateCourse(CreateCourseRequest request)
    {
        var course = await _service.CreateCourseAsync(request);
        return CreatedAtAction(nameof(GetCourseById), new { id = course.Id }, course);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<CourseDto>> UpdateCourse(Guid id, UpdateCourseRequest request) => Ok(await _service.UpdateCourseAsync(id, request));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Lecturer")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<IActionResult> DeleteCourse(Guid id)
    {
        await _service.DeleteCourseAsync(id);
        return NoContent();
    }
}
