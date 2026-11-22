@echo off
rem Runs the full-screen PowerPoint slideshow test. Needs desktop PowerPoint. Takes about 30 seconds.
rem Do not touch the mouse or keyboard while it runs.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-Slideshow-v3.ps1"
echo.
echo Report and screenshots: %~dp0test-results
pause
