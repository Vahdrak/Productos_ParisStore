-- Agregar soporte para Régimen de Turismo
-- Migración: agregar columnas de régimen turismo y precio exenta

ALTER TABLE productos
ADD COLUMN IF NOT EXISTS regimen_turismo BOOLEAN DEFAULT false,
ADD COLUMN IF NOT EXISTS precio_exenta DECIMAL(12,2);

CREATE INDEX IF NOT EXISTS ix_productos_regimen_turismo
ON productos(regimen_turismo);
