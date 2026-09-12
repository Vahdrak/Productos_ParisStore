# CLAUDE.md — Guía del proyecto para Claude Code

Contexto que Claude debe tener presente al trabajar en este repositorio.

## Qué es este proyecto

Servicio de sincronización de catálogo entre **Cangrejo** (Oracle, solo lectura) y el comparador
**Compras Paraguai** (API REST), con una base **PostgreSQL** propia como memoria/escudo. Incluye una
Web API de solo lectura y un front Angular de monitoreo.

**El plan maestro vive en `docs/Plan_Sincronizacion_ComprasParaguai.md`** — es la fuente de verdad
de decisiones, mapeo de campos, esquema de BD y fases. Consultarlo antes de implementar.

## Stack

- **Backend:** .NET 8 (Worker Service + Web API ASP.NET Core)
- **BD propia:** PostgreSQL (Npgsql + Dapper). Auditoría con trigger PL/pgSQL.
- **Origen:** Oracle vía `Oracle.ManagedDataAccess` (streaming, solo lectura)
- **Escritura masiva:** `Npgsql COPY` a staging + `INSERT ... ON CONFLICT DO UPDATE`
- **Front:** Angular (standalone components)
- **Resiliencia/Logs:** Polly, Serilog

## Reglas del proyecto

- **Secretos:** NUNCA hardcodear tokens ni cadenas de conexión. Cada worker los lee de su propio
  `.env` (junto al proyecto, ignorado por git; ver `.env.example`) o de variables de entorno.
  Claves con `__` como separador: `Compras__Token`, `ConnectionStrings__PostgreSQL`.
- **Campos "de Cangrejo" vs "propios":** los de Cangrejo se refrescan en cada sync; los propios
  (traducciones, descripciones, links) se **preservan y nunca se pisan**. Ver Sección 2.5 del plan.
- **BD:** convención `snake_case`. El `token` de la API va SIEMPRE en el **header**, nunca en la URL.
- **Nombres de proyecto:** prefijo `ComprasParaguai.*` para las librerías compartidas y la API (`Core`, `Infrastructure`, `Api`). Los workers se llaman `Worker.<Rol>` (`Worker.Sync`, `Worker.Compras`, `Worker.Compara`) para que el nombre diga qué hacen.
- **Dos destinos, dos columnas de estado:** `sync_status`/`last_sent_at`/`last_error` son de **Compras** Paraguai; `sync_compara_status`/`last_sent_compara_at`/`last_error_compara` son de **Compara** Paraguai. `Worker.Sync` pone ambas en 0 al detectar cambio; cada worker de envío solo lee y escribe la suya (`SyncTarget`).

## Convenciones de código

- C#: 4 espacios, llaves en línea nueva, `System.*` primero. Ver `.editorconfig`.
- TS/HTML/JSON: 2 espacios.
- Migraciones SQL: numeradas y ordenadas en `db/migrations/` (`NNN_descripcion.sql`), inmutables una
  vez aplicadas (los cambios van en una migración nueva).

## Organización de carpetas

- `src/ComprasParaguai.Core` — dominio puro, sin infraestructura.
- `src/ComprasParaguai.Infrastructure` — Oracle, PostgreSQL, cliente HTTP.
- `src/Worker.Sync` — Oracle → PostgreSQL: carga inicial + detección de cambios por hash cada 2 h. No envía a APIs.
- `src/Worker.Compras` — envía pendientes a Compras Paraguai (credenciales `Compras:*`).
- `src/Worker.Compara` — envía pendientes a Compara Paraguai (credenciales `Compara:*`).
- `src/ComprasParaguai.Api` — endpoints de solo lectura para el front.
- `frontend/comprasparaguai-front` — Angular.
- `tests/` — unitarias e integración.

## `.claude/` — agentes, skills y comandos

Este repo está preparado para extenderse con automatizaciones de Claude Code:
- `.claude/agents/` — subagentes especializados del proyecto (ej. revisor de mapeo, generador de
  migraciones).
- `.claude/skills/` — skills reutilizables (ej. crear una nueva migración, validar payload de la API).
- `.claude/commands/` — slash commands del proyecto.

Ver `.claude/README.md` para el formato de cada uno.
