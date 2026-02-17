using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloverReader.Tests;

[TestClass]
public class CloverCacheTests
{
    private static List<CloverSummaryEntry> MakeEntries(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new CloverSummaryEntry { Index = i, Month = $"M{i}" })
            .ToList();

    [TestMethod]
    public void TryGet_ReturnsFalse_WhenNotCached()
    {
        var cache = new CloverCache();

        bool result = cache.TryGet(6, out var data);

        Assert.IsFalse(result);
        Assert.AreEqual(0, data.Count);
    }

    [TestMethod]
    public void TryGet_ReturnsTrue_WhenCachedAndFresh()
    {
        var cache = new CloverCache();
        var entries = MakeEntries(6);
        cache.Set(6, entries);

        bool result = cache.TryGet(6, out var data);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(entries, data);
    }

    [TestMethod]
    public void TryGet_ReturnsFalse_WhenEntryExpired()
    {
        var cache = new CloverCache(ttl: TimeSpan.Zero);
        cache.Set(6, MakeEntries(6));

        bool result = cache.TryGet(6, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Set_OverwritesExistingEntry()
    {
        var cache = new CloverCache();
        cache.Set(6, MakeEntries(6));

        var updated = MakeEntries(3);
        cache.Set(6, updated);
        cache.TryGet(6, out var data);

        CollectionAssert.AreEqual(updated, data);
    }

    [DataTestMethod]
    [DataRow(12, new[] { 1, 3, 6 })]
    [DataRow(6,  new[] { 1, 3 })]
    [DataRow(3,  new[] { 1 })]
    [DataRow(1,  new int[] { })]
    public void InvalidateSmallerThan_RemovesCorrectSlabs(int months, int[] shouldBeInvalidated)
    {
        var cache = new CloverCache();
        foreach (int slab in CloverService.ValidSlabs)
            cache.Set(slab, MakeEntries(slab));

        cache.InvalidateSmallerThan(months);

        foreach (int slab in shouldBeInvalidated)
            Assert.IsFalse(cache.TryGet(slab, out _), $"Slab {slab} should have been invalidated");
    }

    [DataTestMethod]
    [DataRow(12)]
    [DataRow(6)]
    [DataRow(3)]
    [DataRow(1)]
    public void InvalidateSmallerThan_DoesNotRemoveTargetSlab(int months)
    {
        var cache = new CloverCache();
        foreach (int slab in CloverService.ValidSlabs)
            cache.Set(slab, MakeEntries(slab));

        cache.InvalidateSmallerThan(months);

        Assert.IsTrue(cache.TryGet(months, out _), $"Slab {months} should NOT be invalidated");
    }
}
