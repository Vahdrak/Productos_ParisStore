-- ⚠️ SCRIPT MANUAL - EJECUTAR UNA SOLA VEZ DESPUÉS DE MIGRACIÓN 007
-- Resetea todos los productos para recalcular con nuevas columnas de régimen turismo
-- Esto marca todos como pendientes de envío para que se re-sincronicen

BEGIN;

UPDATE productos
SET
    precio_exenta = price - (price / 11),
    sync_status = 0,
    sync_compara_status = 0,
    source_hash = NULL,
    updated_at = NOW()
WHERE price IS NOT NULL;

SELECT COUNT(*) as productos_reseteados FROM productos WHERE sync_status = 0;

COMMIT;
