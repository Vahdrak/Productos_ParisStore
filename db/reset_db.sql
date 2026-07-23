-- Script para resetear la base de datos PostgreSQL
-- Elimina todas las conexiones y luego elimina la BD

SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = 'comprasparaguai'
  AND pid <> pg_backend_pid();

DROP DATABASE IF EXISTS comprasparaguai;

CREATE DATABASE comprasparaguai
  WITH ENCODING 'UTF8'
  LC_COLLATE 'en_US.UTF-8'
  LC_CTYPE 'en_US.UTF-8';

-- Conectar a la nueva BD
\c comprasparaguai

-- Crear extensiones necesarias (si las usas)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Ahora ejecutar las migraciones desde 001_create_core_tables.sql
