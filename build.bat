@echo off
setlocal
cd /d "%~dp0"

rem Usage:
rem   build.bat         standalone exe, runs on any Windows 10/11 x64 PC (about 70-150 MB, no install needed)
rem   build.bat small   small exe (about 1 MB), needs the .NET 10 Desktop Runtime on the PC

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: the .NET SDK was not found.
  echo Install the .NET 10 SDK from https://dotnet.microsoft.com/download and run this file again.
  exit /b 1
)

if /i "%~1"=="small" goto small

echo Building standalone exe (no .NET runtime needed on the target PC)...
dotnet publish FloatingStopwatch\FloatingStopwatch.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -o dist
goto done

:small
echo Building small exe (needs the .NET 10 Desktop Runtime on the target PC)...
dotnet publish FloatingStopwatch\FloatingStopwatch.csproj -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -o dist

:done
if errorlevel 1 (
  echo.
  echo Build FAILED. Copy the error text above and send it back so it can be fixed.
  exit /b 1
)
echo.
echo Done. Your program is: %~dp0dist\FloatingStopwatch.exe
echo To verify it over a real full-screen slideshow, run tests\Run-Test.bat
endlocal
