using Domain.Entities.QuestionBank;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class SqlServerChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        // Always isolated LocalDB: never read the application's shared Azure connection.
        var databaseName = "AIVES_M1Checks_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30";
        await using var db = new AivesDbContext(new DbContextOptionsBuilder<AivesDbContext>().UseSqlServer(connection).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260927162533_InitialCreate");
            var course = new Course { Code = "PRESERVE", Name = "Existing course before User migration" };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            var courseId = course.Id;
            await db.Database.MigrateAsync();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            check(applied.SequenceEqual(new[] { "20260927162533_InitialCreate", "20261003125521_AddUserTable" }), "SQL Server applies InitialCreate then AddUserTable");
            check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "SQL Server has no pending migrations");
            check(!db.Database.HasPendingModelChanges(), "SQL Server migration snapshot matches model");
            check(await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.Code == "PRESERVE"), "User migration preserves existing course data");
            await db.Database.MigrateAsync();
            check((await db.Database.GetAppliedMigrationsAsync()).Count() == 2, "SQL Server migration repeat is idempotent");
            check(!await db.Users.AnyAsync(), "SQL Server User table is queryable after migration");
            check(await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'IX_Users_Username' AND is_unique = 1").SingleAsync() == 1,
                "SQL Server creates unique username index");
            check(await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'UserId' AND TYPE_NAME(user_type_id) = N'uniqueidentifier'").SingleAsync() == 1,
                "SQL Server UserId matches Guid schema");

            // Reuse the HTTP acceptance flow with real SQL Server instead of SQLite.
            await AuthorizationChecks.Run(check, connection);
        }
        finally
        {
            // Only delete the unique test DB constructed above, never a supplied connection.
            await db.Database.EnsureDeletedAsync();
        }
        Console.WriteLine("Isolated LocalDB SQL Server migration/HTTP CRUD checks passed; test database removed.");
    }
}
