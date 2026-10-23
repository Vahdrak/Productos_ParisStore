# Docker Setup - Compras Paraguai Sync Service

Este documento describe cómo construir y desplegar las imágenes Docker del servicio de sincronización.

## Estructura de Imágenes

El proyecto se divide en **6 imágenes Docker**:

1. **API Backend** (`proyectsoft/comprasparaguai-api`)
   - Servicio ASP.NET Core
   - Puerto: 5000
   - Endpoints REST para lectura

2. **Frontend Angular** (`proyectsoft/comprasparaguai-frontend`)
   - Interfaz web
   - Puerto: 4200
   - Nginx + Angular build

3. **Worker.Sync** (`proyectsoft/comprasparaguai-worker-sync`)
   - Lee Oracle → sincroniza PostgreSQL
   - Corre cada 2 horas
   - Detecta cambios por hash

4. **Worker.Compras** (`proyectsoft/comprasparaguai-worker-compras`)
   - Envía productos a Compras Paraguai API
   - Corre cada 2 horas
   - Lotes máximo 500

5. **Worker.Compara** (`proyectsoft/comprasparaguai-worker-compara`)
   - Envía productos a Compara Paraguai API
   - Corre cada 2 horas
   - Lotes máximo 1000, rate limit 80 req/hora

6. **Worker.Verify** (`proyectsoft/comprasparaguai-worker-verify`)
   - Corre siempre encendido (`--servicio`) y atiende los botones **Revisar ahora** / **Verificar muestra** del panel (solo con sesión)
   - Consulta a Compras la disponibilidad de los productos y comprueba una muestra de los enviados
   - Es, junto a Worker.Compras, el único que lleva el token de Compras (la API no lo tiene)

---

## Prerrequisitos

- Docker 20.10+
- Docker Compose 2.0+
- PowerShell (para scripts .bat en Windows)
- Git (para versionamiento automático)
- Cuenta en DockerHub (para publicar)

---

## Configuración Local (.env)

Crear archivo `.env` en la raíz del proyecto:

```env
# PostgreSQL
DB_USER=postgres
DB_PASSWORD=tu-contraseña-segura
DB_NAME=comprasparaguaidb

# Oracle (para Worker.Sync)
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=...);User Id=user;Password=pass;

# Compras Paraguai API
COMPRAS_API_URL=https://produtos.comprasparaguai.com.br
COMPRAS_API_TOKEN=tu-token-compras

# Compara Paraguai API
COMPARA_API_URL=https://comparaparaguai.com/api/integracao/produtos
COMPARA_API_TOKEN=tu-token-compara

# Login del panel
AUTH_JWT_SECRET=una-clave-larga-de-al-menos-32-caracteres
AUTH_ADMIN_USUARIO=admin
AUTH_ADMIN_PASSWORD=cambia-esta-contraseña

# Opcional
IMAGE_TAG=latest        # versión de las imágenes a levantar (p. ej. 1.2.0)
VERIFY_AUTO_HORAS=0     # 0 = Verify solo corre cuando alguien lo pide en el panel
```

Ver `.env.example` para plantilla.

> **Los secretos no viajan en las imágenes.** El `.dockerignore` deja fuera todos los `.env` (el de la raíz y los de cada worker,
> que los `.csproj` copiarían a la salida) y `bin/`, `obj/`, `node_modules/`, `docs/` y `tests/`. Las imágenes salen sin llaves ni contraseñas:
> toda la configuración llega por el `.env` del servidor y las variables del `docker-compose.yml`. Si agregas un servicio nuevo, no copies su `.env` a la imagen.

---

## Construir Imágenes Localmente

### Build individual:

```bash
# API
docker build -t proyectsoft/comprasparaguai-api:local -f Dockerfile.api .

# Worker.Sync
docker build -t proyectsoft/comprasparaguai-worker-sync:local -f Dockerfile.worker-sync .

# Worker.Compras
docker build -t proyectsoft/comprasparaguai-worker-compras:local -f Dockerfile.worker-compras .

# Worker.Compara
docker build -t proyectsoft/comprasparaguai-worker-compara:local -f Dockerfile.worker-compara .

# Worker.Verify
docker build -t proyectsoft/comprasparaguai-worker-verify:local -f Dockerfile.worker-verify .

# Frontend
docker build -t proyectsoft/comprasparaguai-frontend:local -f Dockerfile.frontend .
```

### Build todos a la vez:

```bash
docker compose build
```

---

## Publicar en DockerHub

### Opción 1: Scripts individuales (Windows)

