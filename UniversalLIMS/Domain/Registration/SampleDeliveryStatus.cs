namespace UniversalLIMS.Domain.Registration;

/// <summary>Статус видачі результатів клієнту після затвердження експертом (v1).</summary>
public enum SampleDeliveryStatus
{
    /// <summary>Ще не готово до видачі (не затверджено або повернено на доопрацювання).</summary>
    None = 0,

    /// <summary>Експерт затвердив — очікує видачі в реєстратурі.</summary>
    ReadyForPickup = 1,

    /// <summary>Результати видано клієнту.</summary>
    Issued = 2
}
