-- ============================================================================
-- 003_sync_cp.sql
-- Tabla para registrar sincronizaciones a Compara Paraguai (API externa)
-- Separada de sync_runs para no interferir con ComprasParaguai
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS sync_cp_runs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    started_at TIMESTAMPTZ NOT NULL,
    finished_at TIMESTAMPTZ,
    batch_num INT,                  -- lote #1, #2, etc.
    rows_sent INT,                  -- cuántos productos enviados
    http_status INT,                -- 200, 202, 500, etc.
    response_text VARCHAR(2000),    -- respuesta de la API de Compara Paraguai
    success BOOLEAN,                -- true si status 200-299
    error_msg VARCHAR(2000),        -- mensaje de error si falló
    notes VARCHAR(2000)             -- notas opcionales
);

CREATE INDEX IF NOT EXISTS ix_sync_cp_runs_started ON sync_cp_runs(started_at DESC);

COMMIT;
