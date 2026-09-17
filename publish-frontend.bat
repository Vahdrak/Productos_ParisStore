@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing Frontend...
docker build -t escarlos/comprasparaguai-frontend:latest -t escarlos/comprasparaguai-frontend:%VERSION% -f Dockerfile.frontend .
if %errorlevel% neq 0 (
    echo Error building Frontend image
    exit /b %errorlevel%
)

echo Pushing Frontend image...
docker push escarlos/comprasparaguai-frontend:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push escarlos/comprasparaguai-frontend:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo Frontend image built and pushed successfully!
echo - escarlos/comprasparaguai-frontend:latest
echo - escarlos/comprasparaguai-frontend:%VERSION%

endlocal
