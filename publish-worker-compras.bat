@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing Worker.Compras...
docker build -t proyectsoft/comprasparaguai-worker-compras:latest -t proyectsoft/comprasparaguai-worker-compras:%VERSION% -f Dockerfile.worker-compras .
if %errorlevel% neq 0 (
    echo Error building Worker.Compras image
    exit /b %errorlevel%
)

echo Pushing Worker.Compras image...
docker push proyectsoft/comprasparaguai-worker-compras:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push proyectsoft/comprasparaguai-worker-compras:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo Worker.Compras image built and pushed successfully!
echo - proyectsoft/comprasparaguai-worker-compras:latest
echo - proyectsoft/comprasparaguai-worker-compras:%VERSION%

endlocal
