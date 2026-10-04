namespace Application.Interfaces.Services;

public interface ICurrentUser
{
    Guid? Id { get; }
    bool IsLecturer { get; }
    bool IsStudent { get; }
}
