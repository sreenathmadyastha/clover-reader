namespace CloverReader;

public class CloverCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private record CacheEntry(List<CloverSummaryEntry> Data, DateTime CachedAt);

    private readonly Dictionary<int, CacheEntry> _store = new();

    public bool TryGet(int months, out List<CloverSummaryEntry> data)
    {
        if (_store.TryGetValue(months, out CacheEntry? entry) &&
            DateTime.UtcNow - entry.CachedAt < Ttl)
        {
            data = entry.Data;
            return true;
        }

        data = [];
        return false;
    }

    public void Set(int months, List<CloverSummaryEntry> data)
    {
        _store[months] = new CacheEntry(data, DateTime.UtcNow);
    }

    /// <summary>Removes all cached slabs smaller than <paramref name="months"/>.</summary>
    public void InvalidateSmallerThan(int months)
    {
        foreach (int slab in CloverService.ValidSlabs.Where(s => s < months))
        {
            _store.Remove(slab);
            Console.WriteLine($"[Cache] Invalidated {slab}-month cache.");
        }
    }
}
