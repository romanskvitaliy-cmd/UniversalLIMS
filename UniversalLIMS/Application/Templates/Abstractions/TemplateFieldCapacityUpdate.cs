using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record TemplateFieldCapacityUpdate(
    int? EstimatedCapacityChars,
    int? MaxLines,
    bool AllowMultiline,
    FieldOverflowPolicy OverflowPolicy);
