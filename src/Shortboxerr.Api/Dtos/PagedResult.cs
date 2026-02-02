namespace Shortboxerr.Api.Dtos;

/// <summary>
/// Paginated result container (Arr-like pattern).
/// </summary>
public record PagedResult<T>
{
    public required IReadOnlyList<T> Records { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> records, int page, int pageSize, int totalRecords) => new()
    {
        Records = records,
        Page = page,
        PageSize = pageSize,
        TotalRecords = totalRecords
    };
}

