using Application.DTOs.Interviews;

namespace Application.Services;

/// <summary>Pure scoring rules shared by AI grading and lecturer finalization.</summary>
public static class ScoreCalculator
{
    private const decimal WeightTolerance = 0.01m;

    public static decimal CalculateQuestionScore(decimal points, IReadOnlyCollection<RubricSnapshotCriterion> rubric, IReadOnlyCollection<RubricEarnedScore> earned)
    {
        if (points <= 0 || rubric.Count == 0 || rubric.Any(x => x.RubricId == Guid.Empty || string.IsNullOrWhiteSpace(x.Criteria) || x.Weight <= 0 || x.MaxScore <= 0) ||
            Math.Abs(rubric.Sum(x => x.Weight) - 100m) > WeightTolerance)
            throw new ArgumentException("Rubric snapshot is invalid.");
        if (earned.Count != rubric.Count || earned.Select(x => x.RubricId).Distinct().Count() != earned.Count || earned.Any(x => x.RubricId == Guid.Empty))
            throw new ArgumentException("Exactly one score is required for each rubric criterion.");
        var rubricById = rubric.ToDictionary(x => x.RubricId);
        decimal weighted = 0m;
        foreach (var score in earned)
        {
            if (!rubricById.TryGetValue(score.RubricId, out var criterion) || score.EarnedScore < 0 || score.EarnedScore > criterion.MaxScore)
                throw new ArgumentException("A rubric score is outside its allowed range.");
            weighted += (score.EarnedScore / criterion.MaxScore) * (criterion.Weight / 100m);
        }
        return points * weighted;
    }

    public static decimal CalculateAttemptTotal(IEnumerable<(decimal QuestionScore, decimal Points)> questions)
    {
        var all = questions.ToArray();
        if (all.Length == 0 || all.Any(x => x.Points <= 0 || x.QuestionScore < 0 || x.QuestionScore > x.Points))
            throw new ArgumentException("Question scores or points are invalid.");
        return 10m * all.Sum(x => x.QuestionScore) / all.Sum(x => x.Points);
    }

    public static decimal RoundForStorage(decimal score) => Math.Round(score, 2, MidpointRounding.AwayFromZero);
}
