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
| `Worker.Compara` | Envía pendientes a Compara Paraguai en lotes de 1000 con **46 s** de pausa (límite de la API: 80 requests por hora). Ante un 429 espera 5 min y reintenta el lote. | `sync_compara_status`, `last_sent_compara_at`, `last_error_compara` | `ConnectionStrings:PostgreSQL`, `Compara:ApiUrl`, `Compara:Token`, `Compara:IntervalHours` (2) |

Los tres se comportan igual: **sin argumentos quedan corriendo como servicio** (timer interno); **con un comando ejecutan eso una vez y salen**.

Hay un cuarto ejecutable, `Worker.Verify`, que no envía nada: comprueba contra la API que lo marcado como Enviado realmente está (ver más abajo).

## Comandos

### Worker.Sync

```bash
dotnet run --project workers/Worker.Sync -- --once   # un ciclo (carga inicial o forzar sync) y sale
dotnet run --project workers/Worker.Sync             # servicio: sync al arrancar y cada 2 h
```

La "carga inicial" no es un modo aparte: con la BD vacía, el primer ciclo inserta todo; los siguientes solo tocan nuevos y modificados (por `source_hash`).

### Worker.Compras / Worker.Compara

Mismos comandos, cada uno contra su API y su columna:

```bash
dotnet run --project workers/Worker.Compras -- --check-pending   # cuántos pendientes hay (no envía)
dotnet run --project workers/Worker.Compras -- --test-batch      # 500 al azar, envío real de prueba
dotnet run --project workers/Worker.Compras -- --send-all        # TODOS los productos (primera vez)
dotnet run --project workers/Worker.Compras -- --send-pending    # solo pendientes, una vez
dotnet run --project workers/Worker.Compras                      # servicio: pendientes cada 2 h
```

Reemplazar `Worker.Compras` por `Worker.Compara` para el otro destino.

### Worker.Verify (comprobar que llegaron)

```bash
dotnet run --project workers/Worker.Verify -- --compras        # 500 al azar marcados Enviado a Compras
dotnet run --project workers/Worker.Verify -- --compras 50     # o la cantidad que quieras
```

Toma N productos con `sync_status = 1`, hace `GET /api/products/list/?code=…` por cada uno y escribe `verify-output/verify-comprasparaguai-<fecha>.csv` con: encontrado SI/NO, precio y stock local vs API, disponible, `updated_at` de la API. En consola resume `X/N encontrados`. Si un código aparece en la API, el envío llegó. Config propia: `.env` con `ConnectionStrings__PostgreSQL` y `Compras__Token`. Compara no ofrece un GET por código, así que por ahora solo verifica Compras.

## Puesta en marcha (orden recomendado)

```bash
# 1. Cargar la BD desde Oracle
dotnet run --project workers/Worker.Sync -- --once

# 2. Probar cada API con 500 productos
dotnet run --project workers/Worker.Compras -- --test-batch
dotnet run --project workers/Worker.Compara -- --test-batch

# 3. Envío completo inicial a cada destino
dotnet run --project workers/Worker.Compras -- --send-all
dotnet run --project workers/Worker.Compara -- --send-all      # ~109 lotes × 46 s ≈ 85 min (límite 80 req/h)

# 4. Dejar los tres corriendo (tres consolas, o tres servicios de Windows)
dotnet run --project workers/Worker.Sync
dotnet run --project workers/Worker.Compras
dotnet run --project workers/Worker.Compara
```

## Credenciales: un `.env` por worker

Cada worker lee un archivo `.env` que está al lado del ejecutable (se copia desde la carpeta del proyecto al compilar). **`.env` está en `.gitignore`**; lo que se commitea es `.env.example`.

```
workers/Worker.Sync/.env       ConnectionStrings__Oracle, ConnectionStrings__PostgreSQL
workers/Worker.Compras/.env    ConnectionStrings__PostgreSQL, Compras__Token
workers/Worker.Compara/.env    ConnectionStrings__PostgreSQL, Compara__Token
workers/Worker.Verify/.env     ConnectionStrings__PostgreSQL, Compras__Token
```

Formato `KEY=VALUE`, una por línea, `#` para comentarios. Las secciones se separan con `__` (`Compras__Token` = `Compras:Token`). Para empezar en otra máquina:

```bash
cp workers/Worker.Compras/.env.example workers/Worker.Compras/.env   # y completar
```

Una variable de entorno real del sistema tiene prioridad sobre el `.env` (útil en prod). `appsettings.json` solo tiene lo no secreto (URLs, intervalos).

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

**`HTTP TooManyRequests` en Compara** — límite de 80 requests por hora (no por segundo). `SendOptions.ComparaParaguai` usa 1000 por lote + 46 s de pausa, así que en condiciones normales no aparece; si otro proceso consumió la cuota, el worker espera 5 min y reintenta el lote hasta 3 veces antes de marcarlo en Error. Los que igual queden en `2` no se reintentan solos: `UPDATE productos SET sync_compara_status = 0 WHERE sync_compara_status = 2;` y volver a correr `--send-pending`.

**`HTTP Unauthorized`** — token incorrecto, ausente o en el header equivocado. Cada API autentica distinto: **Compras** usa un header propio `token: <valor>` (si se manda como `Authorization: Bearer` responde "Token is invalid or expired"); **Compara** usa `Authorization: Bearer <valor>`. Ver sección Credenciales.

**`--send-pending` no envía nada** — `--check-pending` da 0. Si esperabas cambios, verificar que `Worker.Sync` corrió (`sync_runs`) y que la vista tiene datos distintos.

**Datos del 2026-09-11** — el primer `--send-all` a Compara marcó la columna de Compras por error. Corrección one-shot en `db/scripts/2026-09-11_fix_send_all_compara.sql` (requiere migración 005, que `DbMigrator` aplica solo al arrancar cualquier worker).
