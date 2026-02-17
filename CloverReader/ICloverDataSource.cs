namespace CloverReader;

public interface ICloverDataSource
{
    /// <summary>Fetches the latest <paramref name="months"/> months of data from the external system.</summary>
    Task<List<CloverSummaryEntry>> FetchAsync(int months);
}
