@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Test-Runtime.ps1" -Interactive -NoRanged
if errorlevel 1 pause
