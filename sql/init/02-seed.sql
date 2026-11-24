USE sale;
GO

-- 500,000 historical orders spread over customers 1..5000 and the
-- regular products 2..4 (product 1 is the flash sale, so its purchase
-- counters start at zero).
IF NOT EXISTS (SELECT 1 FROM Orders)
BEGIN
    ;WITH N AS (
        SELECT TOP (500000)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
        CROSS JOIN sys.all_objects c
    )
    INSERT INTO Orders (CustomerId, ProductId, Qty, BoughtAt)
    SELECT
        (n % 5000) + 1,
        (n % 3) + 2,
        1,
        DATEADD(SECOND, -n, GETUTCDATE())
    FROM N;

    PRINT 'Seeded 500000 orders.';
END
ELSE
    PRINT 'Orders already seeded, skipping.';
GO
