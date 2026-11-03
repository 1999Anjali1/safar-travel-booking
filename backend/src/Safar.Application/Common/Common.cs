namespace Safar.Application.Common;

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

/// <summary>Request conflicts with current state (e.g. email already registered).</summary>
public class ConflictException(string message) : Exception(message);

public class UnauthorizedException(string message) : Exception(message);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class QueryExtensions
{
    public static IQueryable<T> Page<T>(this IQueryable<T> query, int page, int pageSize) =>
        query.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize);
}
