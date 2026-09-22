param([switch]$NoBrowser, [string]$JobsRootPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
foreach ($tool in @('node', 'npm', 'dotnet', 'ffmpeg', 'ffprobe')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Missing $tool. Install Node: https://nodejs.org .NET: https://dotnet.microsoft.com/download and FFmpeg: https://ffmpeg.org/download.html; then reopen your terminal."
    }
}
$port = 5080
$listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, $port)
try { $listener.Start() } catch { throw "Port $port is already in use. Stop that server before starting Spinner." } finally { $listener.Stop() }
& npm.cmd ci --prefix frontend
if ($LASTEXITCODE -ne 0) { throw 'Dependency install failed.' }
$env:VITE_SAMPLE_ENABLED = 'true'
& npm.cmd run build --prefix frontend -- --mode standalone
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
& dotnet restore backend/src/Api/DjVisualizer.Api.csproj --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }
& dotnet build backend/src/Api/DjVisualizer.Api.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
if (-not $JobsRootPath) { $JobsRootPath = Join-Path $repo 'jobs-data' }
$env:Jobs__RootPath = $JobsRootPath
$env:Jobs__SingleContainer = 'true'
$env:Demo__Enabled = 'true'
$env:Demo__AudioFilePath = Join-Path $repo 'assets/demo/sample-mix.mp3'
$env:Demo__ArtworkFilePath = Join-Path $repo 'assets/demo/sample-artwork.png'
$env:Worker__FontFilePathSansBold = Join-Path $repo 'assets/fonts/Poppins-ExtraBold.ttf'
$env:Worker__FontFilePathSerifBold = Join-Path $repo 'assets/fonts/AbrilFatface-Regular.ttf'
$env:Worker__FontFilePathMonoBold = Join-Path $repo 'assets/fonts/SpaceMono-Bold.ttf'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$logs = Join-Path $repo '.portfolio-demo'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$server = Start-Process dotnet -ArgumentList @('"'+(Join-Path $repo 'backend/src/Api/bin/Release/net9.0/DjVisualizer.Api.dll')+'"', '--urls', "http://localhost:$port", '--webroot', '"'+(Join-Path $repo 'frontend/dist')+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'server.log') -RedirectStandardError (Join-Path $logs 'server-error.log')
$server.Id | Set-Content (Join-Path $logs 'server.pid')
$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    if ($server.HasExited) { throw "Spinner exited. See $logs/server-error.log" }
    try {
        $health = Invoke-WebRequest "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 2
        if ($health.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) { Stop-Process -Id $server.Id; throw "Startup timed out. See $logs/server.log" }
Write-Host "Spinner is ready at http://localhost:$port (process $($server.Id)). Logs: $logs"
Write-Host "To stop: Stop-Process -Id $($server.Id)"
if (-not $NoBrowser) { Start-Process "http://localhost:$port" }
