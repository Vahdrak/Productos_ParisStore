# Despliegue ComprasParaguai - Servidor 172.16.249.94

## 📋 Archivos Configurados

### `.env` - Variables de Entorno
```env
DB_USER=postgres
DB_PASSWORD=axbycz
DB_NAME=catalogosdb
ORACLE_CONNECTION_STRING=Data Source=...
COMPRAS_API_URL=https://produtos.comprasparaguai.com.br
COMPRAS_API_TOKEN=59a23dc6-961e-4ec0-8b01-4adb1da9a34f
COMPARA_API_URL=https://comparaparaguai.com/api/integracao/produtos
COMPARA_API_TOKEN=cp_push_1IGpAo0hCi1muypJCirwILscThwF7z6mf8V-txXtn3A
CORS_ALLOWED_ORIGINS=http://localhost:4200,http://172.16.249.94:4200,http://172.16.249.94
```

### `docker-compose.yml` - Orquestación de Servicios

**Contenedores:**
- `catalogos_db_prod` → PostgreSQL (puerto 5432)
- `backend_api_prod` → API .NET (puerto 5000)
- `frontend_prod` → Angular + Nginx (puerto 4200)
- `worker_sync_prod` → Sincronización Oracle → PostgreSQL
- `worker_compras_prod` → Envío a Compras Paraguai
- `worker_compara_prod` → Envío a Compara Paraguai

---

## 🚀 Secuencia de Despliegue (PRIMERA VEZ)

### Paso 1: Copiar archivos al servidor

```bash
scp .env user@172.16.249.94:/opt/catalogosweb/
scp docker-compose.yml user@172.16.249.94:/opt/catalogosweb/
cd /opt/catalogosweb
```

### Paso 2: Levantar SOLO BD (crear base de datos)

```bash
docker compose up -d db

# Verificar que la BD está lista
docker compose ps
docker compose logs -f db
# Esperar: "database system is ready to accept connections"
```

### Paso 3: Levantar API (ejecutar migraciones)

```bash
docker compose up -d backend

# Ver que las migraciones se ejecutaron
docker compose logs -f backend
# Esperar: "Migraciones de base de datos finalizadas"
```

### Paso 4: Levantar Frontend

```bash
docker compose up -d frontend

# Verificar que está sirviendo
docker compose logs -f frontend
curl http://172.16.249.94:4200
```

### Paso 5: Levantar Worker.Sync (CRÍTICO)

```bash
docker compose up -d worker-sync

# Esperar a que termine la sincronización inicial
docker compose logs -f worker-sync
# Esperar: "Sincronización completada: X nuevos, 0 actualizados"
```

⚠️ **IMPORTANTE:** No levantar otros workers hasta que Sync termine.

### Paso 6: Levantar Workers de Envío

Después que Sync termine:

```bash
docker compose up -d worker-compras worker-compara

# Ver estado
docker compose ps
docker compose logs -f worker-compras
docker compose logs -f worker-compara
```

### Paso 7: Verificar Sync completó correctamente

```bash
# Ver última sincronización
docker exec worker_sync_prod dotnet Worker.Sync.dll --last-sync

# Ver cambios pendientes
docker exec worker_sync_prod dotnet Worker.Sync.dll --check-changes
```

### Paso 8: Contar productos pendientes en cada worker

```bash
# Compras Paraguai
docker exec worker_compras_prod dotnet Worker.Compras.dll --check-pending
# Output: "X productos pendientes de enviar"

# Compara Paraguai
docker exec worker_compara_prod dotnet Worker.Compara.dll --check-pending
# Output: "X productos pendientes de enviar"
```

### Paso 9: Prueba pequeña ANTES de envío masivo

#### Prueba Compras Paraguai (500 productos)

```bash
docker exec worker_compras_prod dotnet Worker.Compras.dll --test-batch

# Ver los logs
docker compose logs -f worker-compras
# Esperar a que termine y verificar que NO hay errores
```

