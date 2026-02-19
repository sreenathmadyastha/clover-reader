using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CloverReader.Tests;

[TestClass]
public class CloverServiceTests
{
    // Fixed reference date so date-window assertions are deterministic.
    // N past months + current month (Feb 26) = N+1 entries:
    //   1m  → Jan 26, Feb 26
    //   3m  → Nov 25, Dec 25, Jan 26, Feb 26
    //   6m  → Aug 25, Sep 25, Oct 25, Nov 25, Dec 25, Jan 26, Feb 26
    //   12m → Feb 25, Mar 25 ... Jan 26, Feb 26
    private static readonly DateTime RefDate = new(2026, 2, 17);

    /// <summary>
    /// Creates N entries covering the N past months before RefDate (oldest first).
    /// Matches exactly what FilterToWindow returns for the same N and RefDate.
    /// </summary>
    private static List<CloverSummaryEntry> MakeEntries(int count)
    {
        DateTime startOfMonth = new(RefDate.Year, RefDate.Month, 1);
        return Enumerable.Range(1, count)
            .Select(i => new CloverSummaryEntry
            {
                Index = i,
                Month = startOfMonth.AddMonths(-(count - i + 1))
                                    .ToString("MMM yy", CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    private static (CloverService service, Mock<ICloverDataSource> mock, CloverCache cache)
        Build(List<CloverSummaryEntry>? fetchResult = null, int fetchMonths = 6)
    {
        var mock = new Mock<ICloverDataSource>();
        if (fetchResult is not null)
            mock.Setup(ds => ds.FetchAsync(fetchMonths)).ReturnsAsync(fetchResult);

        var cache = new CloverCache();
        var service = new CloverService(mock.Object, cache, getToday: () => RefDate);
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

    // --- Derive from larger slab, always exactly N past months ---

    [TestMethod]
    public async Task GetDataAsync_Derives6Months_From12MonthCache()
    {
        // 12-month cache: Feb 25 → Jan 26 (12 entries)
        // 6-month window: Aug 25 → Feb 26 (7 entries, 6 past + current)
        var (service, mock, cache) = Build();
        cache.Set(12, MakeEntries(12));

        var result = await service.GetDataAsync(6);

        Assert.AreEqual(7, result.Count);
        Assert.AreEqual("Aug 25", result.First().Month);
        Assert.AreEqual("Feb 26", result.Last().Month);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives3Months_From6MonthCache()
    {
        // 6-month cache: Aug 25 → Jan 26 (6 entries)
        // 3-month window: Nov 25 → Feb 26 (4 entries, 3 past + current)
        var (service, mock, cache) = Build();
        cache.Set(6, MakeEntries(6));

        var result = await service.GetDataAsync(3);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("Nov 25", result.First().Month);
        Assert.AreEqual("Feb 26", result.Last().Month);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives3Months_From12MonthCache_When6NotCached()
    {
        var (service, mock, cache) = Build();
        cache.Set(12, MakeEntries(12));

        var result = await service.GetDataAsync(3);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("Nov 25", result.First().Month);
        Assert.AreEqual("Feb 26", result.Last().Month);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_Derives1Month_From3MonthCache()
    {
        // 3-month cache: Nov 25, Dec 25, Jan 26
        // 1-month window: Jan 26, Feb 26 (2 entries, 1 past + current)
        var (service, mock, cache) = Build();
        cache.Set(3, MakeEntries(3));

        var result = await service.GetDataAsync(1);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Jan 26", result[0].Month);
        Assert.AreEqual("Feb 26", result[1].Month);
        mock.Verify(ds => ds.FetchAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task GetDataAsync_PrefersSmallestSufficientSlab_WhenMultipleLargerSlabsCached()
    {
        var (service, mock, cache) = Build();
        cache.Set(6,  MakeEntries(6));
        cache.Set(12, MakeEntries(12));

        // 3-month window derived from 6-month slab (smallest sufficient)
        var result = await service.GetDataAsync(3);

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("Nov 25", result.First().Month);
        Assert.AreEqual("Feb 26", result.Last().Month);
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
        await service.GetDataAsync(6); // second call must hit cache

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
