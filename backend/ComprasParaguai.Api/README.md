# ComprasParaguai.Api

Web API ASP.NET Core de **solo lectura** que alimenta el front Angular. Lee la BD propia
(Npgsql + Dapper). **No** reenvía nada a Compras Paraguai, así que no maneja su token.

Endpoints previstos (ver Sección 11 del plan):

| Endpoint | Para qué |
|---|---|
| `GET /api/productos` | Lista paginada. Filtros: `sync_status`, `code`, `name`, `changed`. |
| `GET /api/productos/{code}` | Detalle actual. |
| `GET /api/productos/{code}/historial` | Versiones previas (auditoría). |
| `GET /api/productos/{code}/diff` | Actual vs última del historial → `campo → {antes, después}`. |
| `GET /api/sync-runs` | Bitácora de corridas. |
| `GET /api/dashboard` | Contadores: pendientes / enviados / error + última corrida. |

CORS habilitado para el origen del front.

Scaffold:
```bash
dotnet new webapi -n ComprasParaguai.Api -o backend/ComprasParaguai.Api
```
