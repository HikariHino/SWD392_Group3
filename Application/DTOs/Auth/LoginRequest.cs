namespace Application.DTOs.Auth;

public sealed record LoginRequest(string Username, string Password);
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserManagement.UserDto User);
