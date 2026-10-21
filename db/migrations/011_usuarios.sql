-- ============================================================================
-- 011_usuarios.sql
-- Usuarios del panel (login para las operaciones de escritura de la API).
-- Contraseña con hash (PasswordHasher de ASP.NET Core); nunca en texto plano.
-- El primer usuario se crea al arrancar la API desde Auth__AdminUsuario /
-- Auth__AdminPassword si la tabla está vacía.
-- ============================================================================

BEGIN;

CREATE TABLE usuarios (
    id            INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario       VARCHAR(50)  NOT NULL,              -- nombre de login
    nombre        VARCHAR(100) NOT NULL,              -- nombre para mostrar
    password_hash TEXT         NOT NULL,
    activo        BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    last_login_at TIMESTAMP WITHOUT TIME ZONE
);
CREATE UNIQUE INDEX ux_usuarios_usuario ON usuarios (lower(usuario));

COMMIT;
