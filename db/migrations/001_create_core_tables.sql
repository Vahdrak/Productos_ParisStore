-- ============================================================================
-- 001_create_core_tables.sql
-- Tablas núcleo de la BD propia (PostgreSQL): productos, categorias, sync_runs.
-- Ver docs/Plan_Sincronizacion_ComprasParaguai.md — Sección 4.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- categorias
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS categorias (
    id   INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ruta VARCHAR(4000) NOT NULL UNIQUE  -- "TOKU>COMESTIBLES>CHOCOLATES>ALFAJORES"
);

-- ---------------------------------------------------------------------------
-- productos
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS productos (
    id                 BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- ===== Campos que exige la API =====
    code               VARCHAR(255)  NOT NULL UNIQUE,   -- API: code (= SKU)
    name_es            VARCHAR(255),                    -- API: name.es (de Cangrejo)
    name_pt            VARCHAR(255)  NOT NULL,          -- API: name.pt (propio)
    price              NUMERIC(12,2),                   -- API: price (0.05 - 99999)
    price_iva          NUMERIC(12,2),                   -- API: price_iva
    stock              INT           NOT NULL DEFAULT 0,-- API: stock (nunca negativo)
    brand              VARCHAR(255),                    -- API: brand (de Cangrejo)
    description_es     TEXT,                            -- API: description.es (propio)
    description_pt     TEXT,                            -- API: description.pt (propio)
    url_image          VARCHAR(1000),                   -- API: url_image
    link               VARCHAR(1000),                   -- API: link (propio)
    link_purchase      VARCHAR(1000),                   -- API: link_purchase (propio)
    force_image_update BOOLEAN       NOT NULL DEFAULT FALSE,
    categoria_id       INT REFERENCES categorias(id),
    -- ===== Control de sincronización =====
    source_hash        CHAR(64),          -- hash del payload final (SHA-256)
    sync_status        SMALLINT   NOT NULL DEFAULT 0,   -- 0=Pendiente, 1=Enviado, 2=Error
    exists_in_view     BOOLEAN    NOT NULL DEFAULT TRUE,-- ¿sigue en la vista?
    last_seen_at       TIMESTAMPTZ,       -- última vez visto en la vista
    last_changed_at    TIMESTAMPTZ,       -- cuándo detectamos el último cambio
    last_sent_at       TIMESTAMPTZ,       -- último envío EXITOSO
    first_sent_at      TIMESTAMPTZ,       -- primer envío (NULL = nunca subido = nuevo)
    last_error         VARCHAR(2000),
    raw_json           JSONB,             -- fila original de la vista (para auditar)
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_productos_sync_status ON productos(sync_status);

-- ---------------------------------------------------------------------------
-- sync_runs (bitácora de corridas)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sync_runs (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    started_at    TIMESTAMPTZ NOT NULL,
    finished_at   TIMESTAMPTZ,
    rows_in_view  INT,   -- filas leídas de la vista
    new_count     INT,   -- productos nuevos
    updated_count INT,   -- productos modificados
    sent          INT,   -- enviados OK
    failed        INT,
    notes         VARCHAR(2000)
);

COMMIT;
