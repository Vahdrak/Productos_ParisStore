-- ============================================================================
-- 012_envio_reglas.sql
-- Reglas de envío por destino (Compras / Compara):
--   * envio_pausas      : retiene el envío por marca, por categoría (con su subárbol) o por la
--                         combinación marca + categoría. Opción "retirar del destino" (stock 0).
--   * envio_excepciones : excepción por producto (SKU): 'enviar' o 'retener'; gana sobre las pausas.
--   * envio_retirados   : productos que ya se retiraron del destino (se les envió stock 0).
--   * envio_reglas_historial : quién creó/cambió/activó/desactivó/borró cada regla (lo llenan triggers
--                         con el usuario de la sesión: app.usuario).
-- Regla efectiva: excepción activa 'enviar' => se envía; 'retener' => retenido; si no hay excepción,
-- retenido si ALGUNA pausa activa del destino lo cubre. Mientras está retenido el producto se sigue
-- actualizando en la BD pero no se envía; al liberar se envía con los datos más recientes.
-- ============================================================================

BEGIN;

CREATE TABLE envio_pausas (
    id                  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    target              VARCHAR(20) NOT NULL CHECK (target IN ('compras', 'compara')),
    marca_id            INTEGER REFERENCES marcas(id),
    categoria_id        INTEGER REFERENCES categorias(id),     -- cubre todo el subárbol
    retirar_del_destino BOOLEAN NOT NULL DEFAULT FALSE,        -- además de dejar de enviar, enviar stock 0 a lo ya publicado
    activo              BOOLEAN NOT NULL DEFAULT TRUE,         -- se apaga y prende por temporada sin perder la regla
    motivo              TEXT,                                  -- ej. "Temporada verano"
    created_by          VARCHAR(100),
    updated_by          VARCHAR(100),
    created_at          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    updated_at          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    CHECK (marca_id IS NOT NULL OR categoria_id IS NOT NULL)
);
CREATE UNIQUE INDEX ux_envio_pausas_regla
    ON envio_pausas (target, COALESCE(marca_id, 0), COALESCE(categoria_id, 0));
CREATE INDEX ix_envio_pausas_activas ON envio_pausas (target) WHERE activo;

CREATE TABLE envio_excepciones (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    target      VARCHAR(20) NOT NULL CHECK (target IN ('compras', 'compara')),
    producto_id INTEGER NOT NULL REFERENCES productos(id) ON DELETE CASCADE,
    accion      VARCHAR(10) NOT NULL CHECK (accion IN ('enviar', 'retener')),
    activo      BOOLEAN NOT NULL DEFAULT TRUE,
    motivo      TEXT,
    created_by  VARCHAR(100),
    updated_by  VARCHAR(100),
    created_at  TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    updated_at  TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    UNIQUE (target, producto_id)
);

CREATE TABLE envio_retirados (
    producto_id INTEGER NOT NULL REFERENCES productos(id) ON DELETE CASCADE,
    target      VARCHAR(20) NOT NULL CHECK (target IN ('compras', 'compara')),
    retirado_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    PRIMARY KEY (producto_id, target)
);

CREATE TABLE envio_reglas_historial (
    id         BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tabla      VARCHAR(12) NOT NULL CHECK (tabla IN ('pausa', 'excepcion')),
    regla_id   INTEGER NOT NULL,
    accion     VARCHAR(12) NOT NULL CHECK (accion IN ('crear', 'editar', 'activar', 'desactivar', 'borrar')),
    detalle    TEXT,                    -- texto legible: "compara: LACOSTE en TOKU IMPORTADOS>INDUMENTARIA"
    antes      JSONB,
    despues    JSONB,
    usuario    VARCHAR(100),
    changed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_envio_reglas_hist_fecha ON envio_reglas_historial (changed_at DESC);
CREATE INDEX ix_envio_reglas_hist_regla ON envio_reglas_historial (tabla, regla_id);

-- ---------------------------------------------------------------------------
-- Trigger: cada cambio de una regla queda en envio_reglas_historial con el usuario de la sesión
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_audit_envio_regla() RETURNS trigger AS $$
DECLARE
    v_usuario text := NULLIF(current_setting('app.usuario', true), '');
    v_tabla   text := CASE TG_TABLE_NAME WHEN 'envio_pausas' THEN 'pausa' ELSE 'excepcion' END;
    v_antes   jsonb;
    v_despues jsonb;
    v_accion  text;
    v_json    jsonb;
    v_detalle text;
BEGIN
    IF TG_OP = 'DELETE' THEN
        v_antes := to_jsonb(OLD);
        v_accion := 'borrar';
    ELSIF TG_OP = 'INSERT' THEN
        v_despues := to_jsonb(NEW);
        v_accion := 'crear';
    ELSE
        v_antes := to_jsonb(OLD);
        v_despues := to_jsonb(NEW);
        v_accion := CASE
            WHEN OLD.activo IS DISTINCT FROM NEW.activo THEN CASE WHEN NEW.activo THEN 'activar' ELSE 'desactivar' END
            ELSE 'editar' END;
    END IF;

    v_json := COALESCE(v_despues, v_antes);
    IF v_tabla = 'pausa' THEN
        v_detalle := (v_json->>'target') || ': ' || concat_ws(' en ',
            (SELECT nombre FROM marcas WHERE id = (v_json->>'marca_id')::int),
            (SELECT ruta FROM categorias WHERE id = (v_json->>'categoria_id')::int));
        IF (v_json->>'retirar_del_destino')::boolean THEN
            v_detalle := v_detalle || ' (retirar del destino)';
        END IF;
    ELSE
        v_detalle := (v_json->>'target') || ': SKU '
            || COALESCE((SELECT code FROM productos WHERE id = (v_json->>'producto_id')::int), '?')
            || ' (' || (v_json->>'accion') || ')';
    END IF;

    INSERT INTO envio_reglas_historial (tabla, regla_id, accion, detalle, antes, despues, usuario)
    VALUES (v_tabla, (v_json->>'id')::int, v_accion, v_detalle, v_antes, v_despues, v_usuario);

    RETURN NULL;
END; $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_envio_pausas
    AFTER INSERT OR UPDATE OR DELETE ON envio_pausas
    FOR EACH ROW EXECUTE FUNCTION fn_audit_envio_regla();

CREATE TRIGGER trg_audit_envio_excepciones
    AFTER INSERT OR UPDATE OR DELETE ON envio_excepciones
    FOR EACH ROW EXECUTE FUNCTION fn_audit_envio_regla();

COMMIT;
