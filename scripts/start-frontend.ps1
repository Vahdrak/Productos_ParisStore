#!/usr/bin/env powershell
# Script para iniciar el Frontend de Compras Paraguai

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   Iniciando Frontend - Compras Paraguai   ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$frontendPath = "C:\Users\MARIO\Documents\ComprasParaguai\frontend\comprasparaguai-front"

# 1. Verificar que npm existe
Write-Host "🔍 Verificando Node.js/npm..." -ForegroundColor Yellow
$npmVersion = & npm --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ npm v$npmVersion" -ForegroundColor Green
} else {
    Write-Host "❌ npm no está instalado" -ForegroundColor Red
    Write-Host "   Descárgalo desde: https://nodejs.org" -ForegroundColor Yellow
    Read-Host "Presiona Enter para salir"
    exit 1
}

# 2. Ir al directorio del frontend
Set-Location $frontendPath

# 3. Instalar dependencias si es necesario
if (-not (Test-Path "node_modules")) {
    Write-Host ""
    Write-Host "📦 Instalando dependencias..." -ForegroundColor Yellow
    Write-Host ""
    & npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Error al instalar dependencias" -ForegroundColor Red
        Read-Host "Presiona Enter para salir"
        exit 1
    }
} else {
    Write-Host "✅ Dependencias ya instaladas" -ForegroundColor Green
}

# 4. Iniciar servidor
Write-Host ""
Write-Host "🚀 Iniciando servidor..." -ForegroundColor Cyan
Write-Host "   📍 Frontend disponible en: http://localhost:4200" -ForegroundColor Green
Write-Host "   🔗 Conecta con el API en: http://localhost:5000" -ForegroundColor Green
Write-Host ""
Write-Host "⏹️  Presiona Ctrl+C para detener" -ForegroundColor Yellow
Write-Host ""

& npm start
