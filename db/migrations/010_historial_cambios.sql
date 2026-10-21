-- ============================================================================
-- 010_historial_cambios.sql
-- Historial de cambios con diff por campo, origen y usuario, más índices para
-- buscar por producto, marca y texto, y archivo de historial viejo.
--
--  * productos_historial: marca_id, categoria_id, cambios (JSONB), origen, usuario.
--    cambios = {"price": {"antes": "10.00", "despues": "12.00"}, ...}
--  * El trigger ahora registra cuando cambia ALGUNO de los campos seguidos
--    (nombre, marca, categoría, precio, precio exenta, régimen, stock, imagen),
--    aunque no cambie el hash: así la baja (stock -> 0) también queda registrada.
--    Los cambios de solo estado de envío o de solo descripción NO escriben historial.
--  * Origen/usuario: el repositorio los fija por transacción con
--    set_config('app.origen' / 'app.usuario', ..., true).
--  * Índices: historial por (code, fecha), (marca, fecha), fecha y GIN(cambios);
--    productos por trigram (code, name_es, name_pt) para ILIKE '%texto%'.
--  * Archivo: productos_historial_archivo + fn_archivar_historial(meses).
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- Columnas nuevas del historial
-- ---------------------------------------------------------------------------
ALTER TABLE productos_historial
    ADD COLUMN marca_id     INTEGER,        -- marca del producto DESPUÉS del cambio
    ADD COLUMN categoria_id INTEGER,        -- categoría del producto DESPUÉS del cambio
    ADD COLUMN cambios      JSONB,          -- {campo: {antes, despues}}
    ADD COLUMN origen       VARCHAR(20),    -- sync | reconciliacion | api | manual | historico
    ADD COLUMN usuario      VARCHAR(100);

-- Diff de un campo como JSON; '{}' si no cambió. Se concatena con ||.
CREATE OR REPLACE FUNCTION fn_cambio(campo text, antes text, despues text) RETURNS jsonb
LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE
        WHEN antes IS NOT DISTINCT FROM despues THEN '{}'::jsonb
        ELSE jsonb_build_object(campo, jsonb_build_object('antes', antes, 'despues', despues))
    END
$$;

-- ---------------------------------------------------------------------------
-- Backfill de las filas viejas: cada foto "antes" se compara con la siguiente foto
-- del mismo producto (o con el valor actual si es la última). Solo hay datos para
-- nombre, precio, stock, marca e imagen; categoría/régimen no se guardaban.
-- ---------------------------------------------------------------------------
CREATE TEMP TABLE hist_despues ON COMMIT DROP AS
SELECT d.historial_id,
       (d.next_id IS NOT NULL OR p.id IS NOT NULL) AS tiene_despues,
       CASE WHEN d.next_id IS NULL THEN p.name_es   ELSE d.n_name_es   END AS a_name_es,
       CASE WHEN d.next_id IS NULL THEN p.name_pt   ELSE d.n_name_pt   END AS a_name_pt,
       CASE WHEN d.next_id IS NULL THEN p.price     ELSE d.n_price     END AS a_price,
       CASE WHEN d.next_id IS NULL THEN p.stock     ELSE d.n_stock     END AS a_stock,
       CASE WHEN d.next_id IS NULL THEN pm.nombre   ELSE d.n_brand     END AS a_brand,
       CASE WHEN d.next_id IS NULL THEN p.url_image ELSE d.n_url_image END AS a_url_image,
       p.categoria_id AS cat_actual
FROM (
    SELECT h.historial_id, h.code,
           lead(h.historial_id) OVER w AS next_id,
           lead(h.name_es)      OVER w AS n_name_es,
           lead(h.name_pt)      OVER w AS n_name_pt,
           lead(h.price)        OVER w AS n_price,
           lead(h.stock)        OVER w AS n_stock,
           lead(h.brand)        OVER w AS n_brand,
           lead(h.url_image)    OVER w AS n_url_image
    FROM productos_historial h
    WINDOW w AS (PARTITION BY h.code ORDER BY h.changed_at, h.historial_id)
) d
LEFT JOIN productos p ON p.code = d.code
LEFT JOIN marcas pm ON pm.id = p.marca_id;

