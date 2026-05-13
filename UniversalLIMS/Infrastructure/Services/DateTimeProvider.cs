using UniversalLIMS.Application.Abstractions;

namespace UniversalLIMS.Infrastructure.Services;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
