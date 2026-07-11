@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT_NAME=ChatBot.BlazorServerOnly"

echo Stopping existing chatbot processes...
taskkill /F /IM "%PROJECT_NAME%.exe" >nul 2>nul
taskkill /F /IM "ChatBot.Api.exe" >nul 2>nul
taskkill /F /IM "AppHost.exe" >nul 2>nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root = '%ROOT%'.TrimEnd('\');" ^
  "Get-CimInstance Win32_Process |" ^
  "Where-Object { $_.Name -in @('dotnet.exe', 'node.exe', 'ChatBot.Api.exe', 'ChatBot.BlazorServerOnly.exe', 'AppHost.exe') -and $_.CommandLine -and ($_.CommandLine -like \"*$root*\src\ChatBot.BlazorServerOnly*\" -or $_.CommandLine -like \"*$root*\src\ChatBot.Api*\" -or $_.CommandLine -like \"*$root*\src\AppHost*\" -or $_.CommandLine -like \"*$root*\src\ChatBot.React*\") } |" ^
  "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"

echo Building website...
pushd "%ROOT%src\ChatBot.React"
call npm install
if errorlevel 1 (
  popd
  exit /b %ERRORLEVEL%
)
call npm run build
if errorlevel 1 (
  popd
  exit /b %ERRORLEVEL%
)
popd

dotnet build "%ROOT%chatbot.slnx"
exit /b %ERRORLEVEL%
