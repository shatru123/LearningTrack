-- ============================================================================
-- Day 08: PostgreSQL B-Tree Schema
-- Realistic Ticketing & Event Management Database
-- ============================================================================

CREATE TABLE IF NOT EXISTS Venues (
    VenueId SERIAL PRIMARY KEY,
    Name VARCHAR(150) NOT NULL,
    City VARCHAR(100) NOT NULL,
    Capacity INT NOT NULL CHECK (Capacity > 0),
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS Events (
    EventId SERIAL PRIMARY KEY,
    VenueId INT NOT NULL REFERENCES Venues(VenueId),
    Name VARCHAR(200) NOT NULL,
    Category VARCHAR(50) NOT NULL,
    EventDate TIMESTAMPTZ NOT NULL,
    BasePrice NUMERIC(10, 2) NOT NULL CHECK (BasePrice >= 0),
    Status VARCHAR(30) NOT NULL DEFAULT 'Scheduled',
    CreatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS Customers (
    CustomerId SERIAL PRIMARY KEY,
    Email VARCHAR(200) NOT NULL UNIQUE,
    FullName VARCHAR(150) NOT NULL,
    Tier VARCHAR(30) NOT NULL DEFAULT 'Standard',
    RegisteredAt TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS Orders (
    OrderId SERIAL PRIMARY KEY,
    CustomerId INT NOT NULL REFERENCES Customers(CustomerId),
    TotalAmount NUMERIC(10, 2) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Pending',
    OrderDate TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS Tickets (
    TicketId BIGSERIAL PRIMARY KEY,
    EventId INT NOT NULL REFERENCES Events(EventId),
    OrderId INT REFERENCES Orders(OrderId),
    SeatNumber VARCHAR(20) NOT NULL,
    Price NUMERIC(10, 2) NOT NULL,
    Status VARCHAR(30) NOT NULL DEFAULT 'Available',
    PurchasedAt TIMESTAMPTZ
);
