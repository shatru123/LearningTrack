-- ============================================================================
-- Day 08: 4 Critical Indexing Cases Compared with EXPLAIN ANALYZE
-- ============================================================================

-- ============================================================================
-- CASE 1: No Useful Index -> Sequential Scan (Full Table Scan)
-- Query filters on SeatNumber without an index.
-- PostgreSQL must read every 8KB heap page sequentially from disk/shared buffers.
-- Cost: High I/O, O(N) page reads, buffer cache thrashing.
-- ============================================================================
EXPLAIN (ANALYZE, BUFFERS, COSTS, VERBOSE)
SELECT TicketId, EventId, SeatNumber, Price
FROM Tickets
WHERE SeatNumber = 'Block-12-Seat-45';

-- Expected Plan:
-- Seq Scan on public.tickets  (cost=0.00..3285.00 rows=1 width=36) (actual time=0.021..14.850 rows=1 loops=1)
--   Filter: ((seatnumber)::text = 'Block-12-Seat-45'::text)
--   Rows Removed by Filter: 149999
--   Buffers: shared hit=1035


-- ============================================================================
-- CASE 2: Single-Column B-Tree Index -> Index Scan / Bitmap Index Scan
-- Query filters on EventId where idx_tickets_event_id exists.
-- Selectivity determines whether planner chooses Index Scan or Bitmap Index Scan.
-- ============================================================================
-- High Selectivity (Few rows returned) -> Direct B-Tree Index Scan:
EXPLAIN (ANALYZE, BUFFERS, COSTS)
SELECT TicketId, EventId, Price, Status
FROM Tickets
WHERE EventId = 42;

-- Expected Plan:
-- Bitmap Heap Scan on public.tickets  (cost=18.42..1540.20 rows=750 width=28) (actual time=0.120..0.850 rows=750 loops=1)
--   Recheck Cond: (eventid = 42)
--   Buffers: shared hit=42
--   ->  Bitmap Index Scan on idx_tickets_event_id  (cost=0.00..18.23 rows=750 width=0) (actual time=0.080..0.080 rows=750 loops=1)
--         Index Cond: (eventid = 42)
--         Buffers: shared hit=4


-- ============================================================================
-- CASE 3: Composite & Covering Index -> Index-Only Scan (Zero Heap Fetches)
-- Query: Find completed orders for customer with TotalAmount and OrderDate.
-- ============================================================================
CREATE INDEX IF NOT EXISTS idx_orders_customer_status_covering 
ON Orders (CustomerId, Status) 
INCLUDE (TotalAmount, OrderDate);

EXPLAIN (ANALYZE, BUFFERS, COSTS)
SELECT CustomerId, Status, TotalAmount, OrderDate
FROM Orders
WHERE CustomerId = 142 AND Status = 'Completed';

-- Expected Plan:
-- Index Only Scan using idx_orders_customer_status_covering on public.orders  (cost=0.41..8.43 rows=3 width=29) (actual time=0.025..0.028 rows=3 loops=1)
--   Index Cond: ((customerid = 142) AND (status = 'Completed'::text))
--   Heap Fetches: 0   <-- KEY PRODUCTION WIN: Zero table heap pages accessed!
--   Buffers: shared hit=3


-- ============================================================================
-- CASE 4: Poorly Designed Indexes / Anti-Patterns
-- ============================================================================

-- Anti-pattern 4A: Function wrapping the column prevents B-Tree index usage!
-- Query: Case-insensitive search on Customer Email
-- A standard index on Email CANNOT be used with UPPER(Email)!
EXPLAIN (ANALYZE, BUFFERS)
SELECT CustomerId, FullName, Email
FROM Customers
WHERE UPPER(Email) = 'FAN_100@TICKETDOMAIN.CO.UK';
-- Result: Seq Scan on customers (Index ignored!)

-- Solution 4A: Create an Expression / Functional B-Tree Index:
CREATE INDEX IF NOT EXISTS idx_customers_upper_email ON Customers (UPPER(Email));

EXPLAIN (ANALYZE, BUFFERS)
SELECT CustomerId, FullName, Email
FROM Customers
WHERE UPPER(Email) = 'FAN_100@TICKETDOMAIN.CO.UK';
-- Result: Index Scan using idx_customers_upper_email!


-- Anti-pattern 4B: Leading Wildcard in LIKE queries
-- B-Trees store data sorted lexicographically. Leading wildcards prevent index seek!
EXPLAIN (ANALYZE, BUFFERS)
SELECT CustomerId, FullName, Email
FROM Customers
WHERE Email LIKE '%ticketdomain.co.uk';
-- Result: Seq Scan (B-tree cannot binary search without known prefix).


-- Anti-pattern 4C: Violating Leftmost Prefix Rule on Composite Index
-- Given index on (CustomerId, Status):
-- Query filtering ONLY by Status cannot use CustomerId index tree traversal!
EXPLAIN (ANALYZE, BUFFERS)
SELECT OrderId, CustomerId, TotalAmount
FROM Orders
WHERE Status = 'Pending';
-- Result: Seq Scan on orders (violates leftmost prefix rule!).
