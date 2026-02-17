using System.Text.Json;
using CloverReader;

// Optional: pass months as a command-line argument, default to 6
// Valid slabs: 1, 3, 6, 12
int months = args.Length > 0 && int.TryParse(args[0], out int m) ? m : 6;

if (!CloverService.ValidSlabs.Contains(months))
{
    Console.Error.WriteLine($"Error: '{months}' is not a valid slab. Use one of: {string.Join(", ", CloverService.ValidSlabs)}");
    return 1;
}

string jsonPath = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data", "clover.json"));

if (!File.Exists(jsonPath))
{
    Console.Error.WriteLine($"Error: File not found at {jsonPath}");
    return 1;
}

var dataSource = new ExternalCloverDataSource(jsonPath);
var cache = new CloverCache();
var service = new CloverService(dataSource, cache);

List<CloverSummaryEntry> entries = await service.GetDataAsync(months);

if (entries.Count == 0)
{
    Console.Error.WriteLine("Error: No data returned.");
    return 1;
}

// Display
Console.WriteLine();
Console.WriteLine($"=== Clover Transaction Summary (last {months} months) ===");
Console.WriteLine();
Console.WriteLine($"{"#",-4} {"Month",-8} {"Settled":>10} {"Authorized":>12}");
Console.WriteLine(new string('-', 38));

int totalSettled = 0;
int totalAuthorized = 0;

foreach (CloverSummaryEntry entry in entries)
{
    int settled = entry.Summary.SettledTransactionsTotal;
    int authorized = entry.Summary.AuthorizedTransactionsTotal;

    totalSettled += settled;
    totalAuthorized += authorized;

    Console.WriteLine($"{entry.Index,-4} {entry.Month,-8} {settled,10} {authorized,12}");
}

Console.WriteLine(new string('-', 38));
Console.WriteLine($"{"TOTAL",-13} {totalSettled,10} {totalAuthorized,12}");
Console.WriteLine();
Console.WriteLine($"Grand Total Transactions: {totalSettled + totalAuthorized}");

// JSON output
Console.WriteLine();
Console.WriteLine("=== JSON Output ===");
Console.WriteLine();
string json = JsonSerializer.Serialize(
    new { data = new { cloverSummary = entries } },
    new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);

return 0;
