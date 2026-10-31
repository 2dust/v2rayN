@echo off
title CF IP Picker for v2rayN
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0cfip.ps1"
if errorlevel 1 pause
