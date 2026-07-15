# ComprasParaguai.Infrastructure

Implementaciones de acceso a sistemas externos. Depende de `Core` (implementa sus interfaces).

Contendrá:
- **Lector de Cangrejo** (Oracle) con `Oracle.ManagedDataAccess` + Dapper, en *streaming*
  (`DataReader`), solo lectura.
- **Repositorios PostgreSQL** con Npgsql + Dapper: comparación en memoria, escritura masiva con
  `COPY` a tabla staging + `INSERT ... ON CONFLICT (code) DO UPDATE`.
- **Cliente HTTP** tipado de la API de Compras Paraguai (token en header, lotes de 500, parseo del
  `207`), con Polly para reintentos/timeout/circuit breaker.

Scaffold:
```bash
dotnet new classlib -n ComprasParaguai.Infrastructure -o src/ComprasParaguai.Infrastructure
```
