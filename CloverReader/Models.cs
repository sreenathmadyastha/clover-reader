using System.Text.Json.Serialization;

namespace CloverReader;

public class Root
{
    [JsonPropertyName("data")]
    public Data Data { get; set; } = new();
}

public class Data
{
    [JsonPropertyName("cloverSummary")]
    public List<CloverSummaryEntry> CloverSummary { get; set; } = new();
}

public class CloverSummaryEntry
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("month")]
    public string Month { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public Summary Summary { get; set; } = new();
}

public class Summary
{
    [JsonPropertyName("settledTransactionsTotal")]
    public int SettledTransactionsTotal { get; set; }

    [JsonPropertyName("authorizedTransactionsTota")]
    public int AuthorizedTransactionsTotal { get; set; }
}
