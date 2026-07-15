# `.claude/` — Configuración de Claude Code del proyecto

Esta carpeta prepara el repo para extenderse con **agentes**, **skills** y **comandos** propios.
Hoy están vacías (solo `.gitkeep`); se irán llenando a medida que aparezcan tareas repetitivas.

## Estructura

| Carpeta | Qué va acá | Formato |
|---|---|---|
| `agents/` | Subagentes especializados del proyecto | `*.md` con frontmatter (`name`, `description`, `tools`, `model`) + prompt del sistema |
| `skills/` | Skills reutilizables (procedimientos que Claude puede invocar) | Carpeta por skill con `SKILL.md` (frontmatter `name`, `description`) |
| `commands/` | Slash commands del proyecto | `*.md`; el nombre del archivo es el comando (`/nombre`) |

## Ideas de agentes/skills para este proyecto (a futuro)

- **skill `nueva-migracion`** — genera un archivo `db/migrations/NNN_*.sql` con el numerado correcto.
- **skill `validar-payload`** — valida un lote contra las reglas de la API (precio 0.05–99999,
  stock ≥ 0, campos obligatorios) antes de enviarlo.
- **agent `revisor-mapeo`** — revisa que el mapeo vista→API siga la tabla de la Sección 3 del plan.
- **command `/sync-status`** — resume el estado de la última corrida leyendo `sync_runs`.

## Referencias

- Agentes (subagents): se definen como archivos Markdown con frontmatter en `.claude/agents/`.
- Skills: cada una en su carpeta con un `SKILL.md`.
- Comandos: un `.md` por comando en `.claude/commands/`.

> Nota: `.claude/settings.json` (permisos, hooks, env) se puede agregar cuando haga falta. No se creó
> todavía para no fijar configuración prematura.
