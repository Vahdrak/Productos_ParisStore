@echo off
setlocal enabledelayedexpansion

echo.
echo ======================================
echo Building and pushing all images
echo ======================================
echo.

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Version: %VERSION%
echo.

REM Build and push API
echo [1/6] Building API...
docker build -t proyectsoft/comprasparaguai-api:latest -t proyectsoft/comprasparaguai-api:%VERSION% -f Dockerfile.api .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-api:latest
docker push proyectsoft/comprasparaguai-api:%VERSION%
echo API pushed successfully!
echo.

REM Build and push Worker.Sync
echo [2/6] Building Worker.Sync...
docker build -t proyectsoft/comprasparaguai-worker-sync:latest -t proyectsoft/comprasparaguai-worker-sync:%VERSION% -f Dockerfile.worker-sync .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-worker-sync:latest
docker push proyectsoft/comprasparaguai-worker-sync:%VERSION%
echo Worker.Sync pushed successfully!
echo.

REM Build and push Worker.Compras
echo [3/6] Building Worker.Compras...
docker build -t proyectsoft/comprasparaguai-worker-compras:latest -t proyectsoft/comprasparaguai-worker-compras:%VERSION% -f Dockerfile.worker-compras .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-worker-compras:latest
docker push proyectsoft/comprasparaguai-worker-compras:%VERSION%
echo Worker.Compras pushed successfully!
echo.

REM Build and push Worker.Compara
echo [4/6] Building Worker.Compara...
docker build -t proyectsoft/comprasparaguai-worker-compara:latest -t proyectsoft/comprasparaguai-worker-compara:%VERSION% -f Dockerfile.worker-compara .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-worker-compara:latest
docker push proyectsoft/comprasparaguai-worker-compara:%VERSION%
echo Worker.Compara pushed successfully!
echo.

REM Build and push Worker.Verify
echo [5/6] Building Worker.Verify...
docker build -t proyectsoft/comprasparaguai-worker-verify:latest -t proyectsoft/comprasparaguai-worker-verify:%VERSION% -f Dockerfile.worker-verify .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-worker-verify:latest
docker push proyectsoft/comprasparaguai-worker-verify:%VERSION%
echo Worker.Verify pushed successfully!
echo.

REM Build and push Frontend
echo [6/6] Building Frontend...
docker build -t proyectsoft/comprasparaguai-frontend:latest -t proyectsoft/comprasparaguai-frontend:%VERSION% -f Dockerfile.frontend .
if %errorlevel% neq 0 exit /b %errorlevel%
docker push proyectsoft/comprasparaguai-frontend:latest
docker push proyectsoft/comprasparaguai-frontend:%VERSION%
echo Frontend pushed successfully!
echo.

echo.
echo ======================================
echo All images built and pushed!
echo ======================================
echo.
echo Published images:
echo   - proyectsoft/comprasparaguai-api:%VERSION%
echo   - proyectsoft/comprasparaguai-worker-sync:%VERSION%
echo   - proyectsoft/comprasparaguai-worker-compras:%VERSION%
echo   - proyectsoft/comprasparaguai-worker-compara:%VERSION%
echo   - proyectsoft/comprasparaguai-worker-verify:%VERSION%
echo   - proyectsoft/comprasparaguai-frontend:%VERSION%
echo.

endlocal
