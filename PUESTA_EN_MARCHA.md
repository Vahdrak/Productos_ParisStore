# Puesta en Marcha Completa - Compras Paraguai

Guía paso a paso para levantar todos los servicios.

## 🗂️ Servicios a levantar

1. **PostgreSQL** (BD local)
2. **.NET Worker** (lee Oracle, sincroniza BD)
3. **.NET API** (expone endpoints)
4. **Angular Frontend** (interfaz web)

---

## 1️⃣ PostgreSQL (Docker)

### Verificar que está corriendo

```bash
docker ps | grep -i postgres
```

Si ves algo como `Bases_de_Datos_Postgre`, ya está levantado.

### Si no está, levantar:

```bash
docker start Bases_de_Datos_Postgre
```

**Conexión:**
```
Host: localhost
Port: 5432
DB: comprasparaguai
User: postgres
Password: (la que configuraste)
```

---

## 2️⃣ .NET Worker (Lee Oracle + Sincroniza BD)

### Abrir terminal en la carpeta del Worker

```bash
cd src/ComprasParaguai.Worker
```

### Configurar User Secrets (primera vez)

Si aún no los tienes, agrégalos:

```bash
dotnet user-secrets set "ConnectionStrings:Oracle" "Data Source=172.16.249.52:1521/pdb1;User ID=datosbi;Password=tokubi;"
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=comprasparaguai;Username=postgres;Password=TU_PASSWORD;"
dotnet user-secrets set "ComprasParaguai:Token" "TU_TOKEN_COMPRAS_PARAGUAI"
```

### Levantar el Worker

```bash
dotnet run
```

### Qué verás en los logs

```
=== Iniciando ComprasParaguai Worker ===
Iniciando Worker. Intervalo: 15 minutos
=== Iniciando ciclo de sync ===
Cargados 109248 productos de la BD (hash map)
Leyendo vista Oracle SENSEI.PARAGUAY_COMPRAS_VIEW...
LECTURA COMPLETA: 109248 filas leídas de Oracle ✓
Productos nuevos: 342
Productos actualizados: 1256
Guardados en BD (COPY + upsert)
=== Ciclo completado ===
```

**Esto indica que Oracle SÍ está siendo leído correctamente.**

---

## 3️⃣ .NET API (Expone endpoints)

### En otra terminal, ir a la carpeta API

```bash
cd src/ComprasParaguai.Api
```

### Levantar la API

```bash
dotnet run
```

### Qué verás

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
      Now listening on: https://localhost:5001
```

**La API está disponible en `http://localhost:5000`**

---

## 4️⃣ Angular Frontend (Interfaz web)

### En otra terminal, ir a la carpeta frontend

```bash
cd frontend/comprasparaguai-front
```

### Primera vez: instalar dependencias

```bash
npm install
```

### Levantar dev server

```bash
npm start
```

O:

```bash
ng serve
```

### Qué verás

```
✔ Compiled successfully.
✔ Build at: ...

** Angular Live Development Server is listening on localhost:4200 **
```

**Frontend disponible en `http://localhost:4200`**

---

## 🧪 Probar la integración completa

### 1. Abre el frontend

Navega a `http://localhost:4200`

### 2. Busca un producto

Ingresa un código (ej: `PROD-001847`) y presiona "Buscar"

### 3. Verifica en los logs del Worker

Deberías ver:
```
Cargados 109248 productos de la BD (hash map)
Leyendo vista Oracle SENSEI.PARAGUAY_COMPRAS_VIEW...
LECTURA COMPLETA: 109248 filas leídas de Oracle ✓
```

### 4. Edita un producto

En el frontend:
- Click "Editar"
- Cambia precio o nombre
- Click "Guardar cambios"

Verás:
```
Upsert completado: 1 filas insertadas/actualizadas
```

### 5. Ve el Diff

Click "Cargar Diff" para ver antes/después

Click "Cargar Historial" para ver timeline de cambios

---

## 📊 Monitoreo continuo

### Ver logs en tiempo real

**Worker** (cada 15 minutos automáticamente):
```
=== Iniciando ciclo de sync ===
...
=== Ciclo completado ===
```

**Para ver qué está sincronizando:**
```
Productos nuevos: XXX
Productos actualizados: XXX
Enviados: XXX
Errores: XXX
```

### Ver en Beaver (administrador PostgreSQL)

1. Abre **Beaver** (si lo tienes instalado)
2. Conecta a `localhost:5432`
3. Ve tabla `productos` - verás actualizaciones en tiempo real
4. Ve tabla `productos_historial` - verá el antes/después de cada cambio

---

## 🔄 Flujo completo automático

```
Cada 15 minutos:

1. Worker lee Oracle (109k productos)
2. Compara contra BD local (hash map)
3. Detecta nuevos/modificados
4. Actualiza BD (COPY + upsert)
5. Marca como Pendiente
6. Intenta enviar a Compras Paraguai API
7. Registra estadísticas en sync_runs

+ 1 vez al día: Reconciliación (detecta borrados)
```

---

## ⚠️ Checklist antes de empezar

- [ ] PostgreSQL está corriendo (`docker ps`)
- [ ] Tengo las credenciales de Oracle (Host, Usuario, Password)
- [ ] Tengo el token de Compras Paraguai
- [ ] User Secrets están configurados en .NET
- [ ] Tres terminales abiertas (Worker, API, Frontend)

---

## 🐛 Troubleshooting

### "No se conecta a Oracle"
- Verifica host: `172.16.249.52:1521`
- Usuario: `datosbi`
- SID: `pdb1`
- Vista: `SENSEI.PARAGUAY_COMPRAS_VIEW`

### "Error en PostgreSQL"
- Verifica BD existe: `psql -U postgres -l`
- Verifica contraseña en User Secrets
- Mira logs del contenedor: `docker logs Bases_de_Datos_Postgre`

### "Frontend no encuentra API"
- Asegúrate que API está en `http://localhost:5000`
- Revisa en DevTools si hay errores CORS
- Verifica en `http.service.ts` que `apiUrl` es correcto

### "Usuario secrets no funcionan"
```bash
dotnet user-secrets list
```

Si está vacío, re-configúralos.

---

## 📋 Resumen de URLs

| Componente | URL | Puerto |
|---|---|---|
| Frontend | http://localhost:4200 | 4200 |
| API REST | http://localhost:5000/api | 5000 |
| PostgreSQL | localhost:5432 | 5432 |
| Oracle | 172.16.249.52:1521 | 1521 |

---

## 🎯 Qué probar

1. **Búsqueda de producto** - Verifica que carga datos
2. **Editar producto** - Cambia nombre/precio
3. **Guardar cambios** - Debe aparecer en BD
4. **Ver Diff** - Compara antes/después
5. **Ver Historial** - Timeline de cambios
6. **Logs del Worker** - Debe mostrar "LECTURA COMPLETA" cada 15 min

Si todo funciona, ¡estamos listos! 🚀

