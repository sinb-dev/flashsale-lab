# Flash-Sale Lab — Locust + .NET + SQL Server

Students run Locust on their own machine and hammer a flash-sale API hosted
on a shared server. The API (ASP.NET Core, EF Core) talks to a seeded SQL Server.
Goal: watch the database become the bottleneck — long `/buy` latencies and
locked sessions — while cheap reads stay fast.

## Layout

```
docker-compose.yml        spawns both containers
sql/Dockerfile            SQL Server 2022 + init scripts (schema + seed)
sql/init/01-schema.sql    Products (4 rows, product 1 = flash sale) + Orders
sql/init/02-seed.sql      500,000 historical orders, NO index on CustomerId
api/                      ASP.NET Core minimal API (EF Core)
locust/locustfile.py      Locust scenario: 3x browse, 1x buy
```

## Run on the VM

```bash
docker compose up -d --build
# first start: SQL Server boots, schema + 500k rows are seeded (~1-2 min)
curl http://localhost:8080/products
curl http://localhost:8080/stats
```

`docker compose logs -f sql` shows the seeding progress. The database lives
in the `sqldata` volume, so restarting does not re-seed.

The API is deliberately thin (async, no business logic) so the .NET side
stays idle — low CPU, high latency = database problem, not app problem.

## Student flow

```bash
pip install locust
locust -f locustfile.py --host http://<server-ip>:8080
# open http://localhost:8089, start with 20 users, spawn 2/s, ramp up
```

Watch in the Locust UI:

- `/products/[id]` (reads) stays fast while `/buy (flash sale)` p95 climbs.
- Failure rate rises once buys start timing out (30 s command timeout)
  while waiting for the lock or a pooled connection.

## Reset between runs

```bash
curl -X POST http://<server-ip>:8080/admin/restock \
     -H "Content-Type: application/json" -d '{"productId": 1, "stock": 250}'
```

Full reset (wipes and re-seeds): `docker compose down -v && docker compose up -d --build`.