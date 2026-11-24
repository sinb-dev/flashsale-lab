using System.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SaleDb>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Sale")));

var app = builder.Build();

// ---------------------------------------------------------------------------
// GET /products  - cheap list, always fast
// ---------------------------------------------------------------------------
app.MapGet("/products", async (SaleDb db) =>
    await db.Products
        .Select(p => new { p.Id, p.Name, p.Price, p.Stock })
        .ToListAsync());

// ---------------------------------------------------------------------------
// GET /products/{id} - cheap single read
// ---------------------------------------------------------------------------
app.MapGet("/products/{id:int}", async (int id, SaleDb db) =>
{
    var p = await db.Products.FindAsync(id);
    return p is null
        ? Results.NotFound()
        : Results.Ok(new { p.Id, p.Name, p.Price, p.Stock });
});

// ---------------------------------------------------------------------------
// POST /buy - the flash sale. Two deliberate bottlenecks:
//
//   1) Contention:  the product row is locked with UPDLOCK inside a
//      transaction, so all buys for the same product serialize on ONE row.
//      Kestrel accepts thousands of requests; they queue behind the lock.
//
//   2) Capacity:    the "max 2 per customer" check does a full scan of a
//      500k-row Orders table (no index on CustomerId on purpose), so each
//      buy also costs real SQL Server CPU while it holds the row lock.
// ---------------------------------------------------------------------------
app.MapPost("/buy", async (BuyRequest req, SaleDb db) =>
{
    if (req.CustomerId < 1)
        return Results.BadRequest(new { error = "customerId must be >= 1" });

    await using var tx =
        await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

    try
    {
        // 1) Hot-row lock: concurrent buys for this product wait here.
        var product = await db.Products
            .FromSqlInterpolated(
                $"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {req.ProductId}")
            .FirstOrDefaultAsync();

        if (product is null)
            return Results.NotFound(new { error = $"Product {req.ProductId} not found" });

        if (product.Stock <= 0)
            return Results.Conflict(new { error = "Sold out" });

        // 2) Expensive unindexed check: full scan of Orders.
        var bought = await db.Orders
            .CountAsync(o => o.CustomerId == req.CustomerId && o.ProductId == req.ProductId);

        if (bought >= 2)
            return Results.Conflict(new { error = "Purchase limit (2 per customer) reached" });

        db.Orders.Add(new Order
        {
            CustomerId = req.CustomerId,
            ProductId = req.ProductId,
            Qty = 1,
            BoughtAt = DateTime.UtcNow
        });

        product.Stock -= 1;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Results.Ok(new { ok = true, productId = product.Id, remaining = product.Stock });
    }
    catch (Exception)
    {
        // Typical cause under load: the 30s command timeout expires while
        // the request waits for the row lock or for a pooled connection.
        await tx.RollbackAsync();
        return Results.StatusCode(500);
    }
});

// ---------------------------------------------------------------------------
// GET /stats - quick status; the order count is itself a scan, which is fine
// ---------------------------------------------------------------------------
app.MapGet("/stats", async (SaleDb db) =>
{
    var flash = await db.Products.FindAsync(1);
    var orders = await db.Orders.CountAsync();
    return Results.Ok(new { flashSaleStock = flash?.Stock, totalOrders = orders });
});

// ---------------------------------------------------------------------------
// POST /admin/restock - teacher-only: refill stock between lab runs
// ---------------------------------------------------------------------------
app.MapPost("/admin/restock", async (RestockRequest req, SaleDb db) =>
{
    var p = await db.Products.FindAsync(req.ProductId);
    if (p is null) return Results.NotFound();
    p.Stock = req.Stock;
    await db.SaveChangesAsync();
    return Results.Ok(new { p.Id, p.Stock });
});

app.Run();

public record BuyRequest(int ProductId, int CustomerId);
public record RestockRequest(int ProductId, int Stock);

public class SaleDb : DbContext
{
    public SaleDb(DbContextOptions<SaleDb> options) : base(options) { }
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public int Qty { get; set; }
    public DateTime BoughtAt { get; set; }
}
