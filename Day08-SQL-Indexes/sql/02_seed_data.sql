-- ============================================================================
-- Day 08: Data Seeding Script (High Volume for Meaningful Query Plans)
-- ============================================================================

-- 1. Seed Venues
INSERT INTO Venues (Name, City, Capacity)
VALUES 
    ('Emirates Stadium', 'London', 60704),
    ('Stamford Bridge', 'London', 40341),
    ('Anfield', 'Liverpool', 61276),
    ('Old Trafford', 'Manchester', 74310),
    ('Etihad Stadium', 'Manchester', 53400)
ON CONFLICT DO NOTHING;

-- 2. Seed 200 Events
INSERT INTO Events (VenueId, Name, Category, EventDate, BasePrice, Status)
SELECT 
    (i % 5) + 1,
    'Premier League Match Day ' || i,
    CASE WHEN i % 3 = 0 THEN 'Concert' ELSE 'Football' END,
    NOW() + (i || ' days')::INTERVAL,
    (50 + (i % 20) * 10)::NUMERIC,
    CASE WHEN i % 10 = 0 THEN 'Cancelled' ELSE 'Scheduled' END
FROM generate_series(1, 200) AS s(i);

-- 3. Seed 10,000 Customers
INSERT INTO Customers (Email, FullName, Tier)
SELECT 
    'fan_' || i || '@ticketdomain.co.uk',
    'Customer ' || i,
    CASE WHEN i % 50 = 0 THEN 'VIP' WHEN i % 5 = 0 THEN 'Gold' ELSE 'Standard' END
FROM generate_series(1, 10000) AS s(i)
ON CONFLICT (Email) DO NOTHING;

-- 4. Seed 50,000 Orders
INSERT INTO Orders (CustomerId, TotalAmount, Status, OrderDate)
SELECT 
    (i % 10000) + 1,
    (100 + (i % 50) * 15)::NUMERIC,
    CASE WHEN i % 20 = 0 THEN 'Refunded' WHEN i % 4 = 0 THEN 'Pending' ELSE 'Completed' END,
    NOW() - ((i % 365) || ' days')::INTERVAL
FROM generate_series(1, 50000) AS s(i);

-- 5. Seed 150,000 Tickets
INSERT INTO Tickets (EventId, OrderId, SeatNumber, Price, Status, PurchasedAt)
SELECT 
    (i % 200) + 1,
    CASE WHEN i % 3 = 0 THEN NULL ELSE (i % 50000) + 1 END,
    'Block-' || ((i % 100) + 1) || '-Seat-' || ((i % 500) + 1),
    (60 + (i % 15) * 10)::NUMERIC,
    CASE WHEN i % 3 = 0 THEN 'Available' ELSE 'Sold' END,
    CASE WHEN i % 3 = 0 THEN NULL ELSE NOW() - ((i % 60) || ' days')::INTERVAL END
FROM generate_series(1, 150000) AS s(i);
