-- Script para registrar migraciones ya aplicadas
-- Ejecutar UNA SOLA VEZ cuando necesites indicarle cuáles migraciones ya están en la BD

-- Crear tabla de control si no existe
CREATE TABLE IF NOT EXISTS schema_migrations (
    id SERIAL PRIMARY KEY,
    filename VARCHAR(255) NOT NULL UNIQUE,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Registrar migraciones que ya existen
INSERT INTO schema_migrations (filename) VALUES
    ('001_create_core_tables.sql'),
    ('002_add_audit_trigger.sql'),
    ('003_add_sync_status_columns.sql'),
    ('004_add_error_tracking.sql'),
    ('005_add_regimen_columns.sql'),
    ('006_add_compara_sync_status.sql'),
    ('007_add_regimen_turismo_columns.sql')
ON CONFLICT (filename) DO NOTHING;

-- Verificar
SELECT COUNT(*) as migraciones_registradas FROM schema_migrations;
