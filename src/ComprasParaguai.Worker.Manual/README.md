# ComprasParaguai.Worker.Manual

Herramienta de prueba para sincronización manual con Compras Paraguai API.

## Uso

### Ejecutar sincronización completa
```bash
cd src/ComprasParaguai.Worker.Manual
dotnet run -c Release -- --sync
```

Sincroniza TODOS los productos pendientes desde Oracle a la API.

### Ejecutar prueba con 500 aleatorios
```bash
dotnet run -c Release -- --test-batch
```

Envía 500 productos seleccionados aleatoriamente. Útil para validar configuración y conectividad.

## Configuración

Editar `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Oracle": "Data Source=...",
    "PostgreSQL": "Host=..."
  },
  "ComprasParaguai": {
    "Token": "tu_token_aqui"
  },
  "Sync": {
    "ImageBaseUrl": "https://www.toku.com.py/media/"
  }
}
```

## Diferencias con Worker automático

- **Worker (servicio)**: Se ejecuta cada 15 minutos automáticamente en background
- **Worker.Manual**: Se ejecuta manualmente bajo demanda para pruebas

## Requisitos

- Oracle y PostgreSQL accesibles
- Token de Compras Paraguai configurado
- .NET 8 SDK
