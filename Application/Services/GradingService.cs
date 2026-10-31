using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
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
    public async Task<IReadOnlyList<AssessmentDto>> GetAttemptAssessmentsAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var (attempt, session) = await OwnedAttempt(attemptId);
        var assessments = await grading.Assessments(attempt.Id);
        return assessments
            .OrderBy(x => session.ExamSessionQuestions.Single(q => q.Id == x.ExamSessionQuestionId).DisplayOrder)
            .Select(x => ToDto(x, session))
            .ToArray();
    }

    public async Task<AssessmentDto> FinalizeAssessmentAsync(Guid assessmentId, FinalizeAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        RequireLecturer();
        if (request is null) Invalid("Request", "A request body is required.");

        var assessment = await grading.Assessment(assessmentId) ?? throw new NotFoundException("Assessment not found.");
        var session = assessment.Transcript?.Session ?? throw new NotFoundException("Session not found.");
        EnsureSessionOwner(session);

        if (assessment.Transcript!.Status != TranscriptStatus.AwaitingReview)
            throw new ConflictException("Only attempts awaiting lecturer review can be finalized.");
        if (assessment.FinalScore is not null || assessment.FinalizedAt is not null || assessment.FinalizedByUserId is not null)
            throw new ConflictException("This assessment has already been finalized.");
        if (request.Version is null)
            Invalid("Version", "Version is required and must not be negative.");
        var version = request.Version.GetValueOrDefault();
        if (version < 0)
            Invalid("Version", "Version is required and must not be negative.");
        if (version != assessment.Version)
            throw new ConflictException("Assessment version is stale. Reload and retry.");

        if (request.FinalScore < 0m || request.FinalScore > assessment.Points)
            Invalid("FinalScore", $"FinalScore must be between 0 and {assessment.Points}.");
        var comment = request.TeacherComment?.Trim();
        if (comment?.Length > 2000)
            Invalid("TeacherComment", "TeacherComment must not exceed 2000 characters.");

        assessment.FinalScore = ScoreCalculator.RoundForStorage(request.FinalScore);
        assessment.TeacherComment = string.IsNullOrEmpty(comment) ? null : comment;
        assessment.FinalizedByUserId = user.Id!.Value;
        assessment.FinalizedAt = clock.GetUtcNow().UtcDateTime;
        assessment.Version++;

        var attempt = assessment.Transcript;
        var sessionWithQuestions = await grading.Session(session.Id)
            ?? throw new NotFoundException("Session not found.");
        var allAssessments = await grading.Assessments(attempt.Id);
        var finalByQuestion = allAssessments
            .Where(x => x.FinalScore.HasValue)
            .ToDictionary(x => x.ExamSessionQuestionId, x => x.FinalScore!.Value);
        if (sessionWithQuestions.ExamSessionQuestions.Count > 0 &&
            sessionWithQuestions.ExamSessionQuestions.All(x => finalByQuestion.ContainsKey(x.Id)))
        {
            attempt.TotalScore = ScoreCalculator.RoundForStorage(ScoreCalculator.CalculateAttemptTotal(
                sessionWithQuestions.ExamSessionQuestions.Select(x => (finalByQuestion[x.Id], x.Points))));
            attempt.Status = TranscriptStatus.Finalized;
            attempt.Version++;
        }
        await grading.Save();

        return ToDto(assessment, session);
    }

    public async Task<AttemptResultDto> GetAttemptResultAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await grading.Attempt(attemptId) ?? throw new NotFoundException("Attempt not found.");
        var session = await grading.Session(attempt.SessionId) ?? throw new NotFoundException("Session not found.");
        if (user.IsStudent && user.Id == attempt.UserId)
        {
            if (attempt.Status != TranscriptStatus.Finalized)
                throw new ConflictException("Official results are available after the lecturer finalizes every question.");
        }
        else if (!user.IsLecturer || user.Id is null || session.CreatedByUserId != user.Id)
        {
            throw new ForbiddenException();
        }

        var assessments = await grading.Assessments(attempt.Id);
        var byQuestion = assessments.ToDictionary(x => x.ExamSessionQuestionId);
        var pending = session.ExamSessionQuestions.Count(question =>
            !byQuestion.TryGetValue(question.Id, out var assessment) || !assessment.FinalScore.HasValue);
        decimal? aiTotal = null;
        if (session.ExamSessionQuestions.Count > 0 && session.ExamSessionQuestions.All(question =>
                byQuestion.TryGetValue(question.Id, out var assessment) && assessment.AiSuggestedScore.HasValue))
        {
            aiTotal = ScoreCalculator.RoundForStorage(ScoreCalculator.CalculateAttemptTotal(
                session.ExamSessionQuestions.Select(question => (byQuestion[question.Id].AiSuggestedScore!.Value, question.Points))));
        }

        return new AttemptResultDto
        {
            AttemptId = attempt.Id,
            Status = attempt.Status.ToString(),
            AiSuggestedTotal = aiTotal,
            TotalScore = pending == 0 && attempt.Status == TranscriptStatus.Finalized ? attempt.TotalScore : null,
            GradedAt = attempt.GradedAt,
            IsComplete = session.ExamSessionQuestions.Count > 0 && pending == 0,
            PendingQuestionCount = pending,
            Assessments = assessments
                .OrderBy(x => session.ExamSessionQuestions.Single(question => question.Id == x.ExamSessionQuestionId).DisplayOrder)
                .Select(x => ToDto(x, session))
                .ToArray()
        };
    }

    public async Task<IReadOnlyList<AssessmentDto>> GradeAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var (attempt, session) = await OwnedAttempt(attemptId);
        if (attempt.Status == TranscriptStatus.AwaitingReview)
            return (await grading.Assessments(attempt.Id)).Select(x => ToDto(x, session)).ToArray();
        if (attempt.Status != TranscriptStatus.Submitted)
            throw new ConflictException("Only submitted attempts can be graded.");

        var existing = await grading.Assessments(attempt.Id);
        var existingByQuestion = existing.ToDictionary(x => x.ExamSessionQuestionId);
        var candidates = session.ExamSessionQuestions
            .Select(question => new { Question = question, Answer = string.Join("\n", attempt.Turns.Where(t => t.Speaker == "Student" && t.ExamSessionQuestionId == question.Id).OrderBy(t => t.Sequence).Select(t => t.ContentText)) })
            .Where(x => !existingByQuestion.ContainsKey(x.Question.Id))
            .ToArray();
        if (candidates.Length == 0)
            return existing.Select(x => ToDto(x, session)).ToArray();

        attempt.Status = TranscriptStatus.Grading;
        var created = new List<Assessment>();
        try
        {
            foreach (var item in candidates)
            {
                if (string.IsNullOrWhiteSpace(item.Answer))
                {
                    created.Add(new Assessment
                    {
                        TranscriptId = attempt.Id,
                        ExamSessionQuestionId = item.Question.Id,
                        StudentAnswer = string.Empty,
                        Points = item.Question.Points,
                        RubricSnapshotJson = item.Question.RubricSnapshotJson
                    });
                    continue;
                }
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

    private async Task<(Transcript Attempt, ExamSession Session)> OwnedAttempt(Guid attemptId)
    {
        RequireLecturer();
        var attempt = await grading.Attempt(attemptId) ?? throw new NotFoundException("Attempt not found.");
        var session = await grading.Session(attempt.SessionId) ?? throw new NotFoundException("Session not found.");
        EnsureSessionOwner(session);
        return (attempt, session);
    }

    private void RequireLecturer()
    {
        if (!user.IsLecturer || user.Id is null) throw new ForbiddenException();
    }

    private void EnsureSessionOwner(ExamSession session)
    {
        if (session.CreatedByUserId != user.Id) throw new ForbiddenException();
    }

    [DoesNotReturn]
    private static void Invalid(string field, string message) =>
        throw new ValidationException(new Dictionary<string, string[]> { [field] = [message] });

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

    private static AssessmentDto ToDto(Assessment assessment, ExamSession session) => new()
    {
        Id = assessment.Id,
        AttemptId = assessment.TranscriptId,
        QuestionId = assessment.ExamSessionQuestion?.QuestionId
            ?? session.ExamSessionQuestions.Single(x => x.Id == assessment.ExamSessionQuestionId).QuestionId,
        StudentAnswer = assessment.StudentAnswer,
        Points = assessment.Points,
        AiSuggestedScore = assessment.AiSuggestedScore,
        AiFeedback = assessment.AiFeedback,
        AiConfidenceLevel = assessment.AiConfidenceLevel,
        FinalScore = assessment.FinalScore,
        TeacherComment = assessment.TeacherComment,
        FinalizedByUserId = assessment.FinalizedByUserId,
        FinalizedAt = assessment.FinalizedAt,
        Version = assessment.Version
    };
}
