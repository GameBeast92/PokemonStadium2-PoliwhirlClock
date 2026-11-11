@echo off
rem Builds PoliwhirlClock.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
rem 1) refreshes the art/icon in assets\ (optional: needs Python + Pillow)  2) closes a running clock  3) compiles.
rem Nothing else is needed: the compiler is part of Windows 10/11.
setlocal
cd /d "%~dp0"

echo [1/3] Refreshing artwork and icon...
where python >nul 2>nul
if errorlevel 1 (
  echo   Python not found - using the art already in assets\
) else (
  python -I tools\make_assets.py || goto :fail
)

if exist "%~dp0PoliwhirlClock.exe" (
  echo [2/3] Closing a running clock, if any...
  "%~dp0PoliwhirlClock.exe" --exit
  ping -n 3 127.0.0.1 >nul
)

echo [3/3] Compiling (assets\ is built into the exe)...
rem every asset becomes an embedded resource named assets.<file>
set "RES="
for %%F in (assets\*.png assets\*.ini assets\*.wav assets\*.ico) do (
  if /i not "%%~nxF"=="icon_preview_256.png" call set "RES=%%RES%% /resource:%%F,assets.%%~nxF"
)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
  /win32icon:assets\poliwhirl.ico /win32manifest:src\app.manifest ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll %RES% ^
  /out:PoliwhirlClock.exe src\PoliwhirlClock.cs || goto :fail
copy /y src\App.config PoliwhirlClock.exe.config >nul

echo.
echo Built: %CD%\PoliwhirlClock.exe
exit /b 0

:fail
echo.
echo BUILD FAILED
exit /b 1
