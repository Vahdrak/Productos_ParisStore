# comprasparaguai-front (Angular)

App Angular de monitoreo. Consume la Web API (`ComprasParaguai.Api`), solo lectura.

Pantallas (ver Sección 11 del plan):
- **Dashboard** — contadores + últimas corridas (`sync_runs`).
- **Productos** — tabla con `code`, nombre, precio, stock, badge de `sync_status` y marca de
  "modificado" si tiene historial. Filtros arriba.
- **Detalle** — datos actuales + **diff "antes/después"** (`/diff`) + timeline del `/historial`.

Scaffold (Angular CLI vía npx, sin instalación global):
```bash
cd frontend
npx @angular/cli@latest new comprasparaguai-front --standalone --routing --style scss
```

> Ojo: el scaffold crea la app **dentro** de esta carpeta. Al generarla, este README puede
> integrarse o moverse según prefieras.
