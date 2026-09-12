-- ============================================================================
-- 005_last_error_compara.sql
-- Mensaje de error del último envío a Compara Paraguai (par de last_error, que es de Compras).
-- ============================================================================

BEGIN;

ALTER TABLE productos ADD COLUMN IF NOT EXISTS last_error_compara VARCHAR(2000);

COMMIT;
