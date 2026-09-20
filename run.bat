@echo off
setlocal

echo Starting DJ Visualizer Generator (dev mode)...
echo.
echo   API:      http://localhost:5080
echo   Frontend: http://localhost:5173
echo.
echo Each service opens in its own window. Close those windows (or Ctrl+C in each) to stop.
echo.

if not exist "%~dp0backend\src\Api\DjVisualizer.Api.csproj" (
  echo Could not find backend\src\Api\DjVisualizer.Api.csproj - run this from the repo root.
  pause
  exit /b 1
)

where ffmpeg >nul 2>nul
if errorlevel 1 (
  echo NOTE: ffmpeg was not found on PATH. The app will run, but rendered jobs will fail
  echo       with a clear "ffprobe could not be started" error until ffmpeg is installed.
  echo.
)

rem ASP.NET Core's static-web-assets startup step throws if the Api project's own wwwroot doesn't
rem exist on disk. It's gitignored (run-standalone.bat/Dockerfile populate it from the frontend
rem build for single-container hosting) so a fresh dev checkout doesn't have it, and dotnet run
rem would crash a few seconds after "Building..." with no obvious link to this empty folder.
if not exist "%~dp0backend\src\Api\wwwroot" mkdir "%~dp0backend\src\Api\wwwroot"

rem The API and Worker must agree on where job files live. Without this, each process falls
rem back to a "jobs-data" folder next to its own build output - two different folders - and the
rem Worker never sees jobs the API creates.
set "DJVISUALIZER_JOBS_ROOT=%~dp0jobs-data"

rem ffmpeg's drawtext filter needs real font files. The production defaults are Linux container
rem paths (copied into the Worker image by Dockerfile.worker); on Windows those don't exist, so
rem point at the same bundled fonts from assets\fonts\ instead - keeps dev and prod looking
rem identical rather than falling back to generic Windows system fonts.
set "DJVISUALIZER_FONT_SANS=%~dp0assets\fonts\Poppins-ExtraBold.ttf"
set "DJVISUALIZER_FONT_SERIF=%~dp0assets\fonts\AbrilFatface-Regular.ttf"
set "DJVISUALIZER_FONT_MONO=%~dp0assets\fonts\SpaceMono-Bold.ttf"

start "DJ Visualizer - API"    cmd /k "cd /d %~dp0 && set Jobs__RootPath=%DJVISUALIZER_JOBS_ROOT% && dotnet run --project backend\src\Api --urls http://localhost:5080"
start "DJ Visualizer - Worker" cmd /k "cd /d %~dp0 && set Jobs__RootPath=%DJVISUALIZER_JOBS_ROOT% && set Worker__FontFilePathSansBold=%DJVISUALIZER_FONT_SANS% && set Worker__FontFilePathSerifBold=%DJVISUALIZER_FONT_SERIF% && set Worker__FontFilePathMonoBold=%DJVISUALIZER_FONT_MONO% && dotnet run --project backend\src\Worker"
start "DJ Visualizer - Web"    cmd /k "cd /d %~dp0frontend && npm run dev"

echo Waiting a few seconds for the servers to start...
timeout /t 6 /nobreak >nul

start "" http://localhost:5173

endlocal
