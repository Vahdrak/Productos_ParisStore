-- ============================================================================
-- Corrección one-shot (NO es migración; ejecutar a mano una sola vez).
--
-- El 2026-09-11 se corrió --send-all contra Compara Paraguai con un SendService
-- que marcaba sync_status (columna de COMPRAS). Este script mueve ese resultado a
-- las columnas de Compara y devuelve las de Compras a Pendiente.
--
-- Premisa: Compras Paraguai todavía no recibió ningún envío real, así que todo lo
-- que hoy está en sync_status 1/2 proviene de esa corrida.
--
-- Requiere migración 005 (last_error_compara).
-- ============================================================================

BEGIN;

-- Antes
SELECT 'antes' AS momento, sync_status, sync_compara_status, COUNT(*)
FROM productos GROUP BY 1, 2, 3 ORDER BY 2, 3;

UPDATE productos SET
    sync_compara_status  = sync_status,
    last_sent_compara_at = CASE WHEN sync_status = 1 THEN last_sent_at END,
    last_error_compara   = CASE WHEN sync_status = 2 THEN last_error END
WHERE sync_status IN (1, 2);

UPDATE productos SET
    sync_status   = 0,
    last_sent_at  = NULL,
    first_sent_at = NULL,
    last_error    = NULL
WHERE sync_status IN (1, 2);

-- Después: sync_status debe quedar todo en 0.
SELECT 'despues' AS momento, sync_status, sync_compara_status, COUNT(*)
FROM productos GROUP BY 1, 2, 3 ORDER BY 2, 3;

COMMIT;
