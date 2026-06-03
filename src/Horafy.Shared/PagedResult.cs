namespace Horafy.Shared;

/// <summary>
/// Resultado paginado cursor-based para queries de alta performance.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => PageNumber * PageSize < TotalCount;
    public bool HasPreviousPage => PageNumber > 1;

    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 20) =>
        Create([], 0, pageNumber, pageSize);
}

/// <summary>
/// Parâmetros de paginação base para queries.
/// </summary>
public record PagedRequest
{
    private int _pageSize = 20;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value switch
        {
            < 1 => 1,
            > 100 => 100,
            _ => value
        };
    }

    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
}
