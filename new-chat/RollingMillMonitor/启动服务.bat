@echo off
chcp 65001 >nul
title 轧机监控平台
cd /d %~dp0
echo ============================================
echo   轧机监控平台 正在启动...
echo   启动完成后请用浏览器打开： http://localhost:5200
echo   按 Ctrl+C 可停止服务
echo ============================================
dotnet run --urls http://localhost:5200
pause
