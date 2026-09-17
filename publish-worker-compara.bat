@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing Worker.Compara...
docker build -t proyectsoft/comprasparaguai-worker-compara:latest -t proyectsoft/comprasparaguai-worker-compara:%VERSION% -f Dockerfile.worker-compara .
if %errorlevel% neq 0 (
    echo Error building Worker.Compara image
    exit /b %errorlevel%
)

echo Pushing Worker.Compara image...
docker push proyectsoft/comprasparaguai-worker-compara:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push proyectsoft/comprasparaguai-worker-compara:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo Worker.Compara image built and pushed successfully!
echo - proyectsoft/comprasparaguai-worker-compara:latest
echo - proyectsoft/comprasparaguai-worker-compara:%VERSION%

endlocal
