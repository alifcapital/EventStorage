namespace EventStorage.Management.Models;

/// <summary>
/// Represents a paginated list of events, providing pagination details such as the current page index, page size and
/// whether there is a next page. Inherits from <see cref="List{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The type of elements in the paginated list.</typeparam>
public class EventPagedList<TItem> : List<TItem>
{
    /// <summary>
    /// Index of the current page, starting from 1.
    /// </summary>
    public int PageIndex { get; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Indicates whether there is a next page.
    /// </summary>
    public bool HasNextPage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventPagedList{TItem}"/> class. The items may contain one extra item
    /// after the page to identify whether there is a next page, and that item is not added to the list.
    /// </summary>
    /// <param name="items">The collection of items.</param>
    /// <param name="pageIndex">The index of the current page.</param>
    /// <param name="pageSize">The number of items per page.</param>
    internal EventPagedList(ICollection<TItem> items, int pageIndex, int pageSize)
        : this(items, pageIndex, pageSize, hasNextPage: items.Count > pageSize)
    {
    }

    /// <summary>
    /// Initializes a paged list with an already calculated next-page indicator.
    /// </summary>
    /// <param name="items">The collection of items.</param>
    /// <param name="pageIndex">The index of the current page.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="hasNextPage">Indicates whether the source contains another page.</param>
    internal EventPagedList(ICollection<TItem> items, int pageIndex, int pageSize, bool hasNextPage)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        HasNextPage = hasNextPage;

        if (items.Count > pageSize)
            AddRange(items.Take(pageSize));
        else
            AddRange(items);
    }
}
