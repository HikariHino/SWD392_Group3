using System;

namespace Application.DTOs.UserManagement;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
}

public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string? Role { get; set; }
}
