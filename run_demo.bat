@echo off
cd /d "%~dp0"

echo ==========================================
echo   FDP Network Demo Launcher
echo ==========================================

echo Building project...
dotnet build Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --nologo -v q
if %errorlevel% neq 0 (
    echo Build failed.
    pause
    exit /b %errorlevel%
)

echo.
echo Select Mode:
echo 1. Live Distributed Demo (Record)
echo 2. Replay Distributed Demo (Playback)
echo.
set /p choice="Enter choice (1 or 2): "

if "%choice%"=="2" goto REPLAY

:LIVE
echo.
echo Starting Node 100 (Alpha) [LIVE]...
start "Node 100 (Alpha)" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 100 live

echo Waiting 2 seconds for discovery...
timeout /t 2 /nobreak >nul

echo Starting Node 200 (Bravo) [LIVE]...
start "Node 200 (Bravo)" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 200 live
goto END

:REPLAY
echo.
echo Checking for recordings...
if not exist "node_100.fdp" (
    echo WARNING: node_100.fdp not found. Replay might be empty.
)

echo Starting Node 100 (Alpha) [REPLAY]...
start "Node 100 (Alpha) - Replay" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 100 replay

echo Starting Node 200 (Bravo) [REPLAY]...
start "Node 200 (Bravo) - Replay" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 200 replay
goto END

:END
echo.
echo Demo windows launched.
