namespace UniversalLIMS.Domain.Common.Exceptions;

/// <summary>
/// Base type for predictable domain and application rule failures.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
