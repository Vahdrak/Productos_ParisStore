# Migraciones de base de datos (PostgreSQL)

Scripts SQL versionados y **ordenados por número**. Se aplican de menor a mayor y son **inmutables**
una vez ejecutados: cualquier cambio va en una migración nueva (`NNN_descripcion.sql`).

| # | Archivo | Qué hace |
|---|---|---|
| 001 | `001_create_core_tables.sql` | Tablas `categorias`, `productos`, `sync_runs` + índices |
| 002 | `002_audit_historial.sql` | Tabla `productos_historial` + función y trigger de auditoría |
| 009 | `009_normalizar_categorias_marcas.sql` | `categorias` pasa a árbol (`parent_id`, `ruta`), nuevas tablas `marcas` y `categoria_alias`, `productos.brand` → `marca_id`, recrea el trigger de auditoría |
| 010 | `010_historial_cambios.sql` | Historial con diff por campo (`cambios`), `origen`/`usuario`, índices de búsqueda (trigram) y archivo de historial viejo |
| 011 | `011_usuarios.sql` | Tabla `usuarios` (login del panel; contraseña con hash) |

## Cómo aplicarlas

Con `psql` (ajustar host/usuario/base):

```bash
psql -h localhost -U postgres -d comprasparaguai -f 001_create_core_tables.sql
psql -h localhost -U postgres -d comprasparaguai -f 002_audit_historial.sql
```

O abriendo cada archivo en **pgAdmin** y ejecutándolo en orden.

> Crear primero la base: `CREATE DATABASE comprasparaguai;`

## Probar el trigger de auditoría

```sql
-- 1. Insertar un producto
INSERT INTO productos (code, name_pt, price, stock, source_hash)
VALUES ('TEST-001', 'Produto de teste', 10.00, 5, 'hash_v1');

-- 2. Cambiarlo (nuevo source_hash) -> debe generar una fila en el historial
UPDATE productos SET price = 12.00, source_hash = 'hash_v2' WHERE code = 'TEST-001';

-- 3. Verificar que se guardó el ANTES (price = 10.00, source_hash = 'hash_v1')
SELECT code, price, source_hash, changed_at FROM productos_historial WHERE code = 'TEST-001';
```
