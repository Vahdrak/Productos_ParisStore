# Guía de operación: los 3 workers

```
Oracle (vista) ──► Worker.Sync (cada 2 h) ──► PostgreSQL productos
                                                 sync_status = 0          ──► Worker.Compras ──► API Compras Paraguai
                                                 sync_compara_status = 0  ──► Worker.Compara ──► API Compara Paraguai
```

| Worker | Qué hace | Columnas que escribe | Config |
|---|---|---|---|
| `Worker.Sync` | Lee la vista Oracle, compara hash por SKU, inserta nuevos / actualiza modificados. Una vez al día marca bajas. **No envía a ninguna API.** | datos del producto + `sync_status = 0` + `sync_compara_status = 0` | `ConnectionStrings:Oracle`, `ConnectionStrings:PostgreSQL`, `Sync:IntervalMinutes` (120) |
| `Worker.Compras` | Envía pendientes a Compras Paraguai en lotes de 500. | `sync_status`, `last_sent_at`, `last_error` | `ConnectionStrings:PostgreSQL`, `Compras:ApiUrl`, `Compras:Token`, `Compras:IntervalHours` (2) |
| `Worker.Compara` | Envía pendientes a Compara Paraguai en lotes de 1000 con 900 ms de pausa (límite 80 req/h). | `sync_compara_status`, `last_sent_compara_at`, `last_error_compara` | `ConnectionStrings:PostgreSQL`, `Compara:ApiUrl`, `Compara:Token`, `Compara:IntervalHours` (2) |

Los tres se comportan igual: **sin argumentos quedan corriendo como servicio** (timer interno); **con un comando ejecutan eso una vez y salen**.

## Comandos

### Worker.Sync

```bash
dotnet run --project src/Worker.Sync -- --once   # un ciclo (carga inicial o forzar sync) y sale
dotnet run --project src/Worker.Sync             # servicio: sync al arrancar y cada 2 h
```

La "carga inicial" no es un modo aparte: con la BD vacía, el primer ciclo inserta todo; los siguientes solo tocan nuevos y modificados (por `source_hash`).

### Worker.Compras / Worker.Compara

Mismos comandos, cada uno contra su API y su columna:

```bash
dotnet run --project src/Worker.Compras -- --check-pending   # cuántos pendientes hay (no envía)
dotnet run --project src/Worker.Compras -- --test-batch      # 500 al azar, envío real de prueba
dotnet run --project src/Worker.Compras -- --send-all        # TODOS los productos (primera vez)
dotnet run --project src/Worker.Compras -- --send-pending    # solo pendientes, una vez
dotnet run --project src/Worker.Compras                      # servicio: pendientes cada 2 h
```

Reemplazar `Worker.Compras` por `Worker.Compara` para el otro destino.

## Puesta en marcha (orden recomendado)

```bash
# 1. Cargar la BD desde Oracle
dotnet run --project src/Worker.Sync -- --once

# 2. Probar cada API con 500 productos
dotnet run --project src/Worker.Compras -- --test-batch
dotnet run --project src/Worker.Compara -- --test-batch

# 3. Envío completo inicial a cada destino
dotnet run --project src/Worker.Compras -- --send-all
dotnet run --project src/Worker.Compara -- --send-all      # ~108 lotes × 0.9 s ≈ 2 min

# 4. Dejar los tres corriendo (tres consolas, o tres servicios de Windows)
dotnet run --project src/Worker.Sync
dotnet run --project src/Worker.Compras
dotnet run --project src/Worker.Compara
```

## Credenciales

Nunca en `appsettings.json`. Opciones, en orden de preferencia:

```bash
# User Secrets (dev)
dotnet user-secrets set "Compras:Token" "..." --project src/Worker.Compras
dotnet user-secrets set "Compara:Token" "..." --project src/Worker.Compara

# Variables de entorno (prod)
Compras__Token=...   Compara__Token=...   ConnectionStrings__PostgreSQL=...
```

`appsettings.Development.json` también se lee en dev (`DOTNET_ENVIRONMENT=Development`).

## Flujo de estado

Cada columna de estado vale `0 = Pendiente`, `1 = Enviado`, `2 = Error`.

1. `Worker.Sync` detecta cambio en un SKU → `sync_status = 0` **y** `sync_compara_status = 0`.
2. `Worker.Compras` lee `sync_status = 0`, envía, marca `sync_status = 1` (o `2` + `last_error`).
3. `Worker.Compara` lee `sync_compara_status = 0`, envía, marca `sync_compara_status = 1` (o `2` + `last_error_compara`).

Los dos workers de envío no se pisan: cada uno solo toca su columna.

## Monitoreo

```sql
-- Estado por destino
SELECT sync_status, sync_compara_status, COUNT(*) FROM productos GROUP BY 1, 2 ORDER BY 1, 2;

-- Errores recientes de Compara
SELECT code, last_error_compara, updated_at FROM productos
WHERE sync_compara_status = 2 ORDER BY updated_at DESC LIMIT 20;

-- Corridas del Sync
SELECT * FROM sync_runs ORDER BY started_at DESC LIMIT 10;
```

El front Angular / `ComprasParaguai.Api` muestran el estado de **Compras** (`sync_status`).

## Troubleshooting

**`HTTP TooManyRequests` en Compara** — límite de 80 req/h. `SendOptions.ComparaParaguai` ya usa 1000 por lote + 900 ms. Si aparece, esperar una hora y correr `--send-pending`: los que quedaron en `2` no se reintentan solos (hay que ponerlos en `0` a mano: `UPDATE productos SET sync_compara_status = 0 WHERE sync_compara_status = 2;`).

**`HTTP Unauthorized`** — token incorrecto o ausente. Ver sección Credenciales. El header es `Authorization: Bearer {token}`.

**`--send-pending` no envía nada** — `--check-pending` da 0. Si esperabas cambios, verificar que `Worker.Sync` corrió (`sync_runs`) y que la vista tiene datos distintos.

**Datos del 2026-09-11** — el primer `--send-all` a Compara marcó la columna de Compras por error. Corrección one-shot en `db/scripts/2026-09-11_fix_send_all_compara.sql` (requiere migración 005, que `DbMigrator` aplica solo al arrancar cualquier worker).
