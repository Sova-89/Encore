@echo off
rem Build Encore.dll. Usage: build.bat "path to the game folder"
rem The game needs BepInEx and Gods of Pharloom installed.
if "%~1"=="" goto usage
dotnet build "%~dp0src\Encore.csproj" -c Release -p:GameDir="%~1"
if errorlevel 1 exit /b 1
echo.
echo Done: %~dp0src\bin\Release\netstandard2.1\Encore.dll
exit /b 0

:usage
echo Usage: build.bat "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong"
exit /b 1
