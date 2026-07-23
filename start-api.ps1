#!/usr/bin/env powershell
# Script para iniciar la API de Compras Paraguai de forma segura

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     Iniciando API - Compras Paraguai      ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. Matar procesos bloqueando
Write-Host "🔍 Limpiando procesos previos..." -ForegroundColor Yellow
$dotnetProcs = Get-Process | Where-Object { $_.Name -like "*dotnet*" } -ErrorAction SilentlyContinue
if ($dotnetProcs) {
    $dotnetProcs | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "✅ Procesos terminados" -ForegroundColor Green
} else {
    Write-Host "✅ No hay procesos previos" -ForegroundColor Green
}

# 2. Limpiar carpetas de build
Write-Host "🧹 Limpiando carpetas de compilación..." -ForegroundColor Yellow
$apiPath = "C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Api"
Remove-Item -Path "$apiPath\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$apiPath\obj" -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "✅ Carpetas limpias" -ForegroundColor Green

# 3. Compilar y ejecutar
Write-Host ""
Write-Host "🔨 Compilando API en Release mode..." -ForegroundColor Yellow
Write-Host ""

Set-Location $apiPath
$buildResult = & dotnet build -c Release 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Compilación exitosa" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 Iniciando servidor..." -ForegroundColor Cyan
    Write-Host "   📍 API disponible en: http://localhost:5000" -ForegroundColor Green
    Write-Host "   📊 Swagger disponible en: http://localhost:5000/swagger" -ForegroundColor Green
    Write-Host ""
    Write-Host "⏹️  Presiona Ctrl+C para detener" -ForegroundColor Yellow
    Write-Host ""

    & dotnet run -c Release
} else {
    Write-Host ""
    Write-Host "❌ Error en la compilación" -ForegroundColor Red
    Write-Host $buildResult
    Write-Host ""
    Write-Host "📋 Detalles del error mostrados arriba" -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
}
