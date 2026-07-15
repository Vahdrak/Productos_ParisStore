# ComprasParaguai.Worker

Worker Service .NET 8: orquesta la sincronización. Punto de arranque del sistema de sync.

Contendrá:
- **Ciclo cada 15 min** (horario comercial): leer vista → mapear/limpiar → hash → comparar →
  guardar cambios → enviar deltas → registrar en `sync_runs`.
- **Carga inicial** (una vez): subir todo el catálogo en lotes de 500.
- **Reconciliación diaria**: marcar bajas (`exists_in_view = FALSE`, `stock = 0`, Pendiente).
- Config, secretos (User Secrets/env) y logging (Serilog).

Scaffold:
```bash
dotnet new worker -n ComprasParaguai.Worker -o src/ComprasParaguai.Worker
```
