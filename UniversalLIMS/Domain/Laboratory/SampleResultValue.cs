using UniversalLIMS.Domain.Common;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Domain.Laboratory;

/// <summary>
/// Immutable laboratory measurement for one <see cref="DataField"/> on a <see cref="Sample"/>.
/// This entity is the SSOT for values with <see cref="DataFieldScope.Result"/>.
/// </summary>
/// <remarks>
/// Field identity is resolved through <see cref="DataFieldId"/> (dictionary SSOT), not <see cref="InvestigationType"/>.
/// Investigation context is implied by <see cref="SampleId"/> / <see cref="Sample.InvestigationTypeId"/>.
/// Corrections follow ISO 17025: annul the erroneous record and create a new instance.
/// </remarks>
public sealed class SampleResultValue : BaseEntity, ISoftAnnulled
{
    private SampleResultValue()
    {
    }

    /// <summary>
    /// Creates a new immutable result entry with full traceability metadata.
    /// </summary>
    /// <param name="sampleId">Target sample (many-to-one SSOT anchor).</param>
    /// <param name="dataFieldId">Mapped dictionary field with <c>Scope = Result</c>.</param>
    /// <param name="storedValue">Captured measurement value.</param>
    /// <param name="unit">Unit of measure at the time of entry (snapshot for audit).</param>
    /// <param name="uncertainty">Expanded measurement uncertainty at the time of entry.</param>
    /// <param name="equipmentId">Equipment used to obtain the result (ISO 17025 traceability).</param>
    /// <param name="enteredAtUtc">UTC timestamp of data entry.</param>
    /// <param name="enteredByUserId">Identity user who entered the result.</param>
    public SampleResultValue(
        Guid sampleId,
        Guid dataFieldId,
        string storedValue,
        string unit,
        decimal uncertainty,
        Guid equipmentId,
        DateTime enteredAtUtc,
        string enteredByUserId)
    {
        if (sampleId == Guid.Empty)
        {
            throw new ArgumentException("Sample identifier is required.", nameof(sampleId));
        }

        if (dataFieldId == Guid.Empty)
        {
            throw new ArgumentException("Data field identifier is required.", nameof(dataFieldId));
        }

        if (string.IsNullOrWhiteSpace(storedValue))
        {
            throw new ArgumentException("Stored value is required.", nameof(storedValue));
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Unit is required.", nameof(unit));
        }

        if (equipmentId == Guid.Empty)
        {
            throw new ArgumentException("Equipment identifier is required.", nameof(equipmentId));
        }

        if (string.IsNullOrWhiteSpace(enteredByUserId))
        {
            throw new ArgumentException("Entered-by user identifier is required.", nameof(enteredByUserId));
        }

        SampleId = sampleId;
        DataFieldId = dataFieldId;
        StoredValue = storedValue.Trim();
        Unit = unit.Trim();
        Uncertainty = uncertainty;
        EquipmentId = equipmentId;
        EnteredAtUtc = enteredAtUtc;
        EnteredByUserId = enteredByUserId.Trim();
    }

    /// <summary>Many-to-one link to the sample that owns this result (SSOT).</summary>
    public Guid SampleId { get; private set; }

    /// <summary>Navigation to the owning sample.</summary>
    public Sample Sample { get; private set; } = null!;

    /// <summary>Dictionary field that defines what was measured (unique per sample among active rows).</summary>
    public Guid DataFieldId { get; private set; }

    /// <summary>Navigation to the mapped <see cref="DataField"/> definition.</summary>
    public DataField DataField { get; private set; } = null!;

    /// <summary>Captured measurement value as entered by the laboratory technician.</summary>
    public string StoredValue { get; private set; } = string.Empty;

    /// <summary>Unit of measure snapshot at entry time (audit trail).</summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>Expanded measurement uncertainty snapshot at entry time.</summary>
    public decimal Uncertainty { get; private set; }

    /// <summary>Equipment used to obtain the result (ISO 17025 traceability).</summary>
    public Guid EquipmentId { get; private set; }

    /// <summary>Navigation to the equipment record.</summary>
    public Equipment Equipment { get; private set; } = null!;

    /// <summary>UTC timestamp when the value was entered.</summary>
    public DateTime EnteredAtUtc { get; private set; }

    /// <summary>Identity user identifier of the technician who entered the value.</summary>
    public string EnteredByUserId { get; private set; } = string.Empty;

    /// <summary>Whether this result row was soft-annulled instead of physically deleted.</summary>
    public bool IsAnnulled { get; private set; }

    /// <summary>UTC timestamp of annulment, when applicable.</summary>
    public DateTime? AnnulledAtUtc { get; private set; }

    /// <summary>Identity user who performed the annulment.</summary>
    public string? AnnulledByUserId { get; private set; }

    /// <summary>Mandatory business reason for annulment (ISO 17025 audit).</summary>
    public string? AnnulmentReason { get; private set; }

    /// <summary>
    /// Soft-annuls this result. Physical deletion is forbidden; a corrected value must be stored as a new row.
    /// </summary>
    /// <param name="reason">Mandatory annulment reason for the audit trail.</param>
    /// <param name="annulledByUserId">Identity user performing the annulment.</param>
    /// <exception cref="InvalidOperationException">The result is already annulled.</exception>
    public void Annul(string reason, string annulledByUserId)
    {
        if (IsAnnulled)
        {
            throw new InvalidOperationException("This result value is already annulled.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Annulment reason is required.", nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(annulledByUserId))
        {
            throw new ArgumentException("Annulled-by user identifier is required.", nameof(annulledByUserId));
        }

        IsAnnulled = true;
        AnnulmentReason = reason.Trim();
        AnnulledByUserId = annulledByUserId.Trim();
        AnnulledAtUtc = DateTime.UtcNow;
    }

    bool ISoftAnnulled.IsAnnulled
    {
        get => IsAnnulled;
        set => IsAnnulled = value;
    }

    DateTime? ISoftAnnulled.AnnulledAtUtc
    {
        get => AnnulledAtUtc;
        set => AnnulledAtUtc = value;
    }

    string? ISoftAnnulled.AnnulledByUserId
    {
        get => AnnulledByUserId;
        set => AnnulledByUserId = value;
    }

    string? ISoftAnnulled.AnnulmentReason
    {
        get => AnnulmentReason;
        set => AnnulmentReason = value;
    }
}