```bash
.\publish-api.bat
.\publish-worker-sync.bat
.\publish-worker-compras.bat
.\publish-worker-compara.bat
.\publish-worker-verify.bat
.\publish-frontend.bat
```

### Opción 2: Script todo-en-uno

```bash
.\publish-all.bat
```

Automáticamente usa el tag de git (`git describe --tags`) o `latest`.

### Opción 3: Manual

```bash
# Login
docker login

# Build y push
docker build -t proyectsoft/comprasparaguai-api:1.0.0 -f Dockerfile.api .
docker push proyectsoft/comprasparaguai-api:1.0.0
docker push proyectsoft/comprasparaguai-api:latest
```

---

## Ejecutar Localmente

### Con Docker Compose:

```bash
# Inicia todos los servicios
docker compose up -d

# Ver logs
docker compose logs -f

# Detener
docker compose down
```

### Acceso:

- **Frontend:** http://localhost:4200
- **API:** http://localhost:5000/swagger (si está habilitado)
- **PostgreSQL:** localhost:5432

---

## Desplegar en Servidor (Producción)

### 1. Copiar docker-compose.yml y .env al servidor

```bash
scp docker-compose.yml user@server:/app/
scp .env user@server:/app/
```

### 2. Login en DockerHub

```bash
docker login
```

### 3. Levantar servicios

```bash
cd /app
docker compose up -d
```

### 4. Verificar estado

```bash
docker compose ps
docker compose logs -f api
```

### 5. Actualizar (nuevas imágenes)

```bash
docker compose pull
docker compose up -d
```

Para fijar una versión (en vez de `latest`): `IMAGE_TAG=1.2.0` en el `.env` y repetir los dos comandos. Las migraciones de la base se aplican solas
al arrancar la API o cualquier worker (con candado en la BD), así que no importa cuál arranque primero.

---

## Limpieza

### Eliminar contenedores y volúmenes

```bash
docker compose down -v
```

### Limpiar imágenes no usadas

```bash
docker system prune -a
```

---

## Variables de Entorno

| Variable | Descripción | Ejemplo |
|---|---|---|
| `DB_USER` | Usuario PostgreSQL | postgres |
| `DB_PASSWORD` | Contraseña PostgreSQL | MySecurePass123 |
| `DB_NAME` | Nombre base de datos | comprasparaguaidb |
| `ORACLE_CONNECTION_STRING` | Cadena conexión Oracle | Data Source=... |
| `COMPRAS_API_URL` | URL API Compras | https://produtos.comprasparaguai.com.br |
| `COMPRAS_API_TOKEN` | Token Compras API | secret-token |
| `COMPARA_API_URL` | URL API Compara | https://comparaparaguai.com/api/integracao/produtos |
| `COMPARA_API_TOKEN` | Token Compara API | secret-token |
| `AUTH_JWT_SECRET` | Clave de firma de las sesiones del panel (mínimo 32 caracteres) | `openssl rand -hex 32` |
| `AUTH_ADMIN_USUARIO` / `AUTH_ADMIN_PASSWORD` | Primer usuario del panel (solo si la tabla `usuarios` está vacía) | admin |
| `CORS_ALLOWED_ORIGINS` | Orígenes del front que la API acepta | http://172.16.249.94:4200 |
| `VERIFY_AUTO_HORAS` | 0 = Verify solo cuando se pide en el panel; N = además cada N horas | 0 |
| `IMAGE_TAG` | Versión de las imágenes a levantar | latest |

---

## Troubleshooting

### Contenedor se reinicia continuamente

```bash
docker compose logs worker-sync
```

Verificar variables de entorno y conexión a Oracle.

### API no responde

```bash
docker compose exec api dotnet ComprasParaguai.Api.dll
```

### Problema de permisos con volúmenes

```bash
sudo chown -R 1000:1000 /var/lib/docker/volumes/*
```

### Purgar todo y empezar desde cero

```bash
docker compose down -v
docker system prune -a
docker compose pull
docker compose up -d
```

---

## Notas de Seguridad

- ⚠️ Cambiar `DB_PASSWORD` en producción
- ⚠️ No commitear `.env` a git (está en `.gitignore`)
- ⚠️ Usar secrets de Docker Swarm o Kubernetes en producción
- ✅ Los tokens de API se inyectan por variable de entorno
- ✅ PostgreSQL solo expone en red interna (no puerto 5432 en producción)

---

## Referencias

- [DockerHub - proyectsoft](https://hub.docker.com/u/proyectsoft)
- [Docker Compose Docs](https://docs.docker.com/compose/)
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet)
- [Nginx Docker Image](https://hub.docker.com/_/nginx)
