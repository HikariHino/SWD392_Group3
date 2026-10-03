using Microsoft.EntityFrameworkCore;
using FluentValidation;
using WebApi.Hubs;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.Validators.QuestionBank;
using Infrastructure.Persistence;
using Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Thêm Controllers & Swagger UI
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "AIVES API - AI-Powered Viva Exam System",
        Version = "v1",
        Description = "API hệ thống thi vấn đáp trực tuyến AIVES - SWD392 Group 3 (.NET 10 Onion Architecture)"
    });
});

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 7. Đăng ký Dependency Injection cho tầng Data Access (Repositories & UnitOfWork)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();

// 8. Đăng ký Dependency Injection cho tầng Application Services
builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IInterviewService, InterviewService>();
builder.Services.AddScoped<IGradingService, GradingService>();
builder.Services.AddScoped<IPasswordHasher, Infrastructure.Security.PasswordHasher>();

// 9. Đăng ký Dependency Injection cho External Services
builder.Services.AddScoped<Application.Interfaces.ExternalServices.IOpenAIService, Infrastructure.ExternalServices.OpenAIService>();

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

    // Tự động Seed dữ liệu mẫu Course và Question khi khởi động lần đầu
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AivesDbContext>();
        await DatabaseSeeder.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DatabaseSeeder Error]: {ex.Message}");
    }
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseAuthorization();

// Ánh xạ Endpoint cho Controllers và SignalR Hub
app.MapControllers();
app.MapHub<InterviewHub>("/interviewHub");

app.Run();
