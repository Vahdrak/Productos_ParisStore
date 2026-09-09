# Guía de Envío de Productos a Compras Paraguai

## Estrategia

El envío de productos a Compras Paraguai sigue un modelo **two-phase**:

1. **Fase 1: Envío Full Inicial** (`SendAllAsync`)
   - Ejecutar **una sola vez** al inicio
   - Envía TODOS los productos (sin filtro)
   - Divide en lotes de 500
   - Marca todos como "Enviado" en BD

2. **Fase 2: Sincronización Incremental** (`SendPendingAsync`)
   - Ejecutar **cada 8 horas** (configurar scheduler)
   - Envía solo productos con cambios desde el último envío
   - Divide en lotes de 500
   - Marca como "Enviado" o "Error" según resultado

## Comandos disponibles

### Envío Full Inicial (una sola vez)

```bash
dotnet run --send-all
```

**Output esperado:**
```
✓ Envío completado: 40000 enviados, 0 fallidos, 40000 total
```

Si hay 40.000 productos, se ejecutarán 80 lotes de 500.

### Envío Incremental (cada 8 horas)

```bash
dotnet run --send-pending
```

**Output esperado:**
```
✓ Envío completado: 150 enviados, 0 fallidos, 150 total
```

Envía solo cambios detectados desde el último envío.

### Verificar pendientes

```bash
dotnet run --check-pending
```

Muestra cuántos productos están listos para enviar.

## Configurar Scheduler (Windows Task Scheduler)

Para ejecutar `--send-pending` cada 8 horas:

### Opción 1: PowerShell Script

Crear archivo `sync-every-8h.ps1`:

```powershell
# Script para ejecutar cada 8 horas
cd "C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Worker.ComparaParaguai"
dotnet run --send-pending
```

### Opción 2: Windows Task Scheduler

1. Abrir **Task Scheduler** (`taskschd.msc`)
2. Crear nueva tarea
3. **General**: Nombre = "ComprasParaguai - Sync Every 8h"
4. **Trigger**: 
   - Ejecutar al inicio del sistema
   - Repetir cada 8 horas
5. **Action**: 
   - Program: `C:\Program Files\dotnet\dotnet.exe`
   - Arguments: `run --send-pending`
   - Start in: `C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Worker.ComparaParaguai`

## Flujo de Estado de Productos

```
[Pendiente (0)] → SendAll/SendPending → [Enviado (1)]
                                    └→ [Error (2)]
```

- **sync_status = 0**: Pendiente de envío
- **sync_status = 1**: Enviado a Compras Paraguai (último envío exitoso)
- **sync_status = 2**: Error en el último envío (ver `last_error`)

## Campos que se sincronizan

```
Code, NameEs, NamePt, Price, PriceIva, Stock, 
Brand, DescriptionEs, DescriptionPt, UrlImage, Link, LinkPurchase
```

⚠️ El token va siempre en el **header HTTP**, nunca en la URL.

## Reintentos y Errores

- Reintentos automáticos: 3 intentos con backoff exponencial
- Timeout: 30 segundos por lote
- Errores de validación: se registran en `last_error` con detalles del campo

## Monitoreo

Revisar en BD:

```sql
-- Productos con error
SELECT code, last_error FROM productos WHERE sync_status = 2 ORDER BY updated_at DESC;

-- Historial de cambios
SELECT code, changed_at FROM product_changes ORDER BY changed_at DESC LIMIT 50;

-- Resumen
SELECT sync_status, COUNT(*) FROM productos GROUP BY sync_status;
```

## Casos de Uso

### Caso A: Primer envío completo
```bash
# 1. Enviar todos
dotnet run --send-all

# 2. Monitorear
dotnet run --check-pending  # Debería ser 0

# 3. Configurar scheduler para --send-pending
```

### Caso B: Agregar nuevos productos
```bash
# 1. Se crean en BD con sync_status = 0 (Pendiente)
# 2. Siguiente ejecución de --send-pending los detecta y envía
# 3. Se marcan como Enviado (sync_status = 1)
```

### Caso C: Modificar productos existentes
```bash
# 1. Cambio en BD dispara trigger que marca sync_status = 0
# 2. Siguiente ejecución de --send-pending envía solo este
# 3. Se marca como Enviado (sync_status = 1)
```

## Tests

Ejecutar tests de SendService:

```bash
dotnet test --filter "SendService"
```

Tests incluyen:
- ✅ Envío de 2000 productos en 4 lotes
- ✅ Envío de 40.000 productos en 80 lotes
- ✅ Manejo de cambios después de SendAll
- ✅ Errores y reintentos
- ✅ BD vacía

## Troubleshooting

### Problema: Todos los productos marcados como Error

```sql
SELECT DISTINCT last_error FROM productos WHERE sync_status = 2;
```

Causas comunes:
- Token inválido o expirado
- API de Compras Paraguai no disponible
- Validación de campos (precio < mínimo, stock negativo, etc.)

### Problema: SendPending no envía nada

```bash
dotnet run --check-pending  # Ver si hay pendientes
```

Si es 0, significa:
- Todo fue enviado en SendAll
- No hay cambios desde el último envío
- (Es correcto comportamiento)

### Problema: SendAll se detiene a mitad

Revisar logs para ver en qué lote falló. Resumir desde ese punto:

```bash
# Reintentar (solo enviará los que siguen siendo Pendiente)
dotnet run --send-pending
```
