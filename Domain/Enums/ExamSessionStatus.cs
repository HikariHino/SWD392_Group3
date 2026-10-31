namespace Domain.Enums;

/// <summary>
/// Vòng đời ca thi. Draft → Open → Closed; Draft/Open → Cancelled.
/// </summary>
public enum ExamSessionStatus
{
    Draft      = 1,
    Open       = 2,
    Closed     = 3,
    Cancelled  = 4,
}
