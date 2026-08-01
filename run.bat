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

rem The API and Worker must agree on where job files live. Without this, each process falls
rem back to a "jobs-data" folder next to its own build output - two different folders - and the
rem Worker never sees jobs the API creates.
set "DJVISUALIZER_JOBS_ROOT=%~dp0jobs-data"

rem ffmpeg's drawtext filter needs real font files. The production defaults are Linux container
rem paths (installed via apt in Docker); on Windows they fail with a Fontconfig error, so point
rem at real Windows fonts instead - one per caption font choice offered in the UI.
set "DJVISUALIZER_FONT_SANS=C:\Windows\Fonts\segoeuib.ttf"
if not exist "%DJVISUALIZER_FONT_SANS%" set "DJVISUALIZER_FONT_SANS=C:\Windows\Fonts\arialbd.ttf"
set "DJVISUALIZER_FONT_SERIF=C:\Windows\Fonts\georgiab.ttf"
if not exist "%DJVISUALIZER_FONT_SERIF%" set "DJVISUALIZER_FONT_SERIF=C:\Windows\Fonts\timesbd.ttf"
set "DJVISUALIZER_FONT_MONO=C:\Windows\Fonts\consolab.ttf"

start "DJ Visualizer - API"    cmd /k "cd /d %~dp0 && set Jobs__RootPath=%DJVISUALIZER_JOBS_ROOT% && dotnet run --project backend\src\Api --urls http://localhost:5080"
start "DJ Visualizer - Worker" cmd /k "cd /d %~dp0 && set Jobs__RootPath=%DJVISUALIZER_JOBS_ROOT% && set Worker__FontFilePathSansBold=%DJVISUALIZER_FONT_SANS% && set Worker__FontFilePathSerifBold=%DJVISUALIZER_FONT_SERIF% && set Worker__FontFilePathMonoBold=%DJVISUALIZER_FONT_MONO% && dotnet run --project backend\src\Worker"
start "DJ Visualizer - Web"    cmd /k "cd /d %~dp0frontend && npm run dev"

echo Waiting a few seconds for the servers to start...
timeout /t 6 /nobreak >nul

start "" http://localhost:5173

endlocal
