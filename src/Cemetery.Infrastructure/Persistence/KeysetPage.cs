using Cemetery.Application.Abstractions;

namespace Cemetery.Infrastructure.Persistence;

internal static class KeysetPage
{
    public static Page<T> Pack<T>(List<T> rows, int size, Func<T, string> sort, Func<T, Guid> id)
    {
        if (rows.Count <= size)
            return new Page<T>(rows, null);

        var items = rows.Take(size).ToArray();
        var last = items[^1];
        return new Page<T>(items, KeysetCursor.Encode(sort(last), id(last)));
    }
}
