-- ============================================================================
-- 008_reorder_productos_columns.sql
-- Reordena columnas de la tabla productos en orden lógico:
-- código → datos básicos → precios/régimen → stock → URLs → sync → auditoría
-- ============================================================================

BEGIN;

-- Crear tabla con columnas en nuevo orden (27 columnas reales)
CREATE TABLE productos_new (
    -- ===== Identificación =====
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code               VARCHAR(255)  NOT NULL UNIQUE,

    -- ===== Datos básicos del producto =====
    name_es            VARCHAR(255),
    name_pt            VARCHAR(255)  NOT NULL,
    brand              VARCHAR(255),
    description_es     TEXT,
    description_pt     TEXT,
    categoria_id       INTEGER REFERENCES categorias(id),

    -- ===== Régimen y precios =====
    regimen_turismo    BOOLEAN DEFAULT false,
    price              NUMERIC(12,2),
    precio_exenta      NUMERIC(12,2),
    price_iva          NUMERIC(12,2),
    stock              INTEGER NOT NULL DEFAULT 0,

    -- ===== URLs y enlaces =====
    url_image          VARCHAR(1000),
    link               VARCHAR(1000),
    link_purchase      VARCHAR(1000),
    force_image_update BOOLEAN NOT NULL DEFAULT FALSE,

    -- ===== Control de sincronización =====
    source_hash        CHAR(64),
    sync_status        INTEGER NOT NULL DEFAULT 0,
    sync_compara_status INTEGER NOT NULL DEFAULT 0,
    last_sent_at       TIMESTAMP WITHOUT TIME ZONE,
    last_sent_compara_at TIMESTAMP WITHOUT TIME ZONE,
    last_error         TEXT,
    last_error_compara TEXT,

    -- ===== Auditoría =====
    created_at         TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    updated_at         TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    last_changed_at    TIMESTAMP WITHOUT TIME ZONE
);

-- Copiar datos de la tabla vieja (27 columnas)
INSERT INTO productos_new (
    id, code, name_es, name_pt, brand, description_es, description_pt, categoria_id,
    regimen_turismo, price, precio_exenta, price_iva, stock,
    url_image, link, link_purchase, force_image_update,
    source_hash, sync_status, sync_compara_status,
    last_sent_at, last_sent_compara_at, last_error, last_error_compara,
    created_at, updated_at, last_changed_at
) OVERRIDING SYSTEM VALUE
SELECT
    id, code, name_es, name_pt, brand, description_es, description_pt, categoria_id,
    regimen_turismo, price, precio_exenta, price_iva, stock,
    url_image, link, link_purchase, force_image_update,
    source_hash, sync_status, sync_compara_status,
    last_sent_at, last_sent_compara_at, last_error, last_error_compara,
    created_at, updated_at, last_changed_at
FROM productos;

-- Renombrar tabla vieja
ALTER TABLE productos RENAME TO productos_old;

-- Renombrar tabla nueva
ALTER TABLE productos_new RENAME TO productos;

-- Eliminar tabla vieja
DROP TABLE productos_old;

-- Resetear la secuencia IDENTITY al máximo ID actual
SELECT setval(pg_get_serial_sequence('productos', 'id'), COALESCE(MAX(id), 1)) FROM productos;

-- Recrear índices
CREATE INDEX IF NOT EXISTS ix_productos_code ON productos(code);
CREATE INDEX IF NOT EXISTS ix_productos_sync_status ON productos(sync_status);
CREATE INDEX IF NOT EXISTS ix_productos_sync_compara_status ON productos(sync_compara_status);
CREATE INDEX IF NOT EXISTS ix_productos_regimen_turismo ON productos(regimen_turismo);

COMMIT;