#### Prueba Compara Paraguai (500 productos - tardará ~45 min por rate limit)

```bash
docker exec worker_compara_prod dotnet Worker.Compara.dll --test-batch

# Ver los logs (esto tarda mucho)
docker compose logs -f worker-compara
# Esperar: "Lote enviado exitosamente"
```

⚠️ **NOTA:** Compara tiene rate limit de 80 req/hora, así que 500 productos = ~45 minutos

### Paso 10: Si las pruebas OK → Envío masivo inicial

#### Enviar TODOS a Compras Paraguai

```bash
docker exec worker_compras_prod dotnet Worker.Compras.dll --send-all

# Ver progreso
docker compose logs -f worker-compras
# Esperar: "Envío completado: X productos enviados"
```

#### Enviar TODOS a Compara Paraguai (TARDARÁ HORAS)

```bash
docker exec worker_compara_prod dotnet Worker.Compara.dll --send-all

# Ver progreso (esto va a tardar MUCHO)
docker compose logs -f worker-compara
# Si tienes 64k productos: ~1.3 horas (45s × 64 lotes)
```

---

## ✅ Verificar que todo funciona

```bash
# Ver todos los contenedores corriendo
docker compose ps

# Ver logs de cada uno
docker compose logs backend
docker compose logs frontend
docker compose logs worker-sync

# Probar conectividad
curl http://172.16.249.94:5000/api/dashboard    # API
curl http://172.16.249.94:4200                  # Frontend
```

---

## 🔄 Operaciones Posteriores (después del primer despliegue)

### Levantar todo junto (ya está creada la BD)

```bash
# Simplemente:
docker compose up -d

# O si solo quieres ciertos servicios:
docker compose up -d db backend frontend worker-sync worker-compras worker-compara
```

### Ver logs en tiempo real

```bash
# Todos
docker compose logs -f

# Específico
docker compose logs -f backend
docker compose logs -f worker-sync
docker compose logs -f worker-compras
```

### Parar todo

```bash
docker compose down
```

### Reiniciar un servicio

```bash
docker compose restart backend
docker compose restart worker-compras
```

---

## 📊 Ejecutar Comandos de Workers

### Worker.Sync

```bash
# Ver cambios pendientes
docker exec worker_sync_prod dotnet Worker.Sync.dll --check-changes

# Sincronizar una sola vez
docker exec worker_sync_prod dotnet Worker.Sync.dll --sync-once

# Ver última sincronización
docker exec worker_sync_prod dotnet Worker.Sync.dll --last-sync
```

### Worker.Compras

```bash
# Contar pendientes
docker exec worker_compras_prod dotnet Worker.Compras.dll --check-pending

# Prueba con 500 productos
docker exec worker_compras_prod dotnet Worker.Compras.dll --test-batch

# Enviar TODOS (una sola vez)
docker exec worker_compras_prod dotnet Worker.Compras.dll --send-all

# Enviar solo cambios del día
docker exec worker_compras_prod dotnet Worker.Compras.dll --send-pending
```

### Worker.Compara

```bash
# Contar pendientes
docker exec worker_compara_prod dotnet Worker.Compara.dll --check-pending

# Prueba con 500 productos (tardará ~45 min por rate limit)
docker exec worker_compara_prod dotnet Worker.Compara.dll --test-batch

# Enviar TODOS (una sola vez)
docker exec worker_compara_prod dotnet Worker.Compara.dll --send-all

# Enviar solo cambios del día
docker exec worker_compara_prod dotnet Worker.Compara.dll --send-pending
```

---

## 🌐 Acceso

- **Frontend (Web):** http://172.16.249.94:4200
- **API:** http://172.16.249.94:5000
- **PostgreSQL:** 172.16.249.94:5432 (solo desde dentro de la red)

---

## ⚠️ Notas Importantes

