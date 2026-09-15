# Worker.Sync

Sincroniza la vista Oracle (`SENSEI.PARAGUAY_COMPRAS_VIEW`) con la BD propia (PostgreSQL). **No envía a ninguna API.**

- **Sin argumentos**: queda corriendo; ejecuta un ciclo al arrancar y luego cada `Sync:IntervalMinutes` (default 120 = 2 h). Una vez por día corre la reconciliación de bajas.
- **`--once`**: un ciclo de sync + reconciliación y sale. Usar para la carga inicial o para forzar una corrida.

Cada ciclo: leer vista → mapear/validar → hash → comparar con BD → upsert de nuevos/modificados → **ambas** columnas de estado (`sync_status`, `sync_compara_status`) quedan en 0 (Pendiente) → bitácora en `sync_runs`.

Config: `ConnectionStrings:Oracle`, `ConnectionStrings:PostgreSQL` (User Secrets / env), `Sync:*`.

```bash
dotnet run --project workers/Worker.Sync -- --once   # carga inicial / corrida manual
dotnet run --project workers/Worker.Sync             # servicio cada 2 h
```
