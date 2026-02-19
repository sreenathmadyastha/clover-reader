namespace CloverReader;

public class CloverService
{
    // Supported slabs in ascending order
    public static readonly int[] ValidSlabs = [1, 3, 6, 12];

    private readonly ICloverDataSource _dataSource;
    private readonly CloverCache _cache;
    private readonly Func<DateTime> _getToday;

    public CloverService(ICloverDataSource dataSource, CloverCache cache,
        Func<DateTime>? getToday = null)
    {
        _dataSource = dataSource;
        _cache = cache;
        _getToday = getToday ?? (() => DateTime.Today);
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

        // 2. Derive from the smallest valid larger cached slab using the date window
        DateTime today = _getToday();
        foreach (int larger in ValidSlabs.Where(s => s > months))
        {
            if (_cache.TryGet(larger, out List<CloverSummaryEntry> superSet))
            {
                Console.WriteLine($"[Cache] Derived {months}-month data from cached {larger}-month data.");
                return MonthFilter.FilterToWindow(superSet, months, today);
            }
        }

        // 3. Fetch fresh from external, cache it, invalidate smaller stale slabs
        List<CloverSummaryEntry> fresh = await _dataSource.FetchAsync(months);
        _cache.Set(months, fresh);
        _cache.InvalidateSmallerThan(months);
        return fresh;
    }
}
