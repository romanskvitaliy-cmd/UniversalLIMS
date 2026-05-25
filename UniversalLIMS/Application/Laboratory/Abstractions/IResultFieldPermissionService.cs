namespace UniversalLIMS.Application.Laboratory.Abstractions;

public interface IResultFieldPermissionService
{
    Task<bool> CanWriteAsync(Guid sampleId, Guid dataFieldId, CancellationToken cancellationToken = default);
}
