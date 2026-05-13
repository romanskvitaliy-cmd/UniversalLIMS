namespace UniversalLIMS.Domain.Templates;

public enum FieldOverflowPolicy
{
    Block = 1,
    Warn = 2,
    Allow = 3,
    FlowToNextSegment = 4
}
