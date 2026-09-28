# Day 08 - Engineering Notes: SQL B-Tree Indexes & Page Internals

## 1. Physical Layout of an 8KB PostgreSQL B-Tree Page

Every PostgreSQL table and index page is exactly **8192 bytes** (8 KB):
```text
+--------------------------------------------------------------------+
| PageHeaderData (24 bytes)                                          |
| pd_lsn (8B), pd_checksum (2B), pd_flags (2B), pd_lower (2B),      |
| pd_upper (2B), pd_special (2B), pd_pagesize_version (2B)          |
+--------------------------------------------------------------------+
| ItemIdData Array (Linp pointers, 4 bytes each)                     |
| lp_off (15 bits), lp_flags (2 bits), lp_len (15 bits)              |
| [Linp 1] -> offset to Item 1                                       |
| [Linp 2] -> offset to Item 2   (Grows downward)                    |
+--------------------------------------------------------------------+
|                          < FREE SPACE >                            |
+--------------------------------------------------------------------+
| Index Tuple Data (Grows upward)                                    |
| [Item 2: Key value + ctid (Block 45, Offset 1)]                    |
| [Item 1: Key value + ctid (Block 12, Offset 3)]                    |
+--------------------------------------------------------------------+
| BTPageOpaqueData (Special Space at page end, 16 bytes)            |
| btpo_prev (left sibling), btpo_next (right sibling),               |
| btpo_level (0 = leaf), btpo_flags (BTP_LEAF, BTP_ROOT, etc.)      |
+--------------------------------------------------------------------+
```

---

## 2. PostgreSQL vs SQL Server Index Architecture

| Feature | PostgreSQL | SQL Server |
|---|---|---|
| **Base Table Structure** | Always a Heap (unordered rows referenced by `ctid`) | Heap OR Clustered Index (data stored in index leaf pages) |
| **Non-Clustered Leaf** | Stores `ctid` (physical block + offset pointer) | Stores Clustering Key (if clustered) or RID (if heap) |
| **Secondary Lookup Overhead** | Index Scan fetches table heap page via `ctid` | "Key Lookup" traverses clustered index from root to leaf |
| **Covering Indexes** | `CREATE INDEX ... INCLUDE (...)` avoids heap fetches | `CREATE INDEX ... INCLUDE (...)` avoids Key Lookups |
| **Update Mechanism** | HOT (Heap-Only Tuples) if unindexed columns change | In-place update if row length unchanged |
| **Page Size** | 8 KB (8192 bytes) | 8 KB (8192 bytes) |

---

## 3. The 4 Query Plan Scan Types

1. **Sequential Scan (`Seq Scan`)**:
   - Reads every 8KB heap page from disk.
   - Selected when table is small or selectivity is low (query returns $>10–20\%$ of table).
2. **Index Scan**:
   - Traverses B-tree from root to leaf, retrieves matching `ctid`, and immediately reads corresponding table heap page.
   - Best for high selectivity (few rows returned).
3. **Bitmap Index Scan + Bitmap Heap Scan**:
   - B-tree scan populates an in-memory bitmap of qualifying page numbers.
   - Bitmap Heap Scan visits each heap page only once in physical disk order, converting random I/O into sequential I/O.
4. **Index-Only Scan**:
   - All requested columns exist in the index (via key columns or `INCLUDE`).
   - If Visibility Map confirms all tuples on the page are visible, **zero heap pages are read** (`Heap Fetches: 0`).

---

## 4. DSA Takeaways
- **Longest Substring Without Repeating Characters**:
  - Sliding window $[windowStart, i]$.
  - An integer array of size 256 stores the 1-based index where each character was last seen.
  - When character $c$ is repeated, $windowStart = \max(windowStart, lastSeen[c])$.
  - Single pass $O(n)$ time with $O(1)$ space.
