using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CloverReader.Tests;

[TestClass]
public class CloverServiceTests
{
    private static List<CloverSummaryEntry> MakeEntries(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new CloverSummaryEntry { Index = i, Month = $"M{i}" })
            .ToList();

    private static (CloverService service, Mock<ICloverDataSource> mock, CloverCache cache)
        Build(List<CloverSummaryEntry>? fetchResult = null, int fetchMonths = 6)
    {
        var mock = new Mock<ICloverDataSource>();
        if (fetchResult is not null)
            mock.Setup(ds => ds.FetchAsync(fetchMonths)).ReturnsAsync(fetchResult);

        var cache = new CloverCache();
        var service = new CloverService(mock.Object, cache);
        return (service, mock, cache);
    }

    // --- Validation ---

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(5)]
    [DataRow(7)]
    [DataRow(13)]
    public async Task GetDataAsync_ThrowsArgumentException_ForInvalidSlab(int months)
    {
        var (service, _, _) = Build();

        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.GetDataAsync(months));
    }

    // --- Exact cache hit ---

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    [DataRow(12)]
    public async Task GetDataAsync_ReturnsCachedData_OnExactHit(int months)
    {
        var (service, mock, cache) = Build();
        var expected = MakeEntries(months);
        cache.Set(months, expected);

        var result = await service.GetDataAsync(months);

        CollectionAssert.AreEqual(expected, result);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    // --- Derive from larger slab ---

    [TestMethod]
    public async Task GetDataAsync_Derives6Months_From12MonthCache()
    {
        var (service, mock, cache) = Build();
        cache.Set(12, MakeEntries(12));

        var result = await service.GetDataAsync(6);

        Assert.AreEqual(6, result.Count);
        CollectionAssert.AreEqual(new[] { 7, 8, 9, 10, 11, 12 }, result.Select(e => e.Index).ToArray());
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives3Months_From6MonthCache()
    {
        var (service, mock, cache) = Build();
        cache.Set(6, MakeEntries(6));

        var result = await service.GetDataAsync(3);

        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, result.Select(e => e.Index).ToArray());
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives3Months_From12MonthCache_When6NotCached()
    {
        var (service, mock, cache) = Build();
        cache.Set(12, MakeEntries(12));

        var result = await service.GetDataAsync(3);

        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(new[] { 10, 11, 12 }, result.Select(e => e.Index).ToArray());
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives1Month_From3MonthCache()
    {
        var (service, mock, cache) = Build();
        cache.Set(3, MakeEntries(3));

        var result = await service.GetDataAsync(1);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].Index);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_PrefersSmallestSufficientSlab_WhenMultipleLargerSlabsCached()
    {
        var (service, mock, cache) = Build();
        cache.Set(6, MakeEntries(6));
        cache.Set(12, MakeEntries(12));

        var result = await service.GetDataAsync(3);

        // Should derive from 6 (smallest sufficient), not 12
        CollectionAssert.AreEqual(new[] { 4, 5, 6 }, result.Select(e => e.Index).ToArray());
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    // --- External fetch ---

    [TestMethod]
    public async Task GetDataAsync_FetchesFromExternal_WhenNoCacheAvailable()
    {
        var expected = MakeEntries(6);
        var (service, mock, _) = Build(fetchResult: expected, fetchMonths: 6);

        var result = await service.GetDataAsync(6);

        CollectionAssert.AreEqual(expected, result);
        mock.Verify(ds => ds.FetchAsync(6), Times.Once);
    }

    [TestMethod]
    public async Task GetDataAsync_CachesResult_AfterExternalFetch()
    {
        var (service, mock, _) = Build(fetchResult: MakeEntries(6), fetchMonths: 6);

        await service.GetDataAsync(6);
        await service.GetDataAsync(6); // second call should hit cache

        mock.Verify(ds => ds.FetchAsync(6), Times.Once);
    }

    // --- Invalidation on fresh fetch ---

    [TestMethod]
    public async Task GetDataAsync_InvalidatesSmallerSlabs_WhenFetching12Months()
    {
        var (service, _, cache) = Build(fetchResult: MakeEntries(12), fetchMonths: 12);
        cache.Set(6, MakeEntries(6));
        cache.Set(3, MakeEntries(3));
        cache.Set(1, MakeEntries(1));

        await service.GetDataAsync(12);

        Assert.IsFalse(cache.TryGet(6, out _), "6-month cache should be invalidated");
        Assert.IsFalse(cache.TryGet(3, out _), "3-month cache should be invalidated");
        Assert.IsFalse(cache.TryGet(1, out _), "1-month cache should be invalidated");
    }

    [TestMethod]
    public async Task GetDataAsync_InvalidatesSmallerSlabs_WhenFetching6Months()
    {
        var (service, _, cache) = Build(fetchResult: MakeEntries(6), fetchMonths: 6);
        cache.Set(3, MakeEntries(3));
        cache.Set(1, MakeEntries(1));

        await service.GetDataAsync(6);

        Assert.IsFalse(cache.TryGet(3, out _), "3-month cache should be invalidated");
        Assert.IsFalse(cache.TryGet(1, out _), "1-month cache should be invalidated");
    }
}
