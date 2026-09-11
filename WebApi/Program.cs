using WebApi.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(); // Thêm hỗ trợ API Controllers
builder.Services.AddSignalR();     // Thêm SignalR cho Nhóm chức năng 3
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies())); // Đăng ký AutoMapper quét toàn bộ Profile

// 1. Cấu hình Database kết nối với SQL Server
builder.Services.AddDbContext<Infrastructure.Persistence.AivesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Đăng ký Dependency Injection cho tầng Application (Logic)
builder.Services.AddScoped<Application.Interfaces.Services.IQuestionBankService, Application.Services.QuestionBankService>();
builder.Services.AddScoped<Application.Interfaces.Services.IInterviewService, Application.Services.InterviewService>();
builder.Services.AddScoped<Application.Interfaces.Services.IGradingService, Application.Services.GradingService>();

// 3. Đăng ký Dependency Injection cho tầng Infrastructure (External API)
builder.Services.AddScoped<Application.Interfaces.ExternalServices.IOpenAIService, Infrastructure.ExternalServices.OpenAIService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Ánh xạ Endpoint cho Controllers và SignalR Hub
app.MapControllers();
app.MapHub<InterviewHub>("/interviewHub");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
