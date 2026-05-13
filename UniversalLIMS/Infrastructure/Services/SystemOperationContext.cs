using UniversalLIMS.Application.Abstractions;

namespace UniversalLIMS.Infrastructure.Services;

public sealed class SystemOperationContext : ISystemOperationContext
{
    private string? _operationName;

    public bool IsActive => !string.IsNullOrWhiteSpace(_operationName);

    public string? OperationName => _operationName;

    public IDisposable Begin(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var previousOperationName = _operationName;
        _operationName = operationName;

        return new RestoreOperationScope(this, previousOperationName);
    }

    private sealed class RestoreOperationScope : IDisposable
    {
        private readonly SystemOperationContext _context;
        private readonly string? _previousOperationName;
        private bool _disposed;

        public RestoreOperationScope(SystemOperationContext context, string? previousOperationName)
        {
            _context = context;
            _previousOperationName = previousOperationName;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _context._operationName = _previousOperationName;
            _disposed = true;
        }
    }
}
