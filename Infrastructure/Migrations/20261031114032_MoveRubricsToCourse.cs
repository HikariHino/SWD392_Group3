using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRubricsToCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "Rubrics",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE r SET CourseId = q.CourseId
                FROM Rubrics AS r
                INNER JOIN Questions AS q ON q.QuestionId = r.QuestionId;
                """);

            migrationBuilder.DropForeignKey(name: "FK_Rubrics_Questions_QuestionId", table: "Rubrics");
            migrationBuilder.DropIndex(name: "IX_Rubrics_QuestionId", table: "Rubrics");
            migrationBuilder.DropColumn(name: "QuestionId", table: "Rubrics");
            migrationBuilder.AlterColumn<Guid>(
                name: "CourseId", table: "Rubrics", type: "uniqueidentifier", nullable: false,
                oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.CreateIndex(name: "IX_Rubrics_CourseId", table: "Rubrics", column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rubrics_Courses_CourseId",
                table: "Rubrics",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "CourseId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("MoveRubricsToCourse cannot be safely rolled back because one course rubric may serve multiple questions.");
        }
    }
}
