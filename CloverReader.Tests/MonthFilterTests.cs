using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloverReader.Tests;

[TestClass]
public class MonthFilterTests
{
    // Fixed reference: Feb 17, 2026
    // N past months + current month (Feb 26) = N+1 entries:
    //   1m  → Jan 26, Feb 26
    //   3m  → Nov 25, Dec 25, Jan 26, Feb 26
    //   6m  → Aug 25, Sep 25, Oct 25, Nov 25, Dec 25, Jan 26, Feb 26
    //   12m → Feb 25, Mar 25, Apr 25, May 25, Jun 25, Jul 25,
    //          Aug 25, Sep 25, Oct 25, Nov 25, Dec 25, Jan 26, Feb 26
    private static readonly DateTime RefDate = new(2026, 2, 17);

    private static CloverSummaryEntry Entry(int index, string month,
        int settled = 100, int authorized = 50) =>
        new()
        {
            Index = index,
            Month = month,
            Summary = new Summary
            {
                SettledTransactionsTotal = settled,
                AuthorizedTransactionsTotal = authorized
            }
        };

    // All entries from the JSON (Feb 25 → Feb 26)
    private static readonly List<CloverSummaryEntry> AllEntries =
    [
        Entry(1,  "Feb 25"),
        Entry(2,  "Mar 25"),
        Entry(3,  "Apr 25"),
        Entry(4,  "May 25"),
        Entry(5,  "Jun 25"),
        Entry(6,  "Jul 25"),
        Entry(7,  "Aug 25"),
        Entry(8,  "Sep 25"),
        Entry(9,  "Oct 25"),
        Entry(10, "Nov 25"),
        Entry(11, "Dec 25"),
        Entry(12, "Jan 26"),
        Entry(13, "Feb 26"),   // current month — included
    ];

    // --- Count is always exactly N+1 (N past months + current month) ---

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    [DataRow(12)]
    public void FilterToWindow_AlwaysReturnsNPlusOneEntries(int months)
    {
        var result = MonthFilter.FilterToWindow(AllEntries, months, RefDate);

        Assert.AreEqual(months + 1, result.Count);
    }

    // --- Current month is included ---

    [TestMethod]
    public void FilterToWindow_IncludesCurrentMonth()
    {
        var result = MonthFilter.FilterToWindow(AllEntries, 6, RefDate);

        Assert.IsTrue(result.Any(e => e.Month == "Feb 26"), "Current month (Feb 26) must be included");
    }

    // --- Correct month ranges ---

    [TestMethod]
    public void FilterToWindow_1Month_ReturnsJan26AndFeb26()
    {
        var result = MonthFilter.FilterToWindow(AllEntries, 1, RefDate);

        Assert.AreEqual("Jan 26", result[0].Month);
        Assert.AreEqual("Feb 26", result[1].Month);
    }

    [TestMethod]
    public void FilterToWindow_3Months_ReturnsNov25ToFeb26()
    {
        var result = MonthFilter.FilterToWindow(AllEntries, 3, RefDate);

        Assert.AreEqual("Nov 25", result[0].Month);
        Assert.AreEqual("Dec 25", result[1].Month);
        Assert.AreEqual("Jan 26", result[2].Month);
        Assert.AreEqual("Feb 26", result[3].Month);
    }

    [TestMethod]
    public void FilterToWindow_6Months_ReturnsAug25ToFeb26()
    {
        var result = MonthFilter.FilterToWindow(AllEntries, 6, RefDate);

        Assert.AreEqual("Aug 25", result[0].Month);
        Assert.AreEqual("Feb 26", result[6].Month);
    }

    [TestMethod]
    public void FilterToWindow_12Months_ReturnsFeb25ToFeb26()
    {
        var result = MonthFilter.FilterToWindow(AllEntries, 12, RefDate);

        Assert.AreEqual("Feb 25", result[0].Month);
        Assert.AreEqual("Feb 26", result[12].Month);
    }

    // --- Index always starts at 1 ---

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(6)]
    [DataRow(12)]
    public void FilterToWindow_IndexAlwaysStartsAt1(int months)
    {
        var result = MonthFilter.FilterToWindow(AllEntries, months, RefDate);

        Assert.AreEqual(months + 1, result.Count);
        for (int i = 0; i < result.Count; i++)
            Assert.AreEqual(i + 1, result[i].Index, $"Entry at position {i} should have Index {i + 1}");
    }

    // --- Missing months filled with zeros ---

    [TestMethod]
    public void FilterToWindow_MissingMonth_InsertedWithZeroValues()
    {
        var sparse = AllEntries.Where(e => e.Month != "Oct 25").ToList();

        var result = MonthFilter.FilterToWindow(sparse, 6, RefDate);

        Assert.AreEqual(7, result.Count);
        CloverSummaryEntry oct = result.First(e => e.Month == "Oct 25");
        Assert.AreEqual(0, oct.Summary.SettledTransactionsTotal);
        Assert.AreEqual(0, oct.Summary.AuthorizedTransactionsTotal);
    }

    [TestMethod]
    public void FilterToWindow_EmptySource_ReturnsAllZeros()
    {
        var result = MonthFilter.FilterToWindow([], 3, RefDate);

        Assert.AreEqual(4, result.Count);
        Assert.IsTrue(result.All(e => e.Summary.SettledTransactionsTotal == 0));
        Assert.IsTrue(result.All(e => e.Summary.AuthorizedTransactionsTotal == 0));
    }

    // --- Ordering ---

    [TestMethod]
    public void FilterToWindow_ResultIsOrderedOldestToNewest()
    {
        var shuffled = AllEntries.OrderByDescending(e => e.Index).ToList();

        var result = MonthFilter.FilterToWindow(shuffled, 6, RefDate);

        var months = result.Select(e => e.Month).ToList();
        CollectionAssert.AreEqual(
            new[] { "Aug 25", "Sep 25", "Oct 25", "Nov 25", "Dec 25", "Jan 26", "Feb 26" },
            months);
    }

    // --- TryParseMonth ---

    [TestMethod]
    public void TryParseMonth_ReturnsTrueForValidFormat()
    {
        bool ok = MonthFilter.TryParseMonth("Jan 26", out DateTime date);

        Assert.IsTrue(ok);
        Assert.AreEqual(2026, date.Year);
        Assert.AreEqual(1, date.Month);
        Assert.AreEqual(1, date.Day);
    }

    [TestMethod]
    public void TryParseMonth_ReturnsFalseForInvalidFormat()
    {
        Assert.IsFalse(MonthFilter.TryParseMonth("January 2026", out _));
        Assert.IsFalse(MonthFilter.TryParseMonth("invalid", out _));
    }
}
