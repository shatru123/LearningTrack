-- ============================================================================
-- Day 08: Safe Read-Only B-Tree Index Inspection with pageinspect
-- ============================================================================

-- Step 1: Create B-Tree Indexes on Tickets
CREATE INDEX IF NOT EXISTS idx_tickets_event_id ON Tickets (EventId);
CREATE INDEX IF NOT EXISTS idx_tickets_order_id ON Tickets (OrderId);

-- Step 2: Enable pageinspect (Requires superuser or rds_superuser in AWS RDS)
CREATE EXTENSION IF NOT EXISTS pageinspect;

-- ============================================================================
-- 1. Inspect B-Tree Meta Page (Page 0)
-- Displays root page block number, tree level/depth, fast root, and version.
-- ============================================================================
SELECT 
    magic,
    version,
    root,           -- Block number of the root page
    level,          -- Tree height (0 = root is also leaf)
    fastroot,       -- Optimized fast root block
    fastlevel
FROM bt_metap('idx_tickets_event_id');

-- ============================================================================
-- 2. Inspect B-Tree Page Statistics (Page 1 or Root Block)
-- Analyzes page utilization, live items, dead items, free space, and page type.
-- ============================================================================
SELECT 
    blkno,
    type,           -- 'l' = leaf, 'i' = internal, 'r' = root, 'm' = meta
    live_items,     -- Number of active index entries
    dead_items,     -- Number of vacuumed/dead index entries
    avg_item_size,  -- Average bytes per key tuple
    page_size,      -- Standard 8192 bytes
    free_size       -- Unused bytes available on the page
FROM bt_page_stats('idx_tickets_event_id', 1);

-- ============================================================================
-- 3. Inspect Individual Tuples on a B-Tree Leaf Page
-- Shows itemoffset, heap tuple pointer (ctid = (block, offset)), and byte payload.
-- ============================================================================
SELECT 
    itemoffset,     -- Slot index in page linp array
    ctid,           -- (BlockNumber, OffsetNumber) pointing to actual heap row!
    itemlen,        -- Length of this index entry
    nulls,          -- True if indexed column value is NULL
    vars,           -- True if variable-length data
    data            -- Hex/binary representation of indexed key value
FROM bt_page_items('idx_tickets_event_id', 1)
LIMIT 25;

-- ============================================================================
-- 4. Check Index Bloat and Disk Footprint Safely
-- ============================================================================
SELECT
    relname AS IndexName,
    pg_size_pretty(pg_relation_size(oid)) AS TotalIndexSize,
    pg_relation_size(oid) / 8192 AS TotalPages
FROM pg_class
WHERE relname IN ('idx_tickets_event_id', 'idx_tickets_order_id', 'tickets_pkey');
