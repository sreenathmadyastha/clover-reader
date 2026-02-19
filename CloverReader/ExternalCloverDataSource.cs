using System.Text.Json;

namespace CloverReader;

/// <summary>
/// Simulates an external data source by reading from the local clover.json.
/// In production this would call an HTTP API or similar external service.
/// </summary>
public class ExternalCloverDataSource : ICloverDataSource
{
    private readonly string _jsonPath;

    public ExternalCloverDataSource(string jsonPath)
    {
        _jsonPath = jsonPath;
    }

    public async Task<List<CloverSummaryEntry>> FetchAsync(int months)
    {
        Console.WriteLine($"[External] Fetching {months}-month data from external system...");

        // Simulate async I/O (e.g. HTTP call to external API)
        string json = await File.ReadAllTextAsync(_jsonPath);
        Root? root = JsonSerializer.Deserialize<Root>(json);

        List<CloverSummaryEntry> all = root?.Data?.CloverSummary ?? [];

        // Return entries within the requested date window from today
        return MonthFilter.FilterToWindow(all, months);
    }
}
