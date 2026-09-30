using EventStorage.Management.Models;

namespace EventStorage.Extensions;

internal static class PagedListExtensions
{
    /// <summary>
    /// Converts the collection to the paged list. The collection may contain one extra item after the page to
    /// identify whether there is a next page.
    /// </summary>
    /// <param name="items">The collection of items.</param>
    /// <param name="pageIndex">The index of the current page.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <typeparam name="TItem">The element type of collection.</typeparam>
    /// <returns>Newly created paged list based on provided collection.</returns>
    internal static EventPagedList<TItem> ToPagedList<TItem>(this ICollection<TItem> items, int pageIndex, int pageSize)
    {
        return new EventPagedList<TItem>(items, pageIndex, pageSize);
    }

    /// <summary>
    /// Converts the collection to the paged list while preserving an already calculated next-page indicator.
    /// </summary>
    /// <param name="items">The collection of items.</param>
    /// <param name="pageIndex">The index of the current page.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="hasNextPage">Indicates whether the source contains another page.</param>
    /// <typeparam name="TItem">The element type of collection.</typeparam>
    /// <returns>Newly created paged list based on provided collection and pagination information.</returns>
    internal static EventPagedList<TItem> ToPagedList<TItem>(this ICollection<TItem> items, int pageIndex, int pageSize,
        bool hasNextPage)
    {
        return new EventPagedList<TItem>(items, pageIndex, pageSize, hasNextPage);
    }

    /// <summary>
    /// Projects the items of the paged list while preserving its pagination information.
    /// </summary>
    /// <param name="eventPagedList">The source paged list.</param>
    /// <param name="selector">The projection of each item.</param>
    /// <typeparam name="TSource">The element type of the source list.</typeparam>
    /// <typeparam name="TResult">The element type of the result list.</typeparam>
    /// <returns>Newly created paged list with the projected items.</returns>
    internal static EventPagedList<TResult> MapItems<TSource, TResult>(this EventPagedList<TSource> eventPagedList,
        Func<TSource, TResult> selector)
    {
        var items = eventPagedList.Select(selector).ToArray();
        return items.ToPagedList(eventPagedList.PageIndex, eventPagedList.PageSize, eventPagedList.HasNextPage);
    }
}
