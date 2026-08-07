@echo off
echo ========================================================
echo   LithoSync Desktop App - Single-File EXE Derleme
echo ========================================================
echo.

cd /d "%~dp0desktop-app\IoTLedController"

echo .NET SDK kontrol ediliyor...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [HATA] .NET 8 SDK bulunamadi!
    echo Lutfen .NET 8 SDK indirin: https://dotnet.microsoft.com/download/dotnet/8.0
    echo Veya GitHub Actions uzerinden tag olusturarak otomatik derletin.
    pause
    exit /b 1
)

echo.
echo Tek dosya (Single-File) EXE derleniyor (win-x64, Self-Contained)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "%~dp0bin_release"

if %errorlevel% equ 0 (
    echo.
    echo ========================================================
    echo   [BASARILI] EXE dosyaniz hazir:
    echo   %~dp0bin_release\LithoSync.exe
    echo ========================================================
    echo.
) else (
    echo [HATA] Derleme sirasinda hata olustu.
)

pause
