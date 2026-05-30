namespace UniversalLIMS.Application.Registration;

public static class OrderFieldMappingSampleGroupBuilder
{
    public static IReadOnlyList<OrderFieldMappingSampleGroupDto> Build(
        IReadOnlyList<OrderCreateSampleInput> samples,
        OrderCreateFormDto form,
        OrderFieldMappingPrepareDto mapping)
    {
        if (samples.Count == 0)
        {
            return [];
        }

        var templatesByVersionId = mapping.Templates.ToDictionary(template => template.TemplateVersionId);
        var investigationNames = form.InvestigationTypes.ToDictionary(type => type.Id, type => type.NameUk);
        var groups = new List<OrderFieldMappingSampleGroupDto>();

        for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
        {
            var sample = samples[sampleIndex];
            var slots = new List<OrderFieldMappingTemplateSlotDto>();

            if (sample.ReferralTemplateVersionId is Guid referralId
                && referralId != Guid.Empty
                && templatesByVersionId.TryGetValue(referralId, out var referralTemplate))
            {
                slots.Add(ToSlot(referralTemplate, "Направлення"));
            }

            foreach (var protocolVersionId in sample.SelectedTemplateVersionIds.Where(id => id != Guid.Empty))
            {
                if (!templatesByVersionId.TryGetValue(protocolVersionId, out var protocolTemplate))
                {
                    continue;
                }

                slots.Add(ToSlot(protocolTemplate, "Протокол"));
            }

            if (slots.Count == 0)
            {
                continue;
            }

            investigationNames.TryGetValue(sample.InvestigationTypeId, out var investigationNameUk);
            var label = BuildSampleLabel(sampleIndex, samples.Count, investigationNameUk);

            groups.Add(new OrderFieldMappingSampleGroupDto
            {
                SampleIndex = sampleIndex,
                Label = label,
                Templates = slots
            });
        }

        return groups;
    }

    private static string BuildSampleLabel(int sampleIndex, int sampleCount, string? investigationNameUk)
    {
        if (sampleCount <= 1)
        {
            return string.IsNullOrWhiteSpace(investigationNameUk)
                ? "Проба"
                : investigationNameUk;
        }

        var prefix = $"Проба {sampleIndex + 1}";
        return string.IsNullOrWhiteSpace(investigationNameUk)
            ? prefix
            : $"{prefix} — {investigationNameUk}";
    }

    private static OrderFieldMappingTemplateSlotDto ToSlot(
        OrderFieldMappingTemplateDto template,
        string documentRoleUk) =>
        new()
        {
            TemplateVersionId = template.TemplateVersionId,
            TemplateNameUk = template.TemplateNameUk,
            VersionNumber = template.VersionNumber,
            DocumentRoleUk = documentRoleUk,
            Fields = template.Fields
        };
}

/// <summary>Нормалізований рядок проби для побудови груп мапінгу (D7).</summary>
public sealed class OrderCreateSampleInput
{
    public Guid InvestigationTypeId { get; init; }

    public Guid? ReferralTemplateVersionId { get; init; }

    public IReadOnlyList<Guid> SelectedTemplateVersionIds { get; init; } = [];
}
