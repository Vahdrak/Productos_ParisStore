-- ============================================================================
-- 015_verify_solicitudes.sql
-- Cola de pedidos para Worker.Verify. El panel (API) no corre el worker ni conoce el token de Compras:
-- solo deja una fila 'pendiente'; el contenedor worker-verify (--servicio) la toma, la ejecuta y escribe el resultado.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS verify_solicitudes (
    id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo           VARCHAR(20)  NOT NULL CHECK (tipo IN ('disponibilidad', 'muestra')),
    estado         VARCHAR(12)  NOT NULL DEFAULT 'pendiente' CHECK (estado IN ('pendiente', 'corriendo', 'ok', 'error')),
    origen         VARCHAR(10)  NOT NULL DEFAULT 'panel' CHECK (origen IN ('panel', 'auto')),
    solicitado_por VARCHAR(100),
    solicitado_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    iniciado_at    TIMESTAMPTZ,
    terminado_at   TIMESTAMPTZ,
    resultado      TEXT,          -- resumen legible cuando termina bien
    error          TEXT           -- mensaje cuando falla o se interrumpe
);

-- A lo sumo un pedido abierto por tipo: si alguien aprieta el botón dos veces, el segundo reutiliza el primero.
CREATE UNIQUE INDEX IF NOT EXISTS ux_verify_solicitudes_abierta
    ON verify_solicitudes (tipo) WHERE estado IN ('pendiente', 'corriendo');

CREATE INDEX IF NOT EXISTS ix_verify_solicitudes_fecha ON verify_solicitudes (solicitado_at DESC);

COMMIT;
