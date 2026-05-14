namespace UniversalLIMS.Application.Common;

/// <summary>
/// Paginated read-model result for list endpoints and journal screens.
/// </summary>
/// <typeparam name="T">Item type projected for the current page.</typeparam>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;
}
