using Domain.Common;

namespace Domain.Entities.UserManagement;

public class User : BaseEntity
{
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
