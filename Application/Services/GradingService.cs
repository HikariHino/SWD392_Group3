using System.Text.Json;
using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities.Interviews;
using Domain.Enums;

namespace Application.Services;

public sealed class GradingService(IGradingRepository grading, IAiService ai, ICurrentUser user, TimeProvider clock) : IGradingService
{
    public async Task<IReadOnlyList<AssessmentDto>> GradeAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        if (!user.IsLecturer || user.Id is null) throw new ForbiddenException();
        var attempt = await grading.Attempt(attemptId) ?? throw new NotFoundException("Attempt not found.");
        var session = await grading.Session(attempt.SessionId) ?? throw new NotFoundException("Session not found.");
        if (session.CreatedByUserId != user.Id) throw new ForbiddenException();
        if (attempt.Status == TranscriptStatus.AwaitingReview)
            return (await grading.Assessments(attempt.Id)).Select(x => ToDto(x, session)).ToArray();
        if (attempt.Status != TranscriptStatus.Submitted)
            throw new ConflictException("Only submitted attempts can be graded.");

        var existing = await grading.Assessments(attempt.Id);
        var existingByQuestion = existing.ToDictionary(x => x.ExamSessionQuestionId);
        var candidates = session.ExamSessionQuestions
            .Select(question => new { Question = question, Answer = string.Join("\n", attempt.Turns.Where(t => t.Speaker == "Student" && t.ExamSessionQuestionId == question.Id).OrderBy(t => t.Sequence).Select(t => t.ContentText)) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Answer) && !existingByQuestion.ContainsKey(x.Question.Id))
            .ToArray();
        if (candidates.Length == 0)
            return existing.Select(x => ToDto(x, session)).ToArray();

        attempt.Status = TranscriptStatus.Grading;
        var created = new List<Assessment>();
        try
        {
            foreach (var item in candidates)
            {
                var rubric = ParseRubric(item.Question.RubricSnapshotJson);
                var response = await ai.GenerateAssessmentAsync(new AiAssessmentRequest
                {
                    QuestionContent = item.Question.Content,
                    ExpectedAnswer = item.Question.ExpectedAnswer,
                    StudentAnswer = item.Answer,
                    Rubric = rubric
                }, cancellationToken);
                decimal score;
                try { score = ScoreCalculator.CalculateQuestionScore(item.Question.Points, rubric, response.RubricScores); }
                catch (ArgumentException) { throw new AiProviderResponseException(); }
                if (response.ConfidenceLevel is < 0m or > 1m || response.Feedback?.Length > 4000)
                    throw new AiProviderResponseException();
                created.Add(new Assessment
                {
                    TranscriptId = attempt.Id, ExamSessionQuestionId = item.Question.Id,
                    StudentAnswer = item.Answer, Points = item.Question.Points, RubricSnapshotJson = item.Question.RubricSnapshotJson,
                    AiSuggestedScore = ScoreCalculator.RoundForStorage(score), AiFeedback = response.Feedback?.Trim(),
                    AiConfidenceLevel = response.ConfidenceLevel, EvaluatedAt = clock.GetUtcNow().UtcDateTime
                });
            }
        }
        catch
        {
            attempt.Status = TranscriptStatus.Submitted;
            throw;
        }
        grading.AddRange(created);
        attempt.Status = TranscriptStatus.AwaitingReview;
        attempt.GradedAt = clock.GetUtcNow().UtcDateTime;
        attempt.Version++; session.Version++;
        await grading.Save();
        return existing.Concat(created).Select(x => ToDto(x, session)).ToArray();
    }

    private static IReadOnlyList<RubricSnapshotCriterion> ParseRubric(string snapshot)
    {
        try
        {
            var rubric = JsonSerializer.Deserialize<List<RubricSnapshotCriterion>>(snapshot, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rubric is null || rubric.Count == 0) throw new JsonException();
            return rubric;
        }
        catch (JsonException) { throw new AiProviderResponseException(); }
    }

    private static AssessmentDto ToDto(Assessment assessment, ExamSession session)
    {
        var questionId = session.ExamSessionQuestions.Single(x => x.Id == assessment.ExamSessionQuestionId).QuestionId;
        return new AssessmentDto { Id = assessment.Id, AttemptId = assessment.TranscriptId, QuestionId = questionId, StudentAnswer = assessment.StudentAnswer, Points = assessment.Points, AiSuggestedScore = assessment.AiSuggestedScore, AiFeedback = assessment.AiFeedback, AiConfidenceLevel = assessment.AiConfidenceLevel, FinalScore = assessment.FinalScore, TeacherComment = assessment.TeacherComment, FinalizedByUserId = assessment.FinalizedByUserId, FinalizedAt = assessment.FinalizedAt, Version = assessment.Version };
    }
}
