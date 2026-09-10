# Script PowerShell para iniciar API, Worker y Frontend en ventanas separadas
# Uso: .\start-all.ps1

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Compras Paraguai - Inicio Completo" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Iniciando API en ventana separada..." -ForegroundColor Green
Start-Process powershell -ArgumentList @"
    Set-Location 'C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Api'
    Write-Host 'Compras Paraguai - API' -ForegroundColor Cyan
    Write-Host 'Puerto: http://localhost:5000' -ForegroundColor Yellow
    Write-Host '---' -ForegroundColor Gray
    dotnet run -c Release
"@

Start-Sleep -Seconds 5

Write-Host "Iniciando Worker en ventana separada..." -ForegroundColor Green
Start-Process powershell -ArgumentList @"
    Set-Location 'C:\Users\MARIO\Documents\ComprasParaguai\src\ComprasParaguai.Worker'
    Write-Host 'Compras Paraguai - Worker Service' -ForegroundColor Cyan
    Write-Host 'Sincronizando cada 15 minutos' -ForegroundColor Yellow
    Write-Host '---' -ForegroundColor Gray
    dotnet run -c Release
"@

Start-Sleep -Seconds 3

Write-Host "Iniciando Frontend en ventana separada..." -ForegroundColor Green
Start-Process powershell -ArgumentList @"
    Set-Location 'C:\Users\MARIO\Documents\ComprasParaguai\frontend\comprasparaguai-front'
    Write-Host 'Compras Paraguai - Frontend' -ForegroundColor Cyan
    Write-Host 'URL: http://localhost:4200' -ForegroundColor Yellow
    Write-Host '---' -ForegroundColor Gray
    npm start
"@

Write-Host ""
Write-Host "✅ Los 3 servicios están iniciándose..." -ForegroundColor Green
Write-Host ""
Write-Host "📊 API:     http://localhost:5000" -ForegroundColor Cyan
Write-Host "🤖 Worker:  Sincronizando (verifica en terminal)" -ForegroundColor Cyan
Write-Host "🌐 Frontend: http://localhost:4200" -ForegroundColor Cyan
Write-Host ""
Write-Host "Presiona cualquier tecla para cerrar esta ventana" -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
