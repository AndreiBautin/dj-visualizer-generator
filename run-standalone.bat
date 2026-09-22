@echo off
setlocal

echo Building DJ Visualizer Generator (standalone, production build)...
echo.
echo   App: http://localhost:5080
echo.
echo One process serves the app and renders jobs - the same shape as the deployed container,
echo just running directly instead of in Docker. Close its window (or Ctrl+C in it) to stop.
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

echo Building the frontend...
pushd "%~dp0frontend"
call npm run build -- --mode standalone
if errorlevel 1 (
  echo Frontend build failed - see the errors above.
  popd
  pause
  exit /b 1
)
popd

rem Mirrors the Dockerfile's `COPY --from=frontend /src/dist ./wwwroot`: the API serves this
rem folder directly when Jobs__SingleContainer is true. Rebuilt fresh every run, never committed.
if exist "%~dp0backend\src\Api\wwwroot" rmdir /s /q "%~dp0backend\src\Api\wwwroot"
xcopy /e /i /q /y "%~dp0frontend\dist" "%~dp0backend\src\Api\wwwroot" >nul

rem Same shared jobs directory and font paths as run.bat - see its comments for why both matter.
set "DJVISUALIZER_JOBS_ROOT=%~dp0jobs-data"
set "DJVISUALIZER_FONT_SANS=%~dp0assets\fonts\Poppins-ExtraBold.ttf"
set "DJVISUALIZER_FONT_SERIF=%~dp0assets\fonts\AbrilFatface-Regular.ttf"
set "DJVISUALIZER_FONT_MONO=%~dp0assets\fonts\SpaceMono-Bold.ttf"

start "DJ Visualizer" cmd /k "cd /d %~dp0 && set Jobs__RootPath=%DJVISUALIZER_JOBS_ROOT% && set Jobs__SingleContainer=true && set Worker__FontFilePathSansBold=%DJVISUALIZER_FONT_SANS% && set Worker__FontFilePathSerifBold=%DJVISUALIZER_FONT_SERIF% && set Worker__FontFilePathMonoBold=%DJVISUALIZER_FONT_MONO% && dotnet run --project backend\src\Api --urls http://localhost:5080"

echo Waiting for the server to start...
timeout /t 6 /nobreak >nul

start "" http://localhost:5080

endlocal
