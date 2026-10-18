using Application.DTOs.QuestionBank;

namespace Application.Interfaces.Services;

public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetCoursesAsync();
    Task<CourseDto> GetCourseByIdAsync(Guid id);
    Task<CourseDto> CreateCourseAsync(CreateCourseRequest request);
    Task<CourseDto> UpdateCourseAsync(Guid id, UpdateCourseRequest request);
    Task DeleteCourseAsync(Guid id);
}
