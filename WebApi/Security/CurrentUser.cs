using Application.Interfaces.Services;

namespace WebApi.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id => Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
    public bool IsLecturer => accessor.HttpContext?.User.IsInRole("Lecturer") == true;
    public bool IsStudent => accessor.HttpContext?.User.IsInRole("Student") == true;
}
