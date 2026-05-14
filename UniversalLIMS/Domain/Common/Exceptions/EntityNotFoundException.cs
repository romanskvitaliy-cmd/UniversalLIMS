namespace UniversalLIMS.Domain.Common.Exceptions;

/// <summary>
/// Raised when a required aggregate or entity cannot be resolved by identifier.
/// </summary>
public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, Guid entityId)
        : base($"{entityName} with identifier '{entityId}' was not found.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }

    public string EntityName { get; }

    public Guid EntityId { get; }
}
