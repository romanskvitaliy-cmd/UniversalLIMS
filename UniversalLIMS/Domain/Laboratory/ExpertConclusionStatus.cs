namespace UniversalLIMS.Domain.Laboratory;

/// <summary>Статус експертного розгляду висновку по пробі (етап 3).</summary>
public enum ExpertConclusionStatus
{
    PendingReview = 0,
    InProgress = 1,
    Approved = 2,

    /// <summary>Повернено в лабораторію на доопрацювання після експертизи.</summary>
    ReturnedForRework = 3
}
