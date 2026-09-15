# Compras Paraguai — Sincronización de catálogo

Servicio que mantiene actualizado el catálogo de **Toku Importados** en el comparador
**Compras Paraguai**, leyendo productos desde el sistema **Cangrejo** (Oracle, solo lectura),
guardándolos en una base **PostgreSQL** propia y enviando solo los cambios a la API del comparador.

> 📋 El plan completo del proyecto está en
> [`docs/Plan_Sincronizacion_ComprasParaguai.md`](docs/Plan_Sincronizacion_ComprasParaguai.md).

---

## Arquitectura

```
Cangrejo (Oracle)          BD propia (PostgreSQL)              Compras Paraguai
  vista de productos  ──►   copia + hash + auditoría   ──POST──►  API /products/import
   (~109k, read-only)        + Worker de sync                       (token en header)
                                   │
                                   ├── Web API (solo lectura)
                                   └── Front Angular (monitoreo + diff antes/después)
```

## Estructura del repositorio

| Carpeta | Contenido |
|---|---|
| `backend/ComprasParaguai.Core` | Modelos de dominio, contratos/interfaces, lógica de mapeo y hash. Sin dependencias de infraestructura. |
| `backend/ComprasParaguai.Infrastructure` | Acceso a datos: lector Oracle (Cangrejo), repositorios PostgreSQL (Npgsql), cliente HTTP de la API. |
| `workers/Worker.Sync` | Oracle → PostgreSQL: carga inicial + detección de cambios por hash cada 2 h, reconciliación diaria. No envía a APIs. |
| `workers/Worker.Compras` | Envía pendientes a **Compras** Paraguai (lotes de 500). Credenciales propias en `.env`. |
| `workers/Worker.Compara` | Envía pendientes a **Compara** Paraguai (lotes de 1000, rate limit). Credenciales propias en `.env`. |
| `backend/ComprasParaguai.Api` | Web API ASP.NET Core de **solo lectura** que alimenta el front. |
| `frontend/comprasparaguai-front` | App **Angular** de monitoreo (dashboard, productos, diff antes/después). |
| `db/migrations` | Scripts SQL versionados (tablas, índices, trigger de auditoría). |
| `db/seed` | Datos de ejemplo para pruebas locales. |
| `tests/` | Pruebas unitarias e de integración. |
| `docs/` | Plan del proyecto, guías (`Guia_Envio_Productos.md`, `SETUP.md`, `PUESTA_EN_MARCHA.md`), spec de la API (`api/`). |
| `scripts/` | `.bat`/`.ps1` para levantar API, front y workers en local. |
| `.claude/` | Configuración de Claude Code: agentes, skills y comandos del proyecto. |

## Prerequisitos

- **.NET SDK 8** — `dotnet --version` (probado con 8.0.422)
- **Node 20+ / npm** — para el front (probado con Node 24 / npm 11)
- **Angular CLI** — se usa vía `npx @angular/cli` (no requiere instalación global)
- **PostgreSQL 14+** — base de datos propia
- **Cliente PostgreSQL** — `psql` o **pgAdmin** para correr las migraciones de `db/migrations`
- Acceso a Oracle (Cangrejo) — cadena de conexión de solo lectura

## Puesta en marcha (una vez implementado)

```bash
# 1. Base de datos: correr las migraciones en orden contra tu PostgreSQL
#    (con psql o pgAdmin) — ver db/migrations/README.md

# 2. Backend
dotnet restore
dotnet build

# 3. Workers (cada uno lee su .env; ver docs/Guia_Envio_Productos.md)
dotnet run --project workers/Worker.Sync -- --once   # carga inicial Oracle → BD
dotnet run --project workers/Worker.Sync             # servicio cada 2 h
dotnet run --project workers/Worker.Compras          # envía a Compras cada 2 h
dotnet run --project workers/Worker.Compara          # envía a Compara cada 2 h

# 4. Web API (para el front)
dotnet run --project backend/ComprasParaguai.Api

# 5. Front Angular
cd frontend/comprasparaguai-front
npm install
npm start
```

> ⚠️ **Secretos:** el token de Compras Paraguai y las cadenas de conexión (Oracle/PostgreSQL) van en
> **User Secrets** (dev) o **variables de entorno** (prod). Nunca en el código ni en git.

## Estado

Estructura del proyecto creada. La implementación sigue el plan por fases documentado en `docs/`.
