-- ============================================================================
-- 002_audit_historial.sql
-- Auditoría "del antes": tabla de historial + trigger AFTER UPDATE que guarda
-- el estado PREVIO del producto cada vez que cambia su source_hash.
-- Ver docs/Plan_Sincronizacion_ComprasParaguai.md — Sección 4.1.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- productos_historial — snapshot del producto ANTES de cada cambio real
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS productos_historial (
    historial_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id    BIGINT       NOT NULL,   -- referencia a productos.id
    code           VARCHAR(255) NOT NULL,
    -- snapshot de los campos relevantes ANTES del cambio:
    name_es        VARCHAR(255),
    name_pt        VARCHAR(255),
    price          NUMERIC(12,2),
    price_iva      NUMERIC(12,2),
    stock          INT,
    brand          VARCHAR(255),
    url_image      VARCHAR(1000),
    source_hash    CHAR(64),
    sync_status    SMALLINT,
    -- metadatos de auditoría:
    changed_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    operation      CHAR(1)     NOT NULL DEFAULT 'U'  -- 'U'=update, 'D'=delete (opcional)
);
CREATE INDEX IF NOT EXISTS ix_hist_producto ON productos_historial(producto_id, changed_at DESC);

-- ---------------------------------------------------------------------------
-- Función de auditoría: solo registra si cambió el source_hash
-- (evita ruido de timestamps y reintentos)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_audit_producto() RETURNS trigger AS $$
BEGIN
    IF OLD.source_hash IS DISTINCT FROM NEW.source_hash THEN
        INSERT INTO productos_historial(
            producto_id, code, name_es, name_pt, price, price_iva,
            stock, brand, url_image, source_hash, sync_status, operation)
        VALUES (OLD.id, OLD.code, OLD.name_es, OLD.name_pt, OLD.price, OLD.price_iva,
                OLD.stock, OLD.brand, OLD.url_image, OLD.source_hash, OLD.sync_status, 'U');
    END IF;
    RETURN NEW;
END; $$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------------
-- Trigger AFTER UPDATE sobre productos
-- ---------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_audit_producto ON productos;
CREATE TRIGGER trg_audit_producto
    AFTER UPDATE ON productos
    FOR EACH ROW EXECUTE FUNCTION fn_audit_producto();

COMMIT;

-- Nota de rendimiento:
--   Durante la CARGA INICIAL masiva se puede desactivar el trigger y reactivarlo después:
--     ALTER TABLE productos DISABLE TRIGGER trg_audit_producto;
--     ... carga inicial ...
--     ALTER TABLE productos ENABLE TRIGGER trg_audit_producto;
