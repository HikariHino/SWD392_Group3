using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Application.DTOs.Auth;
using Application.DTOs.QuestionBank;
using Application.DTOs.UserManagement;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.Validators.Auth;
using AutoMapper;
using Domain.Entities.UserManagement;
using FluentValidation;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Security;
using WebApi.Swagger;

internal static class AuthorizationChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var settings = new JwtSettings
        {
            Issuer = "authorization-checks", Audience = "checks-client",
            SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        };
        var hasher = new PasswordHasher();
        User Account(string username, string role) => new()
        {
            Username = username, FullName = username, Role = role,
            PasswordHash = hasher.HashPassword("password123"), CreatedAt = DateTime.UtcNow
        };
        var lecturer = Account("lecturer", "Lecturer");
        var student = Account("student", "Student");
        var otherStudent = Account("other", "Student");
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddDbContext<AivesDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddSingleton<IMapper>(mapper);
        builder.Services.AddSingleton<IPasswordHasher>(hasher);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IAccessTokenIssuer, JwtTokenIssuer>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ICourseService, CourseService>();
        builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
        builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        builder.Services.AddControllers().AddApplicationPart(typeof(WebApi.Controllers.UsersController).Assembly);
        builder.Services.AddAivesAuthentication(settings);
        builder.Services.AddAivesSwagger();
        await using var app = builder.Build();
        app.UseMiddleware<WebApi.Middleware.ExceptionMiddleware>();
        app.UseSwagger();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(lecturer, student, otherStudent);
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            async Task<string> Login(string username)
            {
                using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, "password123"));
                check(response.StatusCode == HttpStatusCode.OK, "SQLite HTTP login: " + username);
                return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
            }
            var lecturerToken = await Login("lecturer");
            var studentToken = await Login("student");
            void Token(string? value) => client.DefaultRequestHeaders.Authorization = value == null ? null : new("Bearer", value);
            async Task Status(HttpMethod method, string path, object? body, HttpStatusCode expected, string name)
            {
                using var request = new HttpRequestMessage(method, path);
                if (body != null) request.Content = JsonContent.Create(body);
                using var response = await client.SendAsync(request);
                check(response.StatusCode == expected, name + $" ({(int)response.StatusCode})");
                if (expected is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    check(response.Content.Headers.ContentType?.MediaType == "application/problem+json" &&
                        problem.RootElement.GetProperty("status").GetInt32() == (int)expected &&
                        problem.RootElement.TryGetProperty("traceId", out _), "auth error has ProblemDetails/traceId: " + name);
                }
            }

            var id = otherStudent.Id;
            var protectedRoutes = new (HttpMethod Method, string Path)[]
            {
                (HttpMethod.Get, "/api/Users"), (HttpMethod.Get, $"/api/Users/{id}"),
                (HttpMethod.Post, "/api/Users"), (HttpMethod.Put, $"/api/Users/{id}"), (HttpMethod.Delete, $"/api/Users/{id}"),
                (HttpMethod.Get, "/api/courses"), (HttpMethod.Get, $"/api/courses/{id}"),
                (HttpMethod.Post, "/api/courses"), (HttpMethod.Put, $"/api/courses/{id}"), (HttpMethod.Delete, $"/api/courses/{id}"),
                (HttpMethod.Get, "/api/questions"), (HttpMethod.Get, $"/api/questions/{id}"),
                (HttpMethod.Post, "/api/questions"), (HttpMethod.Put, $"/api/questions/{id}"), (HttpMethod.Delete, $"/api/questions/{id}"),
                (HttpMethod.Get, "/api/questions/courses"), (HttpMethod.Post, "/api/questions/import")
            };
            Token(null);
            foreach (var route in protectedRoutes)
                await Status(route.Method, route.Path, null, HttpStatusCode.Unauthorized, "anonymous blocked: " + route.Method + " " + route.Path);
            Token(studentToken);
            foreach (var route in protectedRoutes.Where(r => !((r.Path.StartsWith("/api/courses") || r.Path == "/api/questions/courses") && r.Method == HttpMethod.Get)))
                await Status(route.Method, route.Path, new { fullName = "Attack", role = "Lecturer" }, HttpStatusCode.Forbidden, "student denied: " + route.Method + " " + route.Path);
            await Status(HttpMethod.Get, "/api/courses", null, HttpStatusCode.OK, "student can read courses");
            await Status(HttpMethod.Get, "/api/questions/courses", null, HttpStatusCode.OK, "student can read legacy course dropdown");
            await Status(HttpMethod.Get, $"/api/Users/{student.Id}", null, HttpStatusCode.OK, "student can read own profile");
            await Status(HttpMethod.Put, $"/api/Users/{student.Id}", new { fullName = "Updated student" }, HttpStatusCode.NoContent, "student can update own full name");
            await Status(HttpMethod.Put, $"/api/Users/{student.Id}", new { fullName = "Attack", role = "Lecturer" }, HttpStatusCode.Forbidden, "student cannot elevate own role");
            await Status(HttpMethod.Put, $"/api/Users/{student.Id}", new { role = "Student" }, HttpStatusCode.Forbidden, "student cannot submit any role update");

            Token(lecturerToken);
            await Status(HttpMethod.Get, "/api/Users", null, HttpStatusCode.OK, "lecturer lists users");
            await Status(HttpMethod.Put, $"/api/Users/{lecturer.Id}", new { role = "Student" }, HttpStatusCode.Forbidden, "lecturer cannot change own role");
            using var createdUser = await client.PostAsJsonAsync("/api/Users", new CreateUserDto
            {
                Username = "new-student", Password = "password123", FullName = "New student", Role = "Student"
            });
            check(createdUser.StatusCode == HttpStatusCode.Created, "lecturer creates user");
            var userDto = (await createdUser.Content.ReadFromJsonAsync<UserDto>())!;
            await Status(HttpMethod.Get, $"/api/Users/{userDto.Id}", null, HttpStatusCode.OK, "lecturer reads user");
            await Status(HttpMethod.Put, $"/api/Users/{userDto.Id}", new { fullName = "Renamed" }, HttpStatusCode.NoContent, "lecturer updates user");
            await Status(HttpMethod.Delete, $"/api/Users/{userDto.Id}", null, HttpStatusCode.NoContent, "lecturer soft deletes user");
            await Status(HttpMethod.Get, $"/api/Users/{userDto.Id}", null, HttpStatusCode.NotFound, "deleted user is hidden");

            using var createdCourse = await client.PostAsJsonAsync("/api/courses", new { code = "SWD392", name = "Design" });
            check(createdCourse.StatusCode == HttpStatusCode.Created, "lecturer creates course via HTTP");
            var course = (await createdCourse.Content.ReadFromJsonAsync<CourseDto>())!;
            var questionBody = new
            {
                courseId = course.Id, content = "Explain Onion", bloomLevel = 2,
                rubrics = new[] { new { criteria = "Correct explanation", weight = 100, maxScore = 10 } }
            };
            using var createdQuestion = await client.PostAsJsonAsync("/api/questions", questionBody);
            check(createdQuestion.StatusCode == HttpStatusCode.Created, "lecturer creates question with rubric via HTTP");
            var question = (await createdQuestion.Content.ReadFromJsonAsync<QuestionDto>())!;
            await Status(HttpMethod.Get, "/api/questions", null, HttpStatusCode.OK, "lecturer lists questions");
            await Status(HttpMethod.Get, $"/api/questions/{question.Id}", null, HttpStatusCode.OK, "lecturer reads question with rubric");
            await Status(HttpMethod.Put, $"/api/questions/{question.Id}", questionBody, HttpStatusCode.OK, "lecturer updates question/rubric");
            await Status(HttpMethod.Delete, $"/api/questions/{question.Id}", null, HttpStatusCode.NoContent, "lecturer deletes question");
            await Status(HttpMethod.Put, $"/api/courses/{course.Id}", new { code = "SWD392", name = "Updated course" }, HttpStatusCode.OK, "lecturer updates course");
            Token(studentToken);
            await Status(HttpMethod.Get, $"/api/courses/{course.Id}", null, HttpStatusCode.OK, "student can read course detail");
            Token(lecturerToken);
            await Status(HttpMethod.Delete, $"/api/courses/{course.Id}", null, HttpStatusCode.NoContent, "lecturer deletes course after question deletion");

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
                var saved = await db.Users.SingleAsync(u => u.Id == student.Id);
                check(saved.Role == "Student" && saved.FullName == "Updated student", "denied profile mutations leave stored role/name unchanged");
                check(await db.Users.AnyAsync(u => u.Id == userDto.Id && u.IsDeleted), "HTTP user deletion retains database row");
                check(hasher.VerifyPassword("password123", (await db.Users.SingleAsync(u => u.Id == userDto.Id)).PasswordHash), "HTTP created user stores valid BCrypt hash");
            }
            await Status(HttpMethod.Put, $"/api/Users/{student.Id}", new { role = "Lecturer" }, HttpStatusCode.NoContent, "lecturer can change another user role");
            Token(studentToken);
            await Status(HttpMethod.Get, "/api/auth/me", null, HttpStatusCode.Unauthorized, "old token invalidated after role change");
            var freshToken = await Login("student");
            Token(freshToken);
            await Status(HttpMethod.Get, "/api/Users", null, HttpStatusCode.OK, "new token has current lecturer permission");
            Token(lecturerToken);
            await Status(HttpMethod.Delete, $"/api/Users/{student.Id}", null, HttpStatusCode.NoContent, "lecturer deletes account with issued token");
            Token(freshToken);
            await Status(HttpMethod.Get, "/api/auth/me", null, HttpStatusCode.Unauthorized, "deleted account token is rejected");
            var missingAccount = new JwtTokenIssuer(settings, TimeProvider.System).Issue(Account("missing", "Lecturer"));
            Token(missingAccount.Value);
            await Status(HttpMethod.Get, "/api/Users", null, HttpStatusCode.Unauthorized, "signed token for missing account is rejected");
            var invalidSubject = Account("invalid", "Lecturer"); invalidSubject.Id = Guid.Empty;
            Token(new JwtTokenIssuer(settings, TimeProvider.System).Issue(invalidSubject).Value);
            await Status(HttpMethod.Get, "/api/Users", null, HttpStatusCode.Unauthorized, "signed token with empty subject rejected");

            Token(null);
            using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
            check(swagger.StatusCode == HttpStatusCode.OK, "Swagger JSON remains public for local demo");
            using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");
            foreach (var path in paths.EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (path.Name == "/api/auth/login")
                    check(!operation.Value.TryGetProperty("security", out var security) || security.GetArrayLength() == 0, "Swagger login stays anonymous");
                else
                    check(operation.Value.GetProperty("security")[0].TryGetProperty("Bearer", out _), "Swagger sends Bearer: " + operation.Name + " " + path.Name);
            }
        }
        finally { await app.StopAsync(); }
    }
}
