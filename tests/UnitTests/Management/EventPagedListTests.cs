using EventStorage.Extensions;

namespace EventStorage.Tests.UnitTests.Management;

internal class EventPagedListTests
{
    #region ToPagedList

    [Test]
    public void ToPagedList_ItemsHaveOneExtraItem_ShouldTakePageSizeItemsAndHaveNextPage()
    {
        int[] items = [1, 2, 3];

        var result = items.ToPagedList(pageIndex: 2, pageSize: 2);

        Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(result.PageIndex, Is.EqualTo(2));
        Assert.That(result.PageSize, Is.EqualTo(2));
        Assert.That(result.HasNextPage, Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ToPagedList_ItemsDoNotExceedPageSize_ShouldTakeAllItemsWithoutNextPage(int count)
    {
        var items = Enumerable.Range(1, count).ToArray();

        var result = items.ToPagedList(pageIndex: 1, pageSize: 2);

        Assert.That(result, Is.EqualTo(items));
        Assert.That(result.HasNextPage, Is.False);
    }

    [Test]
    public void ToPagedList_NextPageIndicatorIsPassed_ShouldKeepIt()
    {
        int[] items = [1, 2];

        var result = items.ToPagedList(pageIndex: 1, pageSize: 2, hasNextPage: true);

        Assert.That(result, Is.EqualTo(items));
        Assert.That(result.HasNextPage, Is.True);
    }

    #endregion

    #region MapItems

    [Test]
    public void MapItems_PagedListHasNextPage_ShouldMapItemsAndKeepPagination()
    {
        var pagedList = new[] { 1, 2, 3 }.ToPagedList(pageIndex: 3, pageSize: 2);

        var result = pagedList.MapItems(i => $"item {i}");

        Assert.That(result, Is.EqualTo(new[] { "item 1", "item 2" }));
        Assert.That(result.PageIndex, Is.EqualTo(3));
        Assert.That(result.PageSize, Is.EqualTo(2));
        Assert.That(result.HasNextPage, Is.True);
    }

    #endregion
}