UPDATE productos_historial h
SET marca_id     = m.id,
    categoria_id = x.cat_actual,
    origen       = 'historico',
    cambios      = CASE WHEN x.tiene_despues THEN
                       fn_cambio('name_es',   h.name_es,      x.a_name_es)
                    || fn_cambio('name_pt',   h.name_pt,      x.a_name_pt)
                    || fn_cambio('marca',     h.brand,        x.a_brand)
                    || fn_cambio('price',     h.price::text,  x.a_price::text)
                    || fn_cambio('stock',     h.stock::text,  x.a_stock::text)
                    || fn_cambio('url_image', h.url_image,    x.a_url_image)
                   ELSE '{}'::jsonb END
FROM hist_despues x
LEFT JOIN marcas m ON upper(m.nombre) = upper(fn_normalizar_marca(x.a_brand))
WHERE h.historial_id = x.historial_id;

-- ---------------------------------------------------------------------------
-- Índices del historial
-- ---------------------------------------------------------------------------
CREATE INDEX ix_hist_code_fecha  ON productos_historial (code, changed_at DESC);
CREATE INDEX ix_hist_marca_fecha ON productos_historial (marca_id, changed_at DESC);
CREATE INDEX ix_hist_fecha       ON productos_historial (changed_at DESC);
CREATE INDEX ix_hist_cambios_gin ON productos_historial USING gin (cambios);

-- ---------------------------------------------------------------------------
-- Archivo de historial viejo (misma estructura; crearla DESPUÉS de los ALTER de arriba.
-- Si se agregan columnas a productos_historial, agregarlas también aquí)
-- ---------------------------------------------------------------------------
CREATE TABLE productos_historial_archivo (LIKE productos_historial INCLUDING DEFAULTS);
ALTER TABLE productos_historial_archivo ADD PRIMARY KEY (historial_id);
CREATE INDEX ix_hist_arch_code_fecha ON productos_historial_archivo (code, changed_at DESC);
CREATE INDEX ix_hist_arch_fecha      ON productos_historial_archivo (changed_at DESC);

-- Mueve al archivo lo más viejo que N meses; devuelve cuántas filas movió.
CREATE OR REPLACE FUNCTION fn_archivar_historial(meses integer) RETURNS bigint
LANGUAGE plpgsql AS $$
DECLARE
    n bigint;
BEGIN
    WITH mov AS (
        DELETE FROM productos_historial
        WHERE changed_at < now() - make_interval(months => meses)
        RETURNING *
    )
    INSERT INTO productos_historial_archivo SELECT * FROM mov;
    GET DIAGNOSTICS n = ROW_COUNT;
    RETURN n;
END;
$$;

