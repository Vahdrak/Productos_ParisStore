using Npgsql;

var connectionString = "Host=localhost;Port=5432;Database=comprasparaguai;Username=postgres;Password=marionet.a";
var migrationSql = @"
BEGIN;

CREATE TABLE IF NOT EXISTS sync_cp_runs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    started_at TIMESTAMPTZ NOT NULL,
    finished_at TIMESTAMPTZ,
    batch_num INT,
    rows_sent INT,
    http_status INT,
    response_text VARCHAR(2000),
    success BOOLEAN,
    error_msg VARCHAR(2000),
    notes VARCHAR(2000)
);

CREATE INDEX IF NOT EXISTS ix_sync_cp_runs_started ON sync_cp_runs(started_at DESC);

COMMIT;
";

try
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    
    using var cmd = new NpgsqlCommand(migrationSql, conn);
    cmd.ExecuteNonQuery();
    
    Console.WriteLine("? Tabla sync_cp_runs creada exitosamente");
}
catch (Exception ex)
{
    Console.WriteLine($"? Error: {ex.Message}");
}
