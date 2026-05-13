namespace UniversalLIMS.Application.Abstractions;

public interface ISystemOperationContext
{
    bool IsActive { get; }

    string? OperationName { get; }

    IDisposable Begin(string operationName);
}
