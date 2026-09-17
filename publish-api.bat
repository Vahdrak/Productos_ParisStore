@echo off
setlocal enabledelayedexpansion

REM Get version from git tag or use default
for /f "tokens=*" %%A in ('git describe --tags --abbrev=0 2^>nul') do set VERSION=%%A
if "!VERSION!"=="" set VERSION=latest

echo Building and pushing API...
docker build -t escarlos/comprasparaguai-api:latest -t escarlos/comprasparaguai-api:%VERSION% -f Dockerfile.api .
if %errorlevel% neq 0 (
    echo Error building API image
    exit /b %errorlevel%
)

echo Pushing API image...
docker push escarlos/comprasparaguai-api:latest
if %errorlevel% neq 0 (
    echo Error pushing latest tag
    exit /b %errorlevel%
)

docker push escarlos/comprasparaguai-api:%VERSION%
if %errorlevel% neq 0 (
    echo Error pushing version tag
    exit /b %errorlevel%
)

echo API image built and pushed successfully!
echo - escarlos/comprasparaguai-api:latest
echo - escarlos/comprasparaguai-api:%VERSION%

endlocal
