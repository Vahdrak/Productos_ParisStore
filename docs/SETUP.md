# 🚀 Guía de Configuración - Compras Paraguai

## Requisitos Previos
- **.NET 8** instalado
- **Node.js 18+** instalado
- **PostgreSQL** corriendo en `localhost:5432`
- **Git** (opcional)

## 🎯 Inicio Rápido

### Opción 1: Scripts Batch (Windows)

#### Backend (API .NET)
```bash
double-click scripts/run-api.bat
```
O en PowerShell:
```powershell
.\scripts/run-api.bat
```
**Esperado:** La API estará disponible en `http://localhost:5000`

#### Frontend (Angular)
En otra terminal:
```bash
double-click scripts/run-frontend.bat
```
**Esperado:** El frontend estará disponible en `http://localhost:4200`

### Opción 2: Comandos Manuales

#### Backend
```bash
cd backend/ComprasParaguai.Api
dotnet run -c Release
```

#### Frontend
```bash
cd frontend/comprasparaguai-front
npm start
```

---

## 🔍 Verificación

### ✅ API funcionando
```bash
curl http://localhost:5000/api/dashboard
```
Debería retornar JSON con estadísticas del dashboard.

### ✅ Monitor funcionando
```bash
curl http://localhost:5000/api/dashboard/trends?hours=24
```
Debería retornar datos de tendencias de los últimos 24 ciclos.

### ✅ Frontend funcionando
Abre http://localhost:4200 en el navegador.

---

## 📋 Características Principales

### Dashboard
- Contadores: Pendientes, Nuevos, Actualizados, Enviados, Errores
- Última corrida y estado general

### Monitor de Tendencias
- Selector de rango (24h, 48h, 72h)
- Estadísticas principales (totales y tasa de éxito)
- Tabla detallada de ciclos de sincronización
- Timestamps y métricas por ciclo

### Productos
- Búsqueda por código o nombre
- Filtros por estado
- Vista detallada con preview de imágenes
- Historial de cambios

### Historial
- Registro de todas las corridas de sincronización
- Detalles de cada ciclo

---

## 🛠️ Solución de Problemas

### "Application Control policy has blocked this file"
**Problema:** Windows bloquea los DLLs compilados
**Solución:**
```powershell
Get-ChildItem -Path "src\ComprasParaguai.Api\bin" -Recurse -File | Unblock-File -Confirm:$false
```

### API no responde
**Verificar:**
1. PostgreSQL está corriendo
2. Puerto 5000 está disponible
3. Usa `-c Release` en lugar de Debug

### Frontend no carga datos
**Verificar:**
1. API está corriendo en puerto 5000
2. CORS está habilitado (ya está configurado)
3. Hard reload (Ctrl+Shift+R) en el navegador

---

## 📁 Estructura del Proyecto

```
ComprasParaguai/
├── backend/
│   ├── ComprasParaguai.Api/          ← Web API (ASP.NET Core)
│   ├── ComprasParaguai.Core/         ← Lógica de negocio
│   └── ComprasParaguai.Infrastructure/  ← Acceso a datos
├── workers/
│   ├── Worker.Sync/                  ← Oracle → PostgreSQL
│   ├── Worker.Compras/               ← Envío a Compras Paraguai
│   └── Worker.Compara/               ← Envío a Compara Paraguai
├── frontend/
│   └── comprasparaguai-front/        ← Frontend (Angular 18)
├── db/
│   └── migrations/                   ← Migraciones SQL
├── scripts/                          ← scripts/run-api.bat, scripts/run-frontend.bat, start-*.ps1
└── docs/SETUP.md                     ← Este archivo
```

---

## 📊 Endpoints Principales

### Dashboard
- `GET /api/dashboard` - Estadísticas generales
- `GET /api/dashboard/sync-runs` - Historial de corridas

### Tendencias (Monitor)
- `GET /api/dashboard/trends?hours=24|48|72` - Datos de tendencias

### Productos
- `GET /api/productos?page=1&pageSize=50` - Listar productos
- `GET /api/productos/{code}` - Detalles de un producto
- `GET /api/productos/{code}/historial` - Historial de cambios
- `GET /api/productos/{code}/diff` - Comparar versiones

---

## 💡 Notas Importantes

1. **Modo Release:** Siempre usa `-c Release` para evitar problemas de bloqueo de Windows
2. **Base de datos:** Las migraciones se ejecutan automáticamente al iniciar la API
3. **CORS:** El frontend en `http://localhost:4200` está autorizado
4. **Logs:** Revisa la consola de la API para ver detalles de sincronización

---

## 🎓 Próximos Pasos

1. Configura tu token de Compras Paraguai en la pestaña "API ComprasParaguai"
2. Verifica que los productos se sincronizan correctamente
3. Monitorea las tendencias en la pestaña "Monitor"

¡Listo para usar! 🎉
