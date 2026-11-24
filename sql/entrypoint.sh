#!/bin/bash
# Start SQL Server, wait for it to come up, run the init scripts, then
# keep the server in the foreground for the lifetime of the container.
set -e

echo "Starting SQL Server..."
if [ "$(id -u)" = "0" ]; then
  # Only needed if the container is ever run as root (e.g. `user: root` override)
  su -s /bin/bash mssql -c "/opt/mssql/bin/sqlservr" &
else
  # Normal case: container already runs as mssql — start sqlservr directly
  /opt/mssql/bin/sqlservr &
fi
SQLPID=$!

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
echo "Waiting for SQL Server to accept connections..."
for i in $(seq 1 120); do
  if "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" -h -1 >/dev/null 2>&1; then
    echo "SQL Server is up."
    break
  fi
  if [ "$i" -eq 120 ]; then
    echo "SQL Server did not become ready in time." >&2
    exit 1
  fi
  sleep 2
done

for f in /usr/src/app/init/*.sql; do
  echo ">> Running $f"
  "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -i "$f"
done

echo "Init done. SQL Server keeps running."
wait "$SQLPID"
