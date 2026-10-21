-- ============================================================================
-- 013_envio_errores.sql
-- Historial de errores de envío por producto, normalizado:
--   * error_mensajes : cada mensaje distinto UNA sola vez (hay miles de productos con el mismo texto).
--   * envio_errores  : un fallo de un producto en un destino y etapa; si se repite el mismo fallo no se
--                      agrega otra fila, se suma a `intentos` y se actualiza `ultimo_at`.
--                      `resuelto_at` se llena cuando un envío posterior sale bien.
-- El tipo de error (duplicado, imagen, precio...) NO se guarda: se deduce del mensaje (ErrorClasificador).
-- La marca/categoría del producto tampoco se copia: se obtienen con JOIN.
-- productos.last_error y last_error_compara repetían el error vigente: se copian a las tablas nuevas y se eliminan.
-- ============================================================================

BEGIN;

CREATE TABLE error_mensajes (
    id      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mensaje TEXT NOT NULL
);
-- Índice por md5: los mensajes pueden ser largos (cuerpos de respuesta HTTP)
CREATE UNIQUE INDEX ux_error_mensajes_md5 ON error_mensajes (md5(mensaje));

CREATE TABLE envio_errores (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id INTEGER     NOT NULL REFERENCES productos(id) ON DELETE CASCADE,
    target      VARCHAR(20) NOT NULL CHECK (target IN ('compras', 'compara')),
    etapa       VARCHAR(10) NOT NULL DEFAULT 'envio' CHECK (etapa IN ('envio', 'retiro')),
    mensaje_id  INTEGER     NOT NULL REFERENCES error_mensajes(id),
    desde       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),   -- primer fallo
    ultimo_at   TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),   -- último fallo (igual al primero si no se repitió)
    intentos    INTEGER     NOT NULL DEFAULT 1,
    resuelto_at TIMESTAMP WITHOUT TIME ZONE                           -- NULL = sigue abierto
);
-- Un solo fallo abierto por (producto, destino, etapa, mensaje): si se repite se suma a intentos
CREATE UNIQUE INDEX ux_envio_errores_abierto ON envio_errores (producto_id, target, etapa, mensaje_id) WHERE resuelto_at IS NULL;
CREATE INDEX ix_envio_errores_producto ON envio_errores (producto_id, desde DESC);
CREATE INDEX ix_envio_errores_abiertos ON envio_errores (target, ultimo_at DESC) WHERE resuelto_at IS NULL;
CREATE INDEX ix_envio_errores_mensaje ON envio_errores (mensaje_id);

-- ---------------------------------------------------------------------------
-- Copiar los errores vigentes (productos en Error) antes de eliminar las columnas.
-- El prefijo "Retiro: " pasa a ser la columna etapa.
-- ---------------------------------------------------------------------------
INSERT INTO error_mensajes (mensaje)
SELECT DISTINCT regexp_replace(m, '^Retiro:\s*', '')
FROM (
    SELECT last_error AS m FROM productos WHERE sync_status = 2 AND last_error IS NOT NULL
    UNION
    SELECT last_error_compara FROM productos WHERE sync_compara_status = 2 AND last_error_compara IS NOT NULL
) t
ON CONFLICT DO NOTHING;

INSERT INTO envio_errores (producto_id, target, etapa, mensaje_id, desde, ultimo_at)
SELECT p.id, 'compras',
       CASE WHEN p.last_error ~ '^Retiro:' THEN 'retiro' ELSE 'envio' END,
       em.id, p.updated_at, p.updated_at
FROM productos p
JOIN error_mensajes em ON md5(em.mensaje) = md5(regexp_replace(p.last_error, '^Retiro:\s*', ''))
WHERE p.sync_status = 2 AND p.last_error IS NOT NULL;

INSERT INTO envio_errores (producto_id, target, etapa, mensaje_id, desde, ultimo_at)
SELECT p.id, 'compara',
       CASE WHEN p.last_error_compara ~ '^Retiro:' THEN 'retiro' ELSE 'envio' END,
       em.id, p.updated_at, p.updated_at
FROM productos p
JOIN error_mensajes em ON md5(em.mensaje) = md5(regexp_replace(p.last_error_compara, '^Retiro:\s*', ''))
WHERE p.sync_compara_status = 2 AND p.last_error_compara IS NOT NULL;

-- Validación: cada producto en Error quedó con su fallo abierto
DO $$
BEGIN
    IF (SELECT count(*) FROM productos WHERE sync_status = 2 AND last_error IS NOT NULL)
       <> (SELECT count(*) FROM envio_errores WHERE target = 'compras' AND resuelto_at IS NULL) THEN
        RAISE EXCEPTION '013: los errores de Compras no se copiaron completos';
    END IF;
    IF (SELECT count(*) FROM productos WHERE sync_compara_status = 2 AND last_error_compara IS NOT NULL)
       <> (SELECT count(*) FROM envio_errores WHERE target = 'compara' AND resuelto_at IS NULL) THEN
        RAISE EXCEPTION '013: los errores de Compara no se copiaron completos';
    END IF;
END $$;

ALTER TABLE productos
    DROP COLUMN last_error,
    DROP COLUMN last_error_compara;

COMMIT;
