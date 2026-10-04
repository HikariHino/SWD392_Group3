using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces.Repositories;
using Application.Services;
using Application.Validators.Auth;
using Domain.Entities.QuestionBank;
using Domain.Entities.UserManagement;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WebApi.Security;

internal static class AuthChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var settings = new JwtSettings
        {
            Issuer = "checks", Audience = "checks-client",
            SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        };
        settings.Validate();
        var hasher = new PasswordHasher();
        using var uow = new AuthUnitOfWork();
        var user = new User
        {
            Id = Guid.NewGuid(), Username = "student", FullName = "Student",
            Role = "Student", PasswordHash = hasher.HashPassword("password123")
        };
        uow.Repository.User = user;
        var service = new AuthService(uow, hasher, new JwtTokenIssuer(settings, TimeProvider.System), new LoginRequestValidator());
        var response = await service.LoginAsync(new(" student ", "password123"));
        check(response.User.Id == user.Id && response.User.Role == "Student", "login returns stored user identity");
        check(response.ExpiresAt > DateTimeOffset.UtcNow && response.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(30), "login token expires in configured interval");
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(response.AccessToken, settings.ValidationParameters(), out _);
        check(principal.Identity!.Name == user.Username && principal.IsInRole("Student") && principal.FindFirst("sub")!.Value == user.Id.ToString(), "signed JWT preserves subject/name/role");

        async Task Invalid(string username, string password, string name)
        {
            try { await service.LoginAsync(new(username, password)); check(false, name); }
            catch (InvalidCredentialsException ex) { check(ex.Message == "Invalid username or password.", name); }
        }
        await Invalid("student", "wrong", "wrong password returns generic credentials failure");
        await Invalid("missing", "password123", "unknown user returns same credentials failure");
        user.IsDeleted = true;
        await Invalid("student", "password123", "deleted account cannot log in");
        user.IsDeleted = false;
        user.Role = "Admin";
        await Invalid("student", "password123", "unsupported stored role cannot issue token");
        user.Role = "Student";
        user.PasswordHash = "legacy plaintext";
        await Invalid("student", "legacy plaintext", "legacy plaintext hash cannot log in");
        try { await service.LoginAsync(new("student", new string('é', 37))); check(false, "BCrypt byte bound"); }
        catch (Application.Exceptions.ValidationException ex) { check(ex.Errors.ContainsKey("Password"), "login enforces BCrypt UTF-8 byte bound"); }

        void Reject(string token, TokenValidationParameters parameters, string name)
        {
            try { handler.ValidateToken(token, parameters, out _); check(false, name); }
            catch (SecurityTokenException) { check(true, name); }
        }
        var invalidAudience = settings.ValidationParameters(); invalidAudience.ValidAudience = "other";
        Reject(response.AccessToken, invalidAudience, "wrong JWT audience rejected");
        var invalidIssuer = settings.ValidationParameters(); invalidIssuer.ValidIssuer = "other";
        Reject(response.AccessToken, invalidIssuer, "wrong JWT issuer rejected");
        var invalidKey = settings.ValidationParameters();
        invalidKey.IssuerSigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(48));
        Reject(response.AccessToken, invalidKey, "wrong JWT signature rejected");
        var expired = new JwtTokenIssuer(settings, new FixedClock(DateTimeOffset.UtcNow.AddHours(-2))).Issue(user);
        Reject(expired.Value, settings.ValidationParameters(), "expired JWT rejected");
        var future = new JwtTokenIssuer(settings, new FixedClock(DateTimeOffset.UtcNow.AddHours(1))).Issue(user);
        Reject(future.Value, settings.ValidationParameters(), "not-yet-valid JWT rejected");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false; o.TokenValidationParameters = settings.ValidationParameters();
        });
        using var provider = services.BuildServiceProvider();
        foreach (var (token, succeeds, name) in new[]
        {
            (response.AccessToken, true, "Bearer middleware accepts issued JWT"),
            (expired.Value, false, "Bearer middleware rejects expired JWT"),
            ("invalid", false, "Bearer middleware rejects malformed JWT"),
            ("", false, "Bearer middleware requires token")
        })
        {
            using var scope = provider.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            if (token.Length > 0) context.Request.Headers.Authorization = "Bearer " + token;
            check((await context.AuthenticateAsync()).Succeeded == succeeds, name);
        }
        try { new JwtSettings().Validate(); check(false, "missing config rejected"); }
        catch (InvalidOperationException) { check(true, "missing JWT configuration fails clearly"); }

        user.PasswordHash = hasher.HashPassword("password123");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<Application.Interfaces.Services.IAuthService>(service);
        builder.Services.AddSingleton<IUnitOfWork>(uow);
        builder.Services.AddControllers().AddApplicationPart(typeof(WebApi.Controllers.AuthController).Assembly);
        builder.Services.AddAivesAuthentication(settings);
        await using var app = builder.Build();
        app.UseMiddleware<WebApi.Middleware.ExceptionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("student", "password123"));
            var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();
            check(login.StatusCode == HttpStatusCode.OK && payload!.User.Id == user.Id, "HTTP login succeeds without authentication");
            using var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("student", "wrong"));
            check(wrong.StatusCode == HttpStatusCode.Unauthorized && wrong.Content.Headers.ContentType!.MediaType == "application/problem+json", "HTTP wrong credentials returns 401 ProblemDetails");
            using var invalid = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));
            check(invalid.StatusCode == HttpStatusCode.BadRequest, "HTTP empty credentials returns 400");
            using var anonymous = await client.GetAsync("/api/auth/me");
            check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "HTTP me without token returns 401");
            client.DefaultRequestHeaders.Authorization = new("Bearer", payload!.AccessToken);
            using var me = await client.GetAsync("/api/auth/me");
            check(me.StatusCode == HttpStatusCode.OK && (await me.Content.ReadAsStringAsync()).Contains(user.Id.ToString()), "HTTP me authenticates issued token");
            client.DefaultRequestHeaders.Authorization = new("Bearer", expired.Value);
            using var denied = await client.GetAsync("/api/auth/me");
            check(denied.StatusCode == HttpStatusCode.Unauthorized, "HTTP expired JWT returns 401");
        }
        finally { await app.StopAsync(); }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AuthUnitOfWork : IUnitOfWork
    {
        public AuthRepository Repository { get; } = new();
        public IUserRepository Users => Repository;
        public IQuestionRepository Questions => throw new NotSupportedException();
        public IGenericRepository<Course> Courses => throw new NotSupportedException();
        public IGenericRepository<Rubric> Rubrics => throw new NotSupportedException();
        public Task<int> SaveChangesAsync() => throw new InvalidOperationException("Login must not persist changes.");
        public void Dispose() { }
    }

    private sealed class AuthRepository : IUserRepository
    {
        public User? User;
        public Task<User?> GetByUsernameAsync(string username) => Task.FromResult(User?.Username == username ? User : null);
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(User?.Id == id ? User : null);
        public Task<IEnumerable<User>> GetAllAsync() => throw new NotSupportedException();
        public Task AddAsync(User user) => throw new NotSupportedException();
        public void Update(User user) => throw new NotSupportedException();
        public void Delete(User user) => throw new NotSupportedException();
    }
}
