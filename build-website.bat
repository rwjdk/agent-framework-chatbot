@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT_NAME=ChatBot.BlazorServerOnly"
set "PROJECT_PATH=%ROOT%src\ChatBot.BlazorServerOnly\ChatBot.BlazorServerOnly.csproj"

echo Stopping existing %PROJECT_NAME% processes...
taskkill /F /IM "%PROJECT_NAME%.exe" >nul 2>nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$projectName = '%PROJECT_NAME%';" ^
  "$root = '%ROOT%'.TrimEnd('\');" ^
  "Get-CimInstance Win32_Process |" ^
  "Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -and ($_.CommandLine -like \"*$projectName*\" -or $_.CommandLine -like \"*$root*\src\ChatBot.BlazorServerOnly*\") } |" ^
  "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"

echo Building website...
dotnet build "%ROOT%chatbot.slnx"
exit /b %ERRORLEVEL%
