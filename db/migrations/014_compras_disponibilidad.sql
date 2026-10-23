-- ============================================================================
-- 014_compras_disponibilidad.sql
-- Foto de los productos con stock que Compras Paraguai marca como no disponibles (available = false).
-- El campo lo decide Compras (nunca lo enviamos). Worker.Verify --disponibilidad la reemplaza completa
-- en cada corrida; la API solo la lee.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS compras_disponibilidad (
    code            VARCHAR(255) PRIMARY KEY,
    stock_api       NUMERIC(12,2),
    updated_at_api  TIMESTAMPTZ,          -- última actualización en Compras (ya convertida a UTC)
    checked_at      TIMESTAMPTZ NOT NULL
);

-- Una sola fila: cuándo se tomó la última foto y cuántos productos con stock tenía Compras.
-- Sirve para distinguir "no corrió nunca" de "corrió y no hay ninguno".
CREATE TABLE IF NOT EXISTS compras_disponibilidad_corrida (
    id               SMALLINT PRIMARY KEY CHECK (id = 1),
    checked_at       TIMESTAMPTZ NOT NULL,
    total_con_stock  INT NOT NULL,
    no_disponibles   INT NOT NULL
);

COMMIT;
