$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\OpenClawFarm.Server\OpenClawFarm.Server.csproj"
$outDir = Join-Path $root "dist\OpenClawFarm"

Write-Host "Publishing OpenClawFarm.exe (win-x64, single-file, self-contained)..." -ForegroundColor Cyan
dotnet publish $project -c Release -p:PublishProfile=win-x64-single

$exe = Join-Path $outDir "OpenClawFarm.exe"
if (Test-Path $exe) {
    Write-Host ""
    Write-Host "Done: $exe" -ForegroundColor Green
    Write-Host "Double-click to play. Right-click tray icon to exit." -ForegroundColor Gray
} else {
    Write-Error "Publish failed — OpenClawFarm.exe not found."
}
