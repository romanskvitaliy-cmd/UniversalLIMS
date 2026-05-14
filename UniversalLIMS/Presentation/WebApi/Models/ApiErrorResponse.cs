namespace UniversalLIMS.Presentation.WebApi.Models;

/// <summary>
/// Standard API error payload for predictable domain failures.
/// </summary>
public sealed class ApiErrorResponse
{
    public ApiErrorResponse(string message)
    {
        Message = message;
    }

    /// <summary>Human-readable error description.</summary>
    public string Message { get; }
}
