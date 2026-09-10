-- ============================================================================
-- 004_sync_compara_status.sql
-- Agregar columna para tracking independiente de sincronización a Compara Paraguai
-- ============================================================================

BEGIN;

-- Agregar columna sync_compara_status
ALTER TABLE productos ADD COLUMN IF NOT EXISTS sync_compara_status SMALLINT NOT NULL DEFAULT 0;
-- 0 = Pendiente, 1 = Enviado, 2 = Error

-- Agregar columna para timestamp del último envío a Compara Paraguai
ALTER TABLE productos ADD COLUMN IF NOT EXISTS last_sent_compara_at TIMESTAMPTZ;

-- Agregar índice para búsquedas rápidas
CREATE INDEX IF NOT EXISTS ix_productos_sync_compara_status ON productos(sync_compara_status);

COMMIT;
