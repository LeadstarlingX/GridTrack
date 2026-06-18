# Usage: .\load-tests\stress.ps1 [DRIVER_VUS] [BASE_URL]
#   .\load-tests\stress.ps1              # 100 VUs, localhost:5098
#   .\load-tests\stress.ps1 500          # 500 VUs (real stress)
#
# Assumes the API is already running (dotnet run --project GridTrack.Api).

param(
    [int]$DriverVus = 100,
    [string]$Base   = 'http://localhost:5098'
)

Write-Host "Starting infra containers..." -ForegroundColor Cyan
docker compose up -d gridtrack.db gridtrack.redis gridtrack.clickhouse gridtrack.seq
if ($LASTEXITCODE -ne 0) { Write-Error "docker compose failed"; exit 1 }

Write-Host ""
Write-Host "Running k6 stress test ($DriverVus driver VUs -> $Base)" -ForegroundColor Cyan
Write-Host ""

k6 run `
    --env BASE=$Base `
    --env DRIVER_VUS=$DriverVus `
    load-tests/gridtrack.js
