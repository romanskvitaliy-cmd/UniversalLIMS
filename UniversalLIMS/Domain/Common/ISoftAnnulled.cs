namespace UniversalLIMS.Domain.Common;

public interface ISoftAnnulled
{
    bool IsAnnulled { get; set; }

    DateTime? AnnulledAtUtc { get; set; }

    string? AnnulledByUserId { get; set; }

    string? AnnulmentReason { get; set; }
}
