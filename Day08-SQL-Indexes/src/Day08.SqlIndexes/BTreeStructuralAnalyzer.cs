namespace Day08.SqlIndexes;

/// <summary>
/// Architectural model of a PostgreSQL 8KB B-Tree Index Page.
/// Explains the exact layout of internal and leaf index pages on disk.
/// </summary>
public record BTreePageLayout(
    int PageSizeBytes,
    int PageHeaderSizeBytes,
    int LinpPointerSizeBytes,
    int MaxTuplesPerPage,
    string PageType,
    string Description);

public static class BTreeStructuralAnalyzer
{
    public const int StandardPageSize = 8192;     // 8 KB
    public const int PageHeaderSize = 24;          // PageHeaderData (pd_lsn, pd_checksum, pd_flags, pd_lower, pd_upper, pd_special)
    public const int LinpPointerSize = 4;          // ItemIdData (lp_off, lp_flags, lp_len)
    public const int SpecialSpaceSize = 16;        // BTPageOpaqueData (btpo_prev, btpo_next, btpo_level, btpo_flags)

    public static BTreePageLayout AnalyzePageCapacity(int averageIndexKeySizeBytes)
    {
        int availableSpace = StandardPageSize - PageHeaderSize - SpecialSpaceSize;
        int bytesPerItem = LinpPointerSize + averageIndexKeySizeBytes;
        int maxTuples = availableSpace / bytesPerItem;

        return new BTreePageLayout(
            StandardPageSize,
            PageHeaderSize,
            LinpPointerSize,
            maxTuples,
            "Leaf / Internal B-Tree Page",
            $"An 8KB page accommodates ~{maxTuples} index keys averaging {averageIndexKeySizeBytes} bytes before requiring a B-Tree page split.");
    }
}
