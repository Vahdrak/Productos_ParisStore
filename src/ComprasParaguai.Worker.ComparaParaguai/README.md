# ComprasParaguai.Worker.ComparaParaguai

Worker de sincronización de productos desde ComprasParaguai a Compara Paraguai API.

## Descripción

Este Worker lee productos de la tabla `productos` de la BD comprasparaguai y los envía a la API de Compara Paraguai en lotes de 500 productos.

### Características
- ✅ Lectura de 500 productos aleatorios o todos
- ✅ Envío en lotes de máximo 500
- ✅ Registro de sincronizaciones en tabla `sync_cp_runs`
- ✅ Logging detallado por lote
- ✅ Manejo de errores HTTP

## Instalación

### Requisitos
- .NET 8 SDK
- PostgreSQL accesible en localhost:5432
- Conexión a internet para la API de Compara Paraguai

### Pasos

1. **Aplicar migración de BD**
   ```bash
   psql -U postgres -d comprasparaguai -f ../../db/migrations/003_sync_cp.sql
   ```

2. **Configurar credenciales**
   - Editar appsettings.Development.json
   - Verificar token en ComparaParaguai:ApiToken

3. **Restaurar dependencias**
   ```bash
   dotnet restore
   ```

## Uso

### Comando: check-pending
Ver cuántos productos hay en la tabla

```bash
dotnet run -c Release -- --check-pending
```

### Comando: test-batch (por defecto)
Enviar 500 productos aleatorios como prueba

```bash
dotnet run -c Release -- --test-batch
```

### Comando: sync-all
Enviar todos los productos en lotes de 500

```bash
dotnet run -c Release -- --sync-all
```

## Flujo de Sincronización

1. Leer productos de tabla `productos`
2. Validar estructura (campos requeridos)
3. Dividir en lotes de 500
4. Para cada lote:
   - Serializar a JSON
   - Enviar POST a la API
   - Esperar respuesta
   - Si éxito: marcar sync_status = 1 en BD
   - Registrar resultado en sync_cp_runs

## Base de Datos

### Tabla: sync_cp_runs
Almacena historial de sincronizaciones

| Campo | Tipo | Descripción |
|-------|------|-------------|
| id | BIGINT | PK |
| started_at | TIMESTAMPTZ | Inicio del envío |
| finished_at | TIMESTAMPTZ | Fin del envío |
| batch_num | INT | Número de lote |
| rows_sent | INT | Cantidad de productos |
| http_status | INT | Status HTTP |
| response_text | VARCHAR(2000) | Respuesta de la API |
| success | BOOLEAN | true si 200-299 |
| error_msg | VARCHAR(2000) | Msg de error |
| notes | VARCHAR(2000) | Notas opcionales |

## Logging

Los logs aparecen en consola con formato:
```
[Lote 1] Enviando 500 productos...
[Lote 1] ✓ Enviado exitosamente. Status: 202
```

## Configuración

Archivo: appsettings.Development.json

```json
{
  "ComparaParaguai": {
    "ApiToken": "tu_token_aqui",
    "ApiUrl": "https://comparaparaguai.com/api/integracao/produtos",
    "BatchSize": 500
  }
}
```

## Seguridad

⚠️ IMPORTANTE
- Token API en appsettings.Development.json (gitignore)
- Nunca commitear credenciales
- Token es específico para escritura en Compara Paraguai

## Fase Siguiente

Después del Worker, se implementará:
- Backend: Endpoints GET para consultar historial
- Frontend: Nueva pestaña "Compara Paraguai" en dashboard
