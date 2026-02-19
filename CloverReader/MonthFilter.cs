using System.Globalization;

namespace CloverReader;

public static class MonthFilter
{
    /// <summary>
    /// Returns <paramref name="months"/> complete past calendar months plus the current month
    /// (total = months + 1 entries).
    ///
    /// Example: referenceDate = Feb 17 2026, months = 6
    ///   → Aug 25, Sep 25, Oct 25, Nov 25, Dec 25, Jan 26, Feb 26  (oldest → newest)
    ///
    /// If a month is absent from <paramref name="entries"/>, a zero-value entry is returned
    /// for that month so the result always contains exactly N items.
    /// </summary>
    public static List<CloverSummaryEntry> FilterToWindow(
        List<CloverSummaryEntry> entries,
        int months,
        DateTime? referenceDate = null)
    {
        DateTime today = referenceDate ?? DateTime.Today;
        DateTime startOfCurrentMonth = new(today.Year, today.Month, 1, 0, 0, 0, today.Kind);

        var result = new List<CloverSummaryEntry>(months + 1);

        // Build a lookup of parsed entries keyed by year+month for O(1) access
        var lookup = entries
            .Where(e => TryParseMonth(e.Month, out _))
            .ToDictionary(e => { TryParseMonth(e.Month, out DateTime d); return (d.Year, d.Month); });

        int index = 1;
        for (int i = months; i >= 0; i--)
        {
            DateTime target = startOfCurrentMonth.AddMonths(-i);
            string label = target.ToString("MMM yy", CultureInfo.InvariantCulture);

            if (lookup.TryGetValue((target.Year, target.Month), out CloverSummaryEntry? match))
            {
                match.Index = index++;
                result.Add(match);
            }
            else
            {
                result.Add(new CloverSummaryEntry
                {
                    Index = index++,
                    Month = label,
                    Summary = new Summary()   // SettledTransactionsTotal = 0, AuthorizedTransactionsTotal = 0
                });
            }
        }

        return result;
    }

    public static bool TryParseMonth(string month, out DateTime date) =>
        DateTime.TryParseExact(month, "MMM yy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
