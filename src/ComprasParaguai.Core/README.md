# ComprasParaguai.Core

Dominio **puro**, sin dependencias de infraestructura (ni Oracle, ni PostgreSQL, ni HTTP).

Contendrá:
- Modelos de dominio (`Producto`, `SyncRun`, etc.).
- Contratos/interfaces (`IViewReader` para leer Cangrejo — intercambiable escaneo↔incremental; ver
  Sección 2.4 del plan).
- Lógica de mapeo vista→API, limpieza (stock negativo→0, validación de precio) y cálculo del
  `source_hash` (SHA-256 del payload final).

Scaffold (cuando arranque la implementación):
```bash
dotnet new classlib -n ComprasParaguai.Core -o src/ComprasParaguai.Core
```
