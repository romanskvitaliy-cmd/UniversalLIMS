namespace UniversalLIMS.Application.Expert;

/// <summary>Коротке сповіщення про пробу, готову до розгляду експертом після заповнення лабораторією.</summary>
public sealed class IncomingExpertSampleNotificationDto
{
    public Guid SampleId { get; init; }

    public required string SampleNumber { get; init; }

    public required string CustomerFullName { get; init; }

    public required string InvestigationTypeName { get; init; }

    public required string TargetBranchName { get; init; }

    public DateTime ResultsEnteredAtUtc { get; init; }
}
