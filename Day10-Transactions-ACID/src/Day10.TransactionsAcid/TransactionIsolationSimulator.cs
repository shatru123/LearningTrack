namespace Day10.TransactionsAcid;

public enum SqlIsolationLevel
{
    ReadUncommitted,
    ReadCommitted,
    RepeatableRead,
    Serializable
}

public sealed class MvccTuple<T>
{
    public long TupleId { get; init; }
    public T Value { get; set; }
    public long Xmin { get; set; } // Tx that created this version
    public long? Xmax { get; set; } // Tx that deleted or updated this version (null if live)

    public MvccTuple(long tupleId, T value, long xmin)
    {
        TupleId = tupleId;
        Value = value;
        Xmin = xmin;
        Xmax = null;
    }
}

public sealed record MvccSnapshot(long SnapshotXmin, long SnapshotXmax, HashSet<long> ActiveXids);

public sealed record AnomalyResult(
    bool AnomalyObserved,
    string AnomalyName,
    SqlIsolationLevel IsolationLevel,
    string Summary);

public static class TransactionIsolationSimulator
{
    /// <summary>
    /// Checks if a tuple is visible to a snapshot under PostgreSQL MVCC rules.
    /// </summary>
    public static bool IsVisible(MvccTuple<int> tuple, MvccSnapshot snapshot, HashSet<long> committedXids)
    {
        // 1. If inserting transaction has not committed, it is invisible to other transactions
        if (!committedXids.Contains(tuple.Xmin))
        {
            return false;
        }

        // 2. If inserting transaction was active when snapshot was taken, it is invisible
        if (snapshot.ActiveXids.Contains(tuple.Xmin))
        {
            return false;
        }

        // 3. If inserting transaction started AFTER snapshot was taken, it is invisible
        if (tuple.Xmin >= snapshot.SnapshotXmax)
        {
            return false;
        }

        // 4. If tuple was deleted/updated by Xmax:
        if (tuple.Xmax.HasValue)
        {
            // If deleting transaction is committed AND was committed before or outside our snapshot:
            if (committedXids.Contains(tuple.Xmax.Value) &&
                !snapshot.ActiveXids.Contains(tuple.Xmax.Value) &&
                tuple.Xmax.Value < snapshot.SnapshotXmax)
            {
                return false; // Deleted and deletion is visible to this snapshot
            }
        }

        return true;
    }

    /// <summary>
    /// Simulates a Dirty Read anomaly: Tx1 modifies row but does NOT commit; Tx2 reads the row.
    /// </summary>
    public static AnomalyResult SimulateDirtyRead(SqlIsolationLevel level)
    {
        // Initial state: Row 1 = 100
        int committedValue = 100;
        int uncommittedValue = 999; // Tx1 writes this but does NOT commit

        int valueReadByTx2 = level == SqlIsolationLevel.ReadUncommitted
            ? uncommittedValue // Dirty read observed
            : committedValue;  // Read Committed blocks or reads previous MVCC version

        bool anomalyOccurred = valueReadByTx2 == uncommittedValue;
        return new AnomalyResult(
            AnomalyObserved: anomalyOccurred,
            AnomalyName: "DirtyRead",
            IsolationLevel: level,
            Summary: anomalyOccurred
                ? "Dirty Read observed: Tx2 read uncommitted value 999 from uncommitted Tx1."
                : "Dirty Read prevented: Tx2 read only committed value 100.");
    }

    /// <summary>
    /// Simulates a Non-Repeatable Read anomaly: Tx1 reads row; Tx2 modifies row and COMMITS; Tx1 reads row again.
    /// </summary>
    public static AnomalyResult SimulateNonRepeatableRead(SqlIsolationLevel level)
    {
        int initialVal = 100;
        int firstRead = initialVal;

        // Tx2 updates to 250 and COMMITS
        int updatedVal = 250;

        int secondRead = level switch
        {
            SqlIsolationLevel.ReadUncommitted or SqlIsolationLevel.ReadCommitted => updatedVal, // New snapshot per query
            _ => firstRead // RepeatableRead & Serializable maintain transaction snapshot
        };

        bool anomalyOccurred = firstRead != secondRead;
        return new AnomalyResult(
            AnomalyObserved: anomalyOccurred,
            AnomalyName: "NonRepeatableRead",
            IsolationLevel: level,
            Summary: anomalyOccurred
                ? $"Non-Repeatable Read observed: First read was {firstRead}, second read was {secondRead}."
                : $"Non-Repeatable Read prevented: Both reads returned identical value {firstRead}.");
    }

    /// <summary>
    /// Simulates a Phantom Read anomaly: Tx1 queries range; Tx2 inserts new matching row and COMMITS; Tx1 queries same range.
    /// In PostgreSQL, Repeatable Read prevents Phantom Reads because of transaction-level MVCC snapshots!
    /// </summary>
    public static AnomalyResult SimulatePhantomRead(SqlIsolationLevel level)
    {
        var initialList = new List<int> { 120, 150 }; // WHERE amount > 100 (Count = 2)
        int firstCount = initialList.Count;

        // Tx2 inserts 180 and commits
        int secondCount = level switch
        {
            SqlIsolationLevel.ReadUncommitted or SqlIsolationLevel.ReadCommitted => firstCount + 1, // 3 rows
            _ => firstCount // RepeatableRead & Serializable prevent phantoms in PostgreSQL MVCC
        };

        bool anomalyOccurred = firstCount != secondCount;
        return new AnomalyResult(
            AnomalyObserved: anomalyOccurred,
            AnomalyName: "PhantomRead",
            IsolationLevel: level,
            Summary: anomalyOccurred
                ? $"Phantom Read observed: Row count changed from {firstCount} to {secondCount} within the transaction."
                : $"Phantom Read prevented: Both range scans observed {firstCount} rows.");
    }

    /// <summary>
    /// Simulates Write Skew (Doctor on-call constraint: at least 1 must remain on-call).
    /// Dr. A and Dr. B are both on-call (Count = 2). Both request leave at the same time.
    /// Under Repeatable Read, both succeed (0 on call => invariant broken!).
    /// Under Serializable (SSI), rw-conflict aborts one transaction.
    /// </summary>
    public static AnomalyResult SimulateWriteSkew(SqlIsolationLevel level)
    {
        bool bothSucceeded = level != SqlIsolationLevel.Serializable;
        bool anomalyOccurred = bothSucceeded;

        return new AnomalyResult(
            AnomalyObserved: anomalyOccurred,
            AnomalyName: "SerializationAnomaly_WriteSkew",
            IsolationLevel: level,
            Summary: anomalyOccurred
                ? "Write Skew occurred: Under Repeatable Read, both doctors went off-call simultaneously (0 remaining)."
                : "Write Skew prevented: Under Serializable (SSI), rw-antidependency detected, one transaction aborted with 40001.");
    }
}
