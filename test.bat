@echo off
rem Run the LuxPresenter Automated Test Suite
setlocal
echo Running LuxPresenter Test Suite...
dotnet run --project "%~dp0Tests\LuxPresenter.Tests.csproj" -c Release
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Test suite failed!
    exit /b %ERRORLEVEL%
)
exit /b 0
