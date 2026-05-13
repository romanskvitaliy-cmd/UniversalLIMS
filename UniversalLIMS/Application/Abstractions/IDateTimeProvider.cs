namespace UniversalLIMS.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
