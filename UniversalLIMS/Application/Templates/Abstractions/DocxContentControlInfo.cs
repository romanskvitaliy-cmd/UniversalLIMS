using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates.Abstractions;

public sealed record DocxContentControlInfo(
    string Tag,
    string? Title,
    WordContentControlType ControlType,
    int SortOrder,
    int? EstimatedCapacityChars,
    bool AllowMultiline);
