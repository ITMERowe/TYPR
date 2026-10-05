@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK is required: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)

dotnet publish TYPR.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
if errorlevel 1 exit /b 1

echo.
echo Built executable:
echo bin\Release\net8.0-windows\win-x64\publish\TYPR.exe
pause
