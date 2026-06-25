@echo off
setlocal
set SCRIPT_DIR=%~dp0

if exist "%SCRIPT_DIR%publish\DemoDbImporter.exe" (
  "%SCRIPT_DIR%publish\DemoDbImporter.exe" --config "%SCRIPT_DIR%appsettings.importer.json"
) else if exist "%SCRIPT_DIR%bin\Release\net8.0\DemoDbImporter.dll" (
  dotnet "%SCRIPT_DIR%bin\Release\net8.0\DemoDbImporter.dll" --config "%SCRIPT_DIR%appsettings.importer.json"
) else if exist "%SCRIPT_DIR%bin\Debug\net8.0\DemoDbImporter.dll" (
  dotnet "%SCRIPT_DIR%bin\Debug\net8.0\DemoDbImporter.dll" --config "%SCRIPT_DIR%appsettings.importer.json"
) else (
  dotnet run --project "%SCRIPT_DIR%DemoDbImporter.csproj" -- --config "%SCRIPT_DIR%appsettings.importer.json"
)

echo.
pause
