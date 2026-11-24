IF DB_ID(N'sale') IS NULL
    CREATE DATABASE sale;
GO
USE sale;
GO

IF OBJECT_ID(N'Orders') IS NULL
BEGIN
    CREATE TABLE Products (
        Id       INT IDENTITY PRIMARY KEY,
        Name     NVARCHAR(100) NOT NULL,
        Price    DECIMAL(10,2) NOT NULL,
        Stock    INT NOT NULL
    );

    CREATE TABLE Orders (
        Id         INT IDENTITY PRIMARY KEY,
        CustomerId INT NOT NULL,
        ProductId  INT NOT NULL,
        Qty        INT NOT NULL,
        BoughtAt   DATETIME2 NOT NULL
    );

    -- Deliberately NO index on Orders.CustomerId or Orders.ProductId:
    -- the "max 2 per customer" check is a full table scan, which is one
    -- of the bottlenecks the students are meant to observe.

    INSERT INTO Products (Name, Price, Stock) VALUES
        (N'Flash-Sale Gadget X1', 199.00, 250),
        (N'Widget A', 49.00, 10000),
        (N'Widget B', 79.00, 10000),
        (N'Widget C', 29.00, 10000);

    PRINT 'Schema and products created.';
END
ELSE
    PRINT 'Schema already exists, skipping.';
GO
