# ⚡ Quick Start - Compras Paraguai

## 🚀 Inicio en 30 segundos

### Requiere:
- ✅ .NET 8 (ya instalado)
- ✅ Node.js (ya instalado)
- ✅ PostgreSQL corriendo

### Ejecutar

**Opción 1: PowerShell (RECOMENDADO - Automático)**

Abre PowerShell en la raíz del proyecto y ejecuta:

```powershell
# Terminal 1 - API
.\scripts/start-api.ps1

# Terminal 2 - Frontend  
.\scripts/start-frontend.ps1
```

**Opción 2: Línea de comandos (Manual)**

```bash
# Terminal 1 - API
cd backend/ComprasParaguai.Api
dotnet run -c Release

# Terminal 2 - Frontend
cd frontend/comprasparaguai-front
npm start
```

---

## ✅ Verificación

- ✅ **API:** http://localhost:5000/swagger
- ✅ **Frontend:** http://localhost:4200
- ✅ **Monitor:** Ve a "Monitor" en el menú → Ver tendencias

---

## 🎯 Lo que hace cada parte

| Componente | Puerto | URL | Función |
|-----------|--------|-----|---------|
| **API** | 5000 | http://localhost:5000 | Backend (.NET) |
| **Frontend** | 4200 | http://localhost:4200 | Interface (Angular) |
| **PostgreSQL** | 5432 | localhost | Base de datos |

---

## 📊 Monitor de Tendencias (Nueva Feature ⭐)

El proyecto ahora incluye un **Monitor de Tendencias** completo:
- Ver estadísticas de los últimos ciclos de sincronización
- Selector de rango (24h, 48h, 72h)
- Tabla detallada de ciclos
- Métricas: nuevos, actualizados, enviados, errores

**Acceso:** Menú → "Monitor"

---

## 🔧 Solución de problemas

### Error: "Application Control policy has blocked this file"
✅ **Ya resuelto** - El archivo `Directory.Build.props` configura Release mode automáticamente

### API no inicia
1. Verifica que PostgreSQL está corriendo
2. Usa el script PowerShell `scripts/start-api.ps1` (limpia automáticamente)

### Frontend no carga datos
1. Verifica que la API está en puerto 5000
2. Hard reload: `Ctrl+Shift+R`

---

## 📝 Archivos de configuración

- `scripts/start-api.ps1` - Script para iniciar API (RECOMENDADO)
- `scripts/start-frontend.ps1` - Script para iniciar Frontend
- `Directory.Build.props` - Fuerza Release mode automáticamente
- `SETUP.md` - Guía completa de instalación

---

## 🎉 ¡Listo!

Ya puedes:
- ✅ Ver el dashboard
- ✅ Buscar productos
- ✅ Monitorear tendencias de sincronización
- ✅ Ver historial de cambios

¡Que disfrutes! 🚀
