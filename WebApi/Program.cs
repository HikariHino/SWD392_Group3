using Microsoft.EntityFrameworkCore;
using FluentValidation;
using WebApi.Hubs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.Validators.QuestionBank;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using WebApi.Security;
using WebApi.Swagger;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<Infrastructure.Security.JwtSettings>()
    ?? new Infrastructure.Security.JwtSettings();
jwt.Validate();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAccessTokenIssuer, Infrastructure.Security.JwtTokenIssuer>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAivesAuthentication(jwt);

// Keep local development logs on the console. The Windows Event Log provider
// can fail when the process does not have permission to write system events.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 1. Thêm Controllers & Swagger UI
builder.Services.AddControllers();
builder.Services.AddAivesSwagger();

// 2. Thêm CORS cho kết nối Frontend React
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Thêm SignalR
builder.Services.AddSignalR();

// 4. Đăng ký AutoMapper quét toàn bộ Profile
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));

// 5. Đăng ký FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateQuestionRequestValidator>();

// 6. Cấu hình Database SQL Server
builder.Services.AddDbContext<AivesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

// 7. Đăng ký Dependency Injection cho tầng Data Access (Repositories & UnitOfWork)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();

// 8. Đăng ký Dependency Injection cho tầng Application Services
builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IExamSessionRepository, ExamSessionRepository>();
builder.Services.AddScoped<IExamSessionService, ExamSessionService>();
builder.Services.AddScoped<IAttemptRepository, AttemptRepository>();
builder.Services.AddScoped<IAttemptService, AttemptService>();
builder.Services.AddScoped<IInterviewService, InterviewService>();
builder.Services.AddScoped<IGradingRepository, GradingRepository>();
builder.Services.AddScoped<IGradingService, GradingService>();
builder.Services.AddScoped<IPasswordHasher, Infrastructure.Security.PasswordHasher>();
builder.Services.AddScoped<IVoiceInterviewService, VoiceInterviewService>();
builder.Services.AddHttpClient<Application.Interfaces.ExternalServices.ISpeechToTextService, Infrastructure.ExternalServices.AzureSpeechToTextService>();
builder.Services.AddHttpClient<Application.Interfaces.ExternalServices.ITextToSpeechService, Infrastructure.ExternalServices.AzureTextToSpeechService>();

// 9. Đăng ký Gemini AI Service (HttpClient + IAiService)
builder.Services.AddHttpClient<Application.Interfaces.ExternalServices.IAiService, Infrastructure.ExternalServices.GeminiService>(client =>
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));

var app = builder.Build();

app.UseMiddleware<WebApi.Middleware.ExceptionMiddleware>();

// Cấu hình HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIVES API v1");
        c.RoutePrefix = string.Empty; // Mở thẳng Swagger UI ngay tại trang chủ localhost:5110
    });

    if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (builder.Configuration.GetValue<bool>("Database:SeedDevelopmentData"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        await DatabaseSeeder.SeedAsync(dbContext);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

// Ánh xạ Endpoint cho Controllers và SignalR Hub
app.MapControllers();
app.MapHub<InterviewHub>("/interviewHub");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (AivesDbContext db, CancellationToken ct) =>
{
    try { return await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    catch { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
}).AllowAnonymous();

app.Run();
