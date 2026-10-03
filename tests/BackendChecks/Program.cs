using System.Text.Json;
using Application.DTOs.UserManagement;
using Application.Exceptions;
using Application.Validators.UserManagement;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Middleware;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

var hasher = new PasswordHasher();
var firstHash = hasher.HashPassword("Sample password 123");
Check(firstHash != "Sample password 123", "password is not stored verbatim");
Check(hasher.VerifyPassword("Sample password 123", firstHash), "correct password verifies");
Check(!hasher.VerifyPassword("wrong password", firstHash), "wrong password rejected");
Check(firstHash != hasher.HashPassword("Sample password 123"), "unique salt per hash");

var create = new CreateUserDtoValidator();
CreateUserDto ValidUser() => new() { Username = "student", Password = "password123", FullName = "Sinh viên", Role = "Student" };
Check(create.Validate(ValidUser()).IsValid, "valid student input");
foreach (var role in new[] { "Admin", "student", "", "Other" })
{
    var input = ValidUser(); input.Role = role;
    Check(!create.Validate(input).IsValid, "unsupported role rejected: " + role);
}
var longName = ValidUser(); longName.Username = new string('a', 51);
Check(!create.Validate(longName).IsValid, "username longer than database column rejected");
longName = ValidUser(); longName.FullName = new string('a', 101);
Check(!create.Validate(longName).IsValid, "full name longer than database column rejected");
var longPassword = ValidUser(); longPassword.Password = new string('é', 37);
Check(!create.Validate(longPassword).IsValid, "multibyte password exceeds BCrypt byte limit");
var update = new UpdateUserDtoValidator();
Check(update.Validate(new UpdateUserDto { FullName = "New name" }).IsValid, "omitted role allowed for profile update");
Check(!update.Validate(new UpdateUserDto { Role = "Admin" }).IsValid, "admin role update rejected");
Check(!update.Validate(new UpdateUserDto { Role = "" }).IsValid, "empty supplied role rejected");
Check(!update.Validate(new UpdateUserDto { FullName = new string('a', 101) }).IsValid, "long update name rejected");

foreach (var (error, status) in new (Exception, int)[]
{
    (new NotFoundException("Missing user"), 404),
    (new ConflictException("Duplicate username"), 409),
    (new ValidationException(new Dictionary<string, string[]> { ["Username"] = ["Invalid"] }), 400),
    (new InvalidOperationException("SENSITIVE_INTERNAL_DETAIL"), 500)
})
{
    var context = new DefaultHttpContext { TraceIdentifier = "check-trace" };
    context.Response.Body = new MemoryStream();
    var middleware = new ExceptionMiddleware(_ => Task.FromException(error), NullLogger<ExceptionMiddleware>.Instance);
    await middleware.InvokeAsync(context);
    context.Response.Body.Position = 0;
    var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
    using var json = JsonDocument.Parse(body);
    Check(context.Response.StatusCode == status, "exception status " + status);
    Check(context.Response.ContentType == "application/problem+json", "problem content type " + status);
    Check(json.RootElement.GetProperty("traceId").GetString() == "check-trace", "traceId included " + status);
    if (status == 400) Check(json.RootElement.GetProperty("errors").GetProperty("Username").GetArrayLength() == 1, "validation field errors preserved");
    if (status == 500) Check(!body.Contains("SENSITIVE_INTERNAL_DETAIL"), "internal failure detail hidden");
}

// No SQL connection: inspect migration discovery, generated SQL and snapshot consistency.
using var db = new AivesDbContext(new DbContextOptionsBuilder<AivesDbContext>()
    .UseSqlServer("Server=localhost;Database=OfflineChecks;Integrated Security=True;TrustServerCertificate=True").Options);
var migrations = db.Database.GetMigrations().ToArray();
Check(migrations.Contains("20260927162533_InitialCreate") && migrations.Contains("20261003125521_AddUserTable"), "both migrations discoverable after relocation");
Check(!db.Database.HasPendingModelChanges(), "migration snapshot matches EF model");
var script = db.GetService<IMigrator>().GenerateScript("20260927162533_InitialCreate", "20261003125521_AddUserTable");
Check(script.Contains("CREATE TABLE [Users]") && script.Contains("IX_Users_Username"), "user migration creates table and unique username index");
Check(!script.Contains("DROP TABLE") && !script.Contains("CREATE TABLE [Courses]"), "user migration preserves existing question-bank tables");
await QuestionBankChecks.Run(Check);
Console.WriteLine($"{checks} checks passed. Live SQL migration/CRUD not verified.");
