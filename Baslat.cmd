@echo off
cd /d "%~dp0"
set SVD_WEB_LAUNCHED=1

sc query SocialVideoDownloader 2>nul | find "RUNNING" >nul
if %errorlevel%==0 goto tray

if exist "%~dp0dist\service\SocialVideoDownloader.Web.exe" (
    start "Social Video Downloader Web" /MIN "%~dp0dist\service\SocialVideoDownloader.Web.exe"
    goto tray
)

start "Social Video Downloader Web" /MIN cmd /c "dotnet run --project \"%~dp0SocialVideoDownloader.Web\SocialVideoDownloader.Web.csproj\""
timeout /t 2 /nobreak >nul

:tray
if exist "%~dp0dist\tray\SocialVideoDownloader.Tray.exe" (
    start "Social Video Downloader Tepsi" "%~dp0dist\tray\SocialVideoDownloader.Tray.exe"
    exit /b 0
)

start "Social Video Downloader Tepsi" /MIN cmd /c "dotnet run --project \"%~dp0SocialVideoDownloader.Tray\SocialVideoDownloader.Tray.csproj\""
