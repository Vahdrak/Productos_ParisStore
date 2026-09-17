@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing Worker.Verify...
docker build -t escarlos/comprasparaguai-worker-verify:latest -t escarlos/comprasparaguai-worker-verify:%VERSION% -f Dockerfile.worker-verify .
if %errorlevel% neq 0 (
    echo Error building Worker.Verify image
    exit /b %errorlevel%
)

echo Pushing Worker.Verify image...
docker push escarlos/comprasparaguai-worker-verify:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push escarlos/comprasparaguai-worker-verify:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo Worker.Verify image built and pushed successfully!
echo - escarlos/comprasparaguai-worker-verify:latest
echo - escarlos/comprasparaguai-worker-verify:%VERSION%

endlocal
