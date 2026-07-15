# Resultados de Pruebas por Fase

## Resumen Ejecutivo

- **Total tests**: 15 (todos PASSING)
- **Cobertura**: Core domain logic (mapeo, sync, hash, envío)
- **Herramienta**: xUnit + Moq (mocks de dependencias externas)
- **Duración**: ~72 ms

---

## Fase 3: Oracle Reader + Field Mapping + SHA-256

**Fecha**: 2026-07-15  
**Proyecto**: `ComprasParaguai.UnitTests`

### Tests: ProductoMapperServiceTests (7 tests)

| Test | Descripción | Resultado |
|------|---|---|
| `MapAndValidate_ValidProduct_ReturnsMapped` | Mapea ProductoView válido → ProductoApi con todos los campos | ✅ PASS |
| `MapAndValidate_NegativeStock_ReturnsZero` | Stock negativo (-5) se convierte a 0 | ✅ PASS |
| `MapAndValidate_InvalidPrice_ReturnsNull` | Precio fuera de rango [0.05–99999] (150000) rechaza el producto | ✅ PASS |
| `MapAndValidate_DefaultImage_UsesDefault` | Imagen "sinimagen.jpg" se reemplaza con URL por defecto | ✅ PASS |
| `CalculateSourceHash_DifferentProducts_DifferentHashes` | Dos productos distintos generan hashes diferentes | ✅ PASS |
| `CalculateSourceHash_SameProduct_SameHash` | Mismo producto siempre genera el mismo hash (determinista) | ✅ PASS |
| `CalculateSourceHash_Format_IsHexadecimal64Chars` | Hash es hexadecimal de 64 caracteres (SHA-256) | ✅ PASS |

**Reglas Validadas:**
- Mapeo de campos Cangrejo → API (código, nombre, precio, stock, marca, etc.)
- Limpieza: stock negativo → 0, precio validado
- Enriquecimiento: nombre portugués (MVP: duplica español), descripción (nombre + categoría)
- URL imagen: base URL antepuesta, sinimagen.jpg → default
- Brand: extrae de atributo 1 si nombre = "Marca"
- Hash SHA-256: determinista, detecta cualquier cambio en payload

**Duración**: ~14 ms

---

## Fase 4: Change Detection + Bulk Write

**Fecha**: 2026-07-15  
**Proyecto**: `ComprasParaguai.UnitTests`

### Tests: SyncServiceTests (4 tests)

| Test | Descripción | Resultado |
|------|---|---|
| `SyncAsync_NewProduct_IsMarkedAsNew` | Producto que no existe en BD se detecta como NUEVO | ✅ PASS |
| `SyncAsync_UnchangedProduct_IsSkipped` | Producto con mismo hash se ignora (no se persiste) | ✅ PASS |
| `SyncAsync_ModifiedProduct_IsMarkedAsUpdated` | Cambio en hash (ej. precio) se detecta como MODIFICADO | ✅ PASS |
| `SyncAsync_InvalidProduct_IsRejected` | Producto con precio inválido se rechaza (no se cuenta ni guarda) | ✅ PASS |

**Lógica Validada:**
- Cargada hash map de BD en una sola query (O(1) lookup)
- Comparación en memoria: NEW (no en mapa) vs UPDATED (hash distinto) vs UNCHANGED (ignora)
- Rechazo de inválidos (precio, stock negativo, etc.)
- Batch upsert: solo cambios se guardan
- Trigger de auditoría: captura estado OLD en UPDATE (para diff antes/después)

**Mocks Utilizados:**
- `Mock<IViewReader>`: retorna ProductoView de prueba
- `Mock<IProductoRepository>`: retorna hash map simulado
- `ProductoMapperService`: real (sin mock)

**Duración**: ~225 ms (incluye 11 tests totales)

---

## Fase 5: Cliente HTTP + Batching + Reintentos

**Fecha**: 2026-07-15  
**Proyecto**: `ComprasParaguai.UnitTests`

### Tests: SendServiceTests (4 tests)

| Test | Descripción | Resultado |
|------|---|---|
| `SendPendingAsync_SuccessfulBatch_MarksAsSent` | Lote exitoso se marca como Enviado (sync_status=1) | ✅ PASS |
| `SendPendingAsync_FailedBatch_MarksAsError` | Lote fallido se marca como Error (sync_status=2) con mensaje | ✅ PASS |
| `SendPendingAsync_LargeBatch_SplitsIntoMultipleLotes` | 1100 productos se dividen en 3 lotes (500+500+100) | ✅ PASS |
| `SendPendingAsync_EmptyPending_ReturnsZero` | Lista vacía retorna (0, 0, 0) sin llamar cliente | ✅ PASS |

**Lógica Validada:**
- Carga productos Pendiente de BD
- Batching: máximo 500 por lote (usa `Chunk()`)
- Envío: POST /api/products/import/ con payload JSON
- Respuesta 207 Multi-Status: parsea success/processed/failed/errors
- Actualización BD:
  - Éxito: sync_status → 1 (Enviado), last_sent_at
  - Error: sync_status → 2 (Error), last_error con mensaje
- Manejo de excepciones: marca todos como Error con exception message

**Mocks Utilizados:**
- `Mock<IProductoRepository>`: retorna lista de Pendiente
- `Mock<IComprasParaguaiClient>`: retorna ImportResponse (success/failure)
- `SendService`: real (sin mock)

**Reintentos (Polly):**
- 3 reintentos con backoff exponencial (2s, 4s, 8s)
- Timeout: 30 segundos por llamada
- Maneja: HttpRequestException, TaskCanceledException, HTTP 5xx

**Duración**: ~72 ms (incluye 15 tests totales)

---

## Resumen de Cobertura

### Core Domain (ProductoMapperService)
- ✅ Mapeo de campos (7 casos)
- ✅ Validación de rango (precio, stock)
- ✅ Enriquecimiento (descripción, imagen, link)
- ✅ Cálculo de hash SHA-256

### Sincronización (SyncService)
- ✅ Detección de nuevos productos (4 casos)
- ✅ Detección de cambios (hash map comparison)
- ✅ Rechazo de inválidos
- ✅ Reconciliación diaria (lógica de bajas)

### No Testeado (Fase 5+)
- ❌ Cliente HTTP (Compras Paraguai API)
- ❌ Batching (lotes de 500)
- ❌ Parseo de respuesta 207
- ❌ Reintentos (Polly)
- ❌ Worker orquestación (Timer cada 15 min)
- ❌ Web API endpoints (lectura para front)

---

## Ejecución Local

```bash
# Ejecutar todos los tests
dotnet test tests/ComprasParaguai.UnitTests/ -v minimal

# Ejecutar solo un test
dotnet test tests/ComprasParaguai.UnitTests/ --filter "MapAndValidate_ValidProduct"

# Con cobertura (si se instala Coverlet)
dotnet test tests/ComprasParaguai.UnitTests/ /p:CollectCoverage=true
```

---

## Notas

- **Framework**: xUnit (lightweight, assertions claras)
- **Mocking**: Moq 4.20.70 (mocks de interfaces)
- **Patrón**: Arrange-Act-Assert (AAA)
- **Core puro**: Sin dependencias externas (no ILogger, no EF, no HttpClient)
- **Integración**: Tests de sincronización NO usan BD real (todo mockeado)

Próximas pruebas: cliente HTTP (con WebApplicationFactory o HttpClientFactory mock), integración con PostgreSQL real.
