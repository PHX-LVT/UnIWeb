@echo off
setlocal
set SCRIPT_DIR=%~dp0

dotnet publish "%SCRIPT_DIR%DemoDbImporter.csproj" -c Release -o "%SCRIPT_DIR%publish"

echo.
echo Published to %SCRIPT_DIR%publish
pause
