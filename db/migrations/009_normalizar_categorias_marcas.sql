-- ============================================================================
-- 009_normalizar_categorias_marcas.sql
-- Normaliza el catálogo en PostgreSQL:
--   * categorias: de lista plana de rutas a ÁRBOL (parent_id), sin repetidos ni
--     caracteres rotos. Las rutas crudas de Oracle se recuerdan en categoria_alias.
--   * marcas: tabla propia; productos.brand (texto) pasa a productos.marca_id.
--   * Recrea el trigger de auditoría (se perdió en la 008 al reconstruir productos).
-- El source_hash NO cambia, así que esta migración no dispara reenvíos por sí sola.
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- Funciones de normalización (una sola regla, la usan la migración y el sync)
-- ---------------------------------------------------------------------------

-- Cangrejo entrega algunas letras como katakana de ancho medio (U+FF61..U+FF9F):
-- "BAﾑO" en vez de "BAÑO". El carácter correcto es cp - 0xFF61 + 0xA1 (Latin-1).
CREATE OR REPLACE FUNCTION fn_fix_encoding(t text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE
        WHEN t IS NULL THEN NULL
        WHEN t !~ ('[' || chr(65377) || '-' || chr(65439) || ']') THEN t
        ELSE (SELECT string_agg(
                        CASE WHEN ascii(x.ch) BETWEEN 65377 AND 65439
                             THEN chr(ascii(x.ch) - 65377 + 161)
                             ELSE x.ch END,
                        '' ORDER BY x.ord)
              FROM regexp_split_to_table(t, '') WITH ORDINALITY AS x(ch, ord))
    END
$$;

-- "A>B>B>C " -> "A>B>C": corrige encoding, recorta, descarta segmentos vacíos y
-- colapsa segmentos consecutivos iguales (Cangrejo repite el nivel cuando no lo usa).
CREATE OR REPLACE FUNCTION fn_normalizar_ruta_categoria(raw text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
    SELECT NULLIF(string_agg(y.seg, '>' ORDER BY y.ord), '')
    FROM (
        SELECT x.seg, x.ord
        FROM (
            SELECT regexp_replace(btrim(t.s), '\s+', ' ', 'g') AS seg,
                   t.ord,
                   lag(upper(regexp_replace(btrim(t.s), '\s+', ' ', 'g')))
                       OVER (ORDER BY t.ord) AS prev
            FROM unnest(string_to_array(fn_fix_encoding(raw), '>')) WITH ORDINALITY AS t(s, ord)
            WHERE btrim(t.s) <> ''
        ) x
        WHERE x.prev IS DISTINCT FROM upper(x.seg)
    ) y
$$;

-- Marca: recorta y colapsa espacios; vacío o NULL = BRANDLESS (igual que Cangrejo).
CREATE OR REPLACE FUNCTION fn_normalizar_marca(t text) RETURNS text
LANGUAGE sql IMMUTABLE AS $$
    SELECT COALESCE(NULLIF(regexp_replace(btrim(t), '\s+', ' ', 'g'), ''), 'BRANDLESS')
$$;

-- ---------------------------------------------------------------------------
-- marcas
-- ---------------------------------------------------------------------------
CREATE TABLE marcas (
    id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre     VARCHAR(255) NOT NULL,
    activo     BOOLEAN NOT NULL DEFAULT TRUE,   -- tiene productos (se recalcula en la reconciliación)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_marcas_nombre_upper ON marcas (upper(nombre));

-- ---------------------------------------------------------------------------
-- categorias: columnas del árbol (name queda hasta migrar los productos)
-- ---------------------------------------------------------------------------
ALTER TABLE categorias
    ADD COLUMN parent_id  INTEGER REFERENCES categorias(id),
    ADD COLUMN nombre     VARCHAR(255),
    ADD COLUMN nivel      SMALLINT,
    ADD COLUMN ruta       VARCHAR(1000),
    ADD COLUMN activo     BOOLEAN NOT NULL DEFAULT TRUE,   -- tiene productos propios o de descendientes
    ADD COLUMN created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now();
ALTER TABLE categorias ALTER COLUMN name DROP NOT NULL;
CREATE UNIQUE INDEX ux_categorias_ruta ON categorias (ruta);

-- Memoria del sync: texto crudo de Oracle -> nodo. Cada variante se normaliza una sola vez.
CREATE TABLE categoria_alias (
    ruta_origen  TEXT PRIMARY KEY,
    categoria_id INTEGER NOT NULL REFERENCES categorias(id)
);
CREATE INDEX ix_categoria_alias_categoria ON categoria_alias (categoria_id);

-- Crea los nodos que falten de una ruta ya normalizada y devuelve el id de la hoja.
CREATE OR REPLACE FUNCTION fn_asegurar_categoria(p_ruta text) RETURNS integer
LANGUAGE plpgsql AS $$
DECLARE
    segs   text[];
    i      integer;
    acc    text := '';
    parent integer := NULL;
    cur    integer := NULL;
BEGIN
    IF p_ruta IS NULL OR p_ruta = '' THEN
        RETURN NULL;
    END IF;
    segs := string_to_array(p_ruta, '>');
    FOR i IN 1 .. array_length(segs, 1) LOOP
        acc := CASE WHEN i = 1 THEN segs[1] ELSE acc || '>' || segs[i] END;
        SELECT id INTO cur FROM categorias WHERE ruta = acc;
        IF cur IS NULL THEN
            INSERT INTO categorias (parent_id, nombre, nivel, ruta)
            VALUES (parent, segs[i], i, acc)
            ON CONFLICT (ruta) DO NOTHING
            RETURNING id INTO cur;
            IF cur IS NULL THEN
                SELECT id INTO cur FROM categorias WHERE ruta = acc;
            END IF;
        END IF;
        parent := cur;
    END LOOP;
    RETURN cur;
END;
$$;

-- ---------------------------------------------------------------------------
-- Migrar datos: categorías con productos -> árbol; marcas distintas -> marcas
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    r    record;
    v_id integer;
BEGIN
    FOR r IN
        SELECT c.name
        FROM categorias c
        WHERE c.ruta IS NULL
          AND c.name IS NOT NULL
          AND EXISTS (SELECT 1 FROM productos p WHERE p.categoria_id = c.id)
    LOOP
        v_id := fn_asegurar_categoria(fn_normalizar_ruta_categoria(r.name));
        IF v_id IS NOT NULL THEN
            INSERT INTO categoria_alias (ruta_origen, categoria_id)
            VALUES (r.name, v_id)
            ON CONFLICT (ruta_origen) DO NOTHING;
        END IF;
    END LOOP;
END $$;

INSERT INTO marcas (nombre)
SELECT DISTINCT ON (upper(t.n)) t.n
FROM (SELECT fn_normalizar_marca(brand) AS n FROM productos GROUP BY 1) t
ORDER BY upper(t.n), (t.n = upper(t.n)) DESC, t.n;

INSERT INTO marcas (nombre) VALUES ('BRANDLESS') ON CONFLICT DO NOTHING;

-- id viejo de categoría -> id nuevo
CREATE TEMP TABLE map_categoria ON COMMIT DROP AS
SELECT o.id AS old_id, a.categoria_id AS new_id
FROM categorias o
JOIN categoria_alias a ON a.ruta_origen = o.name
WHERE o.ruta IS NULL;

-- ---------------------------------------------------------------------------
-- Reconstruir productos: marca_id reemplaza a brand, en su mismo lugar
-- (mismo patrón que la 008)
-- ---------------------------------------------------------------------------
CREATE TABLE productos_v9 (
    -- ===== Identificación =====
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code               VARCHAR(255)  NOT NULL UNIQUE,

    -- ===== Datos básicos del producto =====
    name_es            VARCHAR(255),
    name_pt            VARCHAR(255)  NOT NULL,
    marca_id           INTEGER NOT NULL REFERENCES marcas(id),
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

INSERT INTO productos_v9 (
    id, code, name_es, name_pt, marca_id, description_es, description_pt, categoria_id,
    regimen_turismo, price, precio_exenta, price_iva, stock,
    url_image, link, link_purchase, force_image_update,
    source_hash, sync_status, sync_compara_status,
    last_sent_at, last_sent_compara_at, last_error, last_error_compara,
    created_at, updated_at, last_changed_at
) OVERRIDING SYSTEM VALUE
SELECT
    p.id, p.code, p.name_es, p.name_pt, m.id, p.description_es, p.description_pt, mc.new_id,
    p.regimen_turismo, p.price, p.precio_exenta, p.price_iva, p.stock,
    p.url_image, p.link, p.link_purchase, p.force_image_update,
    p.source_hash, p.sync_status, p.sync_compara_status,
    p.last_sent_at, p.last_sent_compara_at, p.last_error, p.last_error_compara,
    p.created_at, p.updated_at, p.last_changed_at
FROM productos p
JOIN marcas m ON upper(m.nombre) = upper(fn_normalizar_marca(p.brand))
LEFT JOIN map_categoria mc ON mc.old_id = p.categoria_id;

-- Validaciones: no perder filas ni categorías
DO $$
BEGIN
    IF (SELECT count(*) FROM productos_v9) <> (SELECT count(*) FROM productos) THEN
        RAISE EXCEPTION '009: se perdieron productos (% nuevos vs % originales)',
            (SELECT count(*) FROM productos_v9), (SELECT count(*) FROM productos);
    END IF;
    IF (SELECT count(*) FROM productos_v9 WHERE categoria_id IS NOT NULL)
       <> (SELECT count(*) FROM productos WHERE categoria_id IS NOT NULL) THEN
        RAISE EXCEPTION '009: productos con categoría quedaron sin categoría';
    END IF;
END $$;

DROP TABLE productos;
ALTER TABLE productos_v9 RENAME TO productos;
ALTER TABLE productos RENAME CONSTRAINT productos_v9_pkey TO productos_pkey;
ALTER TABLE productos RENAME CONSTRAINT productos_v9_code_key TO productos_code_key;
ALTER TABLE productos RENAME CONSTRAINT productos_v9_marca_id_fkey TO productos_marca_id_fkey;
ALTER TABLE productos RENAME CONSTRAINT productos_v9_categoria_id_fkey TO productos_categoria_id_fkey;
ALTER SEQUENCE productos_v9_id_seq RENAME TO productos_id_seq;
SELECT setval(pg_get_serial_sequence('productos', 'id'), COALESCE(MAX(id), 1)) FROM productos;

-- ix_productos_code se omite: duplicaba el índice único de code.
CREATE INDEX ix_productos_sync_status ON productos (sync_status);
CREATE INDEX ix_productos_sync_compara_status ON productos (sync_compara_status);
CREATE INDEX ix_productos_regimen_turismo ON productos (regimen_turismo);
CREATE INDEX ix_productos_categoria_id ON productos (categoria_id);
CREATE INDEX ix_productos_marca_id ON productos (marca_id);

-- ---------------------------------------------------------------------------
-- Cerrar categorias: borrar filas viejas y quitar name
-- ---------------------------------------------------------------------------
DELETE FROM categorias WHERE ruta IS NULL;
ALTER TABLE categorias
    DROP COLUMN name,
    ALTER COLUMN nombre SET NOT NULL,
    ALTER COLUMN nivel  SET NOT NULL,
    ALTER COLUMN ruta   SET NOT NULL;
CREATE INDEX ix_categorias_parent ON categorias (parent_id);
CREATE UNIQUE INDEX ux_categorias_parent_nombre ON categorias (COALESCE(parent_id, 0), nombre);

-- ---------------------------------------------------------------------------
-- Auditoría: el historial guarda el NOMBRE de la marca previa; recrear el trigger
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_audit_producto() RETURNS trigger AS $$
BEGIN
    IF OLD.source_hash IS DISTINCT FROM NEW.source_hash THEN
        INSERT INTO productos_historial(
            producto_id, code, name_es, name_pt, price, price_iva,
            stock, brand, url_image, source_hash, sync_status, operation)
        VALUES (OLD.id, OLD.code, OLD.name_es, OLD.name_pt, OLD.price, OLD.price_iva,
                OLD.stock, (SELECT nombre FROM marcas WHERE id = OLD.marca_id),
                OLD.url_image, OLD.source_hash, OLD.sync_status, 'U');
    END IF;
    RETURN NEW;
END; $$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_audit_producto ON productos;
CREATE TRIGGER trg_audit_producto
    AFTER UPDATE ON productos
    FOR EACH ROW EXECUTE FUNCTION fn_audit_producto();

COMMIT;
