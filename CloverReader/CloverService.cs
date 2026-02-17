namespace CloverReader;

public class CloverService
{
    // Supported slabs in ascending order
    public static readonly int[] ValidSlabs = [1, 3, 6, 12];

    private readonly ICloverDataSource _dataSource;
    private readonly CloverCache _cache;

    public CloverService(ICloverDataSource dataSource, CloverCache cache)
    {
        _dataSource = dataSource;
        _cache = cache;
    }

    public async Task<List<CloverSummaryEntry>> GetDataAsync(int months)
    {
        if (!ValidSlabs.Contains(months))
            throw new ArgumentException($"Invalid slab. Supported values: {string.Join(", ", ValidSlabs)}.", nameof(months));

        // 1. Exact cache hit
        if (_cache.TryGet(months, out List<CloverSummaryEntry> cached))
        {
            Console.WriteLine($"[Cache] Hit for {months}-month data.");
            return cached;
        }

        // 2. Derive from the smallest valid larger cached slab
        foreach (int larger in ValidSlabs.Where(s => s > months))
        {
            if (_cache.TryGet(larger, out List<CloverSummaryEntry> superSet))
            {
                Console.WriteLine($"[Cache] Derived {months}-month data from cached {larger}-month data.");
                return superSet.OrderBy(e => e.Index).TakeLast(months).ToList();
            }
        }

        // 3. Fetch fresh from external, cache it, invalidate smaller stale slabs
        List<CloverSummaryEntry> fresh = await _dataSource.FetchAsync(months);
        _cache.Set(months, fresh);
        _cache.InvalidateSmallerThan(months);
        return fresh;
    }
}
