@echo off
REM Script para ejecutar el Frontend de Compras Paraguai

echo.
echo ====================================
echo Iniciando Frontend - Compras Paraguai
echo ====================================
echo.

cd /d "C:\Users\MARIO\Documents\ComprasParaguai\frontend\comprasparaguai-front"

echo Instalando dependencias (si es necesario)...
if not exist "node_modules" (
    call npm install
)

echo.
echo Iniciando servidor en http://localhost:4200...
echo Presiona Ctrl+C para detener
echo.

call npm start

pause
