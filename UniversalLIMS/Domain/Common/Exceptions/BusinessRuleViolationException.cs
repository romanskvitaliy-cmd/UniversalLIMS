namespace UniversalLIMS.Domain.Common.Exceptions;

/// <summary>
/// Raised when an operation violates an invariant or workflow rule.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }
}
