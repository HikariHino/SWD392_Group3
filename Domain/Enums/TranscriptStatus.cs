namespace Domain.Enums;

/// <summary>
/// Vòng đời bài thi của sinh viên.
/// InProgress → Submitted → Grading → AwaitingReview → Finalized.
/// Grading thất bại → quay lại Submitted để retry.
/// </summary>
public enum TranscriptStatus
{
    InProgress     = 1,
    Submitted      = 2,
    Grading        = 3,
    AwaitingReview = 4,
    Finalized      = 5,
}
