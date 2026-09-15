@echo off
REM Script para ejecutar la API de Compras Paraguai en Release mode
REM Esto evita problemas de bloqueo de archivos de Windows

echo.
echo ====================================
echo Iniciando API - Compras Paraguai
echo ====================================
echo.

cd /d "C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Api"

echo Compilando en Release mode...
dotnet build -c Release

if %ERRORLEVEL% NEQ 0 (
    echo Error en la compilacion
    pause
    exit /b 1
)

echo.
echo Iniciando servidor en http://localhost:5000...
echo Presiona Ctrl+C para detener
echo.

dotnet run -c Release

pause
