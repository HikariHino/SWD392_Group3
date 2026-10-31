using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Application.Common;
using Application.DTOs.QuestionBank;
using Application.Interfaces.Services;
using Application.Validators.QuestionBank;
using Domain.Entities.QuestionBank;
using WebApi.Controllers;
using WebApi.Importing;

internal static class QuestionCsvImportChecks
{
    private sealed class ImportService : IQuestionBankService
    {
        public int Calls { get; private set; }
        public ImportQuestionsRequest? LastRequest { get; private set; }
        public Task<int> ImportQuestionsBatchAsync(ImportQuestionsRequest request)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(request.Questions.Count);
        }
        public Task<PagedResponse<QuestionDto>> GetQuestionsAsync(QuestionQueryParameters query) => throw new NotSupportedException();
        public Task<QuestionDto?> GetQuestionByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<QuestionDto> CreateQuestionAsync(CreateQuestionRequest request) => throw new NotSupportedException();
        public Task<QuestionDto?> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request) => throw new NotSupportedException();
        public Task<bool> DeleteQuestionAsync(Guid id) => throw new NotSupportedException();
        public Task<AiGeneratedQuestionDto> GenerateQuestionWithAiAsync(AiGenerateQuestionRequest request) => throw new NotSupportedException();
        public Task<IEnumerable<CourseDto>> GetCoursesAsync() => throw new NotSupportedException();
    }

    public static Task Run(Action<bool, string> check)
    {
        var courseId = Guid.NewGuid();
        var parsed = QuestionCsvParser.Parse("\uFEFFContent,BloomLevel\r\n\"Explain, compare\",Analyze\r\n\"Use \"\"quoted\"\" text\",Apply\r\n", courseId);
        check(parsed.Errors.Count == 0 && parsed.Rows.Count == 2, "CSV parser accepts UTF-8 BOM, quoted commas and escaped quotes");
        check(parsed.Rows[0].Request.Content == "Explain, compare" && parsed.Rows[0].Request.CourseId == courseId,
            "CSV rows use selected course and preserve quoted content");
        check(parsed.Rows[1].Request.Content == "Use \"quoted\" text", "escaped CSV quotes are decoded");

        var invalid = QuestionCsvParser.Parse("Content,BloomLevel\nValid,Remember\nBroken,NotABloomLevel\n\"unterminated,Apply\nExtra,Apply,Column\n", courseId);
        check(invalid.Rows.Count == 1 && invalid.Errors.Select(x => x.Line).SequenceEqual([3, 4, 5]),
            "CSV parser reports malformed fields and invalid enums at physical line numbers");
        check(QuestionCsvParser.Parse("Content,BloomLevel\nA,Remember", Guid.Empty).Errors.Any(x => x.Field == "courseId"),
            "CSV import requires a course");
        check(QuestionCsvParser.Parse("Content,BloomLevel\n", courseId).Errors.Count > 0,
            "empty question batch is rejected");
        var tooMany = "Content,BloomLevel\n" + string.Concat(Enumerable.Repeat("Question,Remember\n", QuestionCsvParser.MaxRows + 1));
        check(QuestionCsvParser.Parse(tooMany, courseId).Errors.Any(x => x.Message.Contains("at most")),
            "CSV row limit is enforced");
        var import = typeof(QuestionsController).GetMethod(nameof(QuestionsController.ImportQuestions))!;
        check(import.GetCustomAttributes(typeof(ConsumesAttribute), true).Cast<ConsumesAttribute>().Single().ContentTypes.Contains("multipart/form-data"),
            "question import endpoint consumes multipart form data");
        return CheckControllerUpload(check, courseId);
    }

    private static async Task CheckControllerUpload(Action<bool, string> check, Guid courseId)
    {
        var service = new ImportService();
        var controller = new QuestionsController(service, new CreateQuestionRequestValidator(), new UpdateQuestionRequestValidator());
        static IFormFile File(string content) => new FormFile(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), 0,
            System.Text.Encoding.UTF8.GetByteCount(content), "file", "questions.csv");
        var valid = await controller.ImportQuestions(File("Content,BloomLevel\nExplain the design,Understand\n"), courseId, CancellationToken.None);
        check(valid is ObjectResult { StatusCode: 201 } && service.Calls == 1 &&
              service.LastRequest?.Questions.Count == 1 && service.LastRequest.Questions[0].CourseId == courseId,
            "valid multipart upload imports the parsed batch once");
        var invalid = await controller.ImportQuestions(File("Content,BloomLevel\nValid,Remember\n,Create\n"), courseId, CancellationToken.None);
        check(invalid is BadRequestObjectResult && service.Calls == 1,
            "one invalid CSV row rejects the entire upload without calling persistence");
    }
}
