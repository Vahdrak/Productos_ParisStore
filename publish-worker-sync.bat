@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing Worker.Sync...
docker build -t proyectsoft/comprasparaguai-worker-sync:latest -t proyectsoft/comprasparaguai-worker-sync:%VERSION% -f Dockerfile.worker-sync .
if %errorlevel% neq 0 (
    echo Error building Worker.Sync image
    exit /b %errorlevel%
)

echo Pushing Worker.Sync image...
docker push proyectsoft/comprasparaguai-worker-sync:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push proyectsoft/comprasparaguai-worker-sync:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo Worker.Sync image built and pushed successfully!
echo - proyectsoft/comprasparaguai-worker-sync:latest
echo - proyectsoft/comprasparaguai-worker-sync:%VERSION%

endlocal
