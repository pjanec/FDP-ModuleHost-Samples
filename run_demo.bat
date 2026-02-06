@echo off
cd /d "%~dp0"
echo Building project first to avoid build contention...
dotnet build Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --nologo -v q

echo Starting Node 100 (Alpha)...
start "Node 100 (Alpha)" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 100

echo Waiting 2 seconds...
timeout /t 2 /nobreak >nul

echo Starting Node 200 (Bravo)...
start "Node 200 (Bravo)" dotnet run --project Fdp.Examples.NetworkDemo\Fdp.Examples.NetworkDemo.csproj --no-build -- 200

echo Demo running in separate windows.
