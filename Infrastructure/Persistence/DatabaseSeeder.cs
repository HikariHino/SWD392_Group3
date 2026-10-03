using Microsoft.EntityFrameworkCore;
using Domain.Entities.QuestionBank;
using Domain.Enums;

namespace Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AivesDbContext context)
    {
        // Tự động migrate / tạo DB nếu chưa có
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Courses nếu chưa có
        if (!await context.Courses.AnyAsync())
        {
            var course1 = new Course
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Code = "SWD392",
                Name = "Software Architecture and Design",
                Description = "Kiến trúc và thiết kế phần mềm doanh nghiệp (.NET 8, Onion Architecture)"
            };

            var course2 = new Course
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Code = "PRN231",
                Name = "Building Cross-Platform Web APIs with .NET",
                Description = "Lập trình ứng dụng phân tán và Web API đa nền tảng"
            };

            await context.Courses.AddRangeAsync(course1, course2);
            await context.SaveChangesAsync();

            // 2. Seed 1 câu hỏi mẫu có Rubric cho SWD392
            var question = new Question
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                CourseId = course1.Id,
                Content = "Trình bày sự khác biệt giữa Onion Architecture và Kiến trúc 3 tầng truyền thống (3-Tier Architecture)? Tại sao nên áp dụng Dependency Inversion Principle (DIP)?",
                BloomLevel = BloomLevel.Analyze,
                Rubrics = new List<Rubric>
                {
                    new Rubric
                    {
                        Id = Guid.NewGuid(),
                        Criteria = "Hiểu đúng định nghĩa và chiều hướng phụ thuộc của Onion Architecture",
                        Weight = 40.0,
                        MaxScore = 4.0
                    },
                    new Rubric
                    {
                        Id = Guid.NewGuid(),
                        Criteria = "Phân tích được nhược điểm phụ thuộc Database của kiến trúc 3 tầng",
                        Weight = 30.0,
                        MaxScore = 3.0
                    },
                    new Rubric
                    {
                        Id = Guid.NewGuid(),
                        Criteria = "Giải thích rõ ràng lợi ích thực tế của Dependency Inversion Principle (DIP)",
                        Weight = 30.0,
                        MaxScore = 3.0
                    }
                }
            };

            await context.Questions.AddAsync(question);
            await context.SaveChangesAsync();
        }
    }
}
