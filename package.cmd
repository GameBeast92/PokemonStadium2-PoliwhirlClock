@echo off
rem Builds PoliwhirlClock.exe (all art built in) and makes the release file
rem   dist\PoliwhirlClock-<version>.zip  = the exe + .exe.config + README / DISCLAIMER / LICENSE
rem Usage: package.cmd 1.0.0      (closes a running clock, like build.cmd)
setlocal
cd /d "%~dp0"
set "VER=%~1"
if "%VER%"=="" set "VER=1.0.0"

call "%~dp0build.cmd" || exit /b 1

echo.
echo Packing version %VER%...
set "DIST=%~dp0dist"
set "STAGE=%DIST%\PoliwhirlClock"
if exist "%DIST%" rmdir /s /q "%DIST%"
mkdir "%STAGE%" || exit /b 1

for %%F in (PoliwhirlClock.exe PoliwhirlClock.exe.config README.md DISCLAIMER.md LICENSE) do (
  copy /y "%%F" "%STAGE%\" >nul || exit /b 1
)

powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%' -DestinationPath '%DIST%\PoliwhirlClock-%VER%.zip' -Force" || exit /b 1
echo Packed: %DIST%\PoliwhirlClock-%VER%.zip
exit /b 0