-- ---------------------------------------------------------------------------
-- Trigger de auditoría: alta (INSERT) y cambio de campos seguidos (UPDATE)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_audit_producto() RETURNS trigger AS $$
DECLARE
    v_cambios   jsonb;
    v_origen    text := COALESCE(NULLIF(current_setting('app.origen', true), ''), 'manual');
    v_usuario   text := NULLIF(current_setting('app.usuario', true), '');
    v_marca_old text;
    v_marca_new text;
    v_cat_old   text;
    v_cat_new   text;
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO productos_historial (producto_id, code, marca_id, categoria_id, cambios, origen, usuario, operation)
        VALUES (NEW.id, NEW.code, NEW.marca_id, NEW.categoria_id, '{}'::jsonb, v_origen, v_usuario, 'I');
        RETURN NEW;
    END IF;

    SELECT nombre INTO v_marca_old FROM marcas WHERE id = OLD.marca_id;
    v_marca_new := v_marca_old;
    IF OLD.marca_id IS DISTINCT FROM NEW.marca_id THEN
        SELECT nombre INTO v_marca_new FROM marcas WHERE id = NEW.marca_id;
    END IF;

    v_cat_old := NULL;
    v_cat_new := NULL;
    IF OLD.categoria_id IS DISTINCT FROM NEW.categoria_id THEN
        SELECT ruta INTO v_cat_old FROM categorias WHERE id = OLD.categoria_id;
        SELECT ruta INTO v_cat_new FROM categorias WHERE id = NEW.categoria_id;
    END IF;

    v_cambios := fn_cambio('name_es',         OLD.name_es,                 NEW.name_es)
              || fn_cambio('name_pt',         OLD.name_pt,                 NEW.name_pt)
              || fn_cambio('marca',           v_marca_old,                 v_marca_new)
              || fn_cambio('categoria',       v_cat_old,                   v_cat_new)
              || fn_cambio('price',           OLD.price::text,             NEW.price::text)
              || fn_cambio('precio_exenta',   OLD.precio_exenta::text,     NEW.precio_exenta::text)
              || fn_cambio('regimen_turismo', OLD.regimen_turismo::text,   NEW.regimen_turismo::text)
              || fn_cambio('stock',           OLD.stock::text,             NEW.stock::text)
              || fn_cambio('url_image',       OLD.url_image,               NEW.url_image);

    IF v_cambios <> '{}'::jsonb THEN
        -- Las columnas de foto (name_es, price, brand...) guardan el valor ANTERIOR, como siempre
        -- (las usan /historial y /diff); marca_id/categoria_id guardan el valor posterior.
        INSERT INTO productos_historial (
            producto_id, code, name_es, name_pt, price, price_iva, stock, brand, url_image,
            source_hash, sync_status, operation, marca_id, categoria_id, cambios, origen, usuario)
        VALUES (
            OLD.id, OLD.code, OLD.name_es, OLD.name_pt, OLD.price, OLD.price_iva, OLD.stock, v_marca_old, OLD.url_image,
            OLD.source_hash, OLD.sync_status, 'U', NEW.marca_id, NEW.categoria_id, v_cambios, v_origen, v_usuario);
    END IF;
    RETURN NEW;
END; $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_producto ON productos;
DROP TRIGGER IF EXISTS trg_audit_producto_insert ON productos;

-- UPDATE: solo se dispara si cambió algún campo seguido (MarkAsSent y similares no la invocan).
CREATE TRIGGER trg_audit_producto
    AFTER UPDATE ON productos
    FOR EACH ROW
    WHEN (OLD.name_es         IS DISTINCT FROM NEW.name_es
       OR OLD.name_pt         IS DISTINCT FROM NEW.name_pt
       OR OLD.marca_id        IS DISTINCT FROM NEW.marca_id
       OR OLD.categoria_id    IS DISTINCT FROM NEW.categoria_id
       OR OLD.price           IS DISTINCT FROM NEW.price
       OR OLD.precio_exenta   IS DISTINCT FROM NEW.precio_exenta
       OR OLD.regimen_turismo IS DISTINCT FROM NEW.regimen_turismo
       OR OLD.stock           IS DISTINCT FROM NEW.stock
       OR OLD.url_image       IS DISTINCT FROM NEW.url_image)
    EXECUTE FUNCTION fn_audit_producto();

CREATE TRIGGER trg_audit_producto_insert
    AFTER INSERT ON productos
    FOR EACH ROW EXECUTE FUNCTION fn_audit_producto();

-- ---------------------------------------------------------------------------
-- Búsqueda por texto: ILIKE '%texto%' con índices trigram
-- (si pg_trgm no está disponible o no hay permiso, se omiten sin romper la migración)
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'pg_trgm no disponible (%): se omiten los indices de busqueda por texto', SQLERRM;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_productos_code_trgm    ON productos USING gin (code gin_trgm_ops)';
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_productos_name_es_trgm ON productos USING gin (name_es gin_trgm_ops)';
        EXECUTE 'CREATE INDEX IF NOT EXISTS ix_productos_name_pt_trgm ON productos USING gin (name_pt gin_trgm_ops)';
    END IF;
END $$;

COMMIT;