1. **Orden de levantamiento:**
   - Primero: `db`
   - Segundo: `backend` (aplica migraciones automáticamente)
   - Tercero: `frontend`
   - Cuarto: `worker-sync` (ESPERAR a que termine)
   - Quinto: `worker-compras` y `worker-compara`

2. **Worker.Sync es crítico:**
   - No levantar otros workers hasta que Sync termine
   - Tardará minutos/horas dependiendo de cantidad de productos

3. **Compara tiene rate limit:**
   - 80 req/hora = 45 segundos entre lotes
   - `--send-all` con 64k productos = ~1.3 horas

4. **CORS está configurado para:**
   - `http://localhost:4200` (desarrollo)
   - `http://172.16.249.94:4200` (producción)
   - `http://172.16.249.94` (sin puerto)

5. **Base de datos:**
   - Nombre: `catalogosdb`
   - Usuario: `postgres`
   - Contraseña: `axbycz`
   - Las migraciones se ejecutan automáticamente en el startup de la API

---

## 🛠️ Troubleshooting

### Contenedor no inicia
```bash
docker compose logs worker-compras
# Revisar el error específico
```

### API no se conecta a BD
```bash
docker compose exec backend dotnet ComprasParaguai.Api.dll
# Verificar ConnectionString en .env
```

### Frontend no carga
```bash
docker compose exec frontend nginx -t
# Verificar configuración de Nginx
```

### Borrar todo y empezar desde cero
```bash
docker compose down -v
docker system prune -a
docker compose pull
docker compose up -d db backend frontend worker-sync
```

---

---

## 🔁 Actualizar un despliegue existente (migración 009 en adelante)

La migración **009** cambia el esquema (`productos.brand` pasa a `marca_id`, `categorias` pasa a árbol).
Cualquier imagen **vieja** contra la BD migrada falla con `column "brand" does not exist`, así que:

1. **Respaldo** antes de nada:
   ```bash
   docker exec <contenedor-db> pg_dump -U postgres catalogosdb > backup_pre009.sql
   ```
2. **Reconstruir y subir las imágenes de TODOS los servicios** (API y los 4 workers, más el front), no solo algunos.
3. `docker compose pull && docker compose up -d`. La API (o cualquier worker) aplica las migraciones al arrancar.
4. **Ojo con `worker-compara`:** al cambiar la descripción de los productos, el primer sync deja casi todo
   pendiente y Compara enviará el catálogo completo (~110k productos, ~1,5 h con lotes de 1000 y pausa de 46 s).
   Si no quieres ese envío ahora, deja `worker-compara` detenido hasta la ventana elegida.

**Migraciones nuevas:** 009 (catálogo normalizado), 010 (historial con diff y búsqueda; variable opcional `Sync__HistorialMesesRetencion`, 6 por defecto) y 011 (usuarios del panel). 012 (pausas de envío por marca/categoría, excepciones por SKU y retiro del destino; ver `docs/Guia_Envio_Productos.md`).

> ⚠️ Con la 012, `worker-compras` y `worker-compara` excluyen lo retenido por pausas. Si activas una pausa con **"retirar del destino"**, el worker envía stock 0 a lo ya publicado; en Compara conviene probarlo antes con `--test-batch`.

**Variables nuevas del servicio `backend` (login del panel; ver datos sigue siendo libre, modificar exige sesión):**

| Variable | Qué es |
|---|---|
| `AUTH_JWT_SECRET` | Clave de firma del token (≥ 32 caracteres, p. ej. `openssl rand -hex 32`). Sin ella la API usa una temporal y las sesiones se pierden al reiniciar. |
| `AUTH_ADMIN_USUARIO` / `AUTH_ADMIN_PASSWORD` | Primer usuario; se crea solo si la tabla `usuarios` está vacía. Cambia la contraseña. |

Agrégalas al `.env` del servidor (ver `.env.example`).
---

**Última actualización:** 2026-09-18  
**Servidor:** 172.16.249.94  
**Usuario Docker:** proyectsoft
