@echo off
REM Tapster Launcher
REM Uses dotnet run to handle MSIX deployment automatically

cd /d "%~dp0"
dotnet run --project Tapster.Fluent.csproj
