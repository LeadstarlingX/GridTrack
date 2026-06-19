# Usage: .\load-tests\stress.ps1 [DRIVER_VUS] [BASE_URL]
#   .\load-tests\stress.ps1              # 100 VUs, localhost:5098 (write-behind)
#   .\load-tests\stress.ps1 1000          # 1000 VUs
#   .\load-tests\stress.ps1 500 -Sync    # baseline: direct Postgres, no buffer
#
# Assumes: API + all infra containers are already running.
# Start the API with simulation off:
#   $env:Simulation__Enabled='false'; dotnet run --project GridTrack.Api --launch-profile http

param(
    [int]$DriverVus = 500,
    [string]$Base   = 'http://localhost:5098',
    [switch]$Sync
)

$null = New-Item -ItemType Directory -Force load-tests/results

# Fetch real driver IDs - essential so telemetry hits the 10-min metadata cache
# instead of firing a Postgres lookup on every request.
Write-Host "Waiting for API at $Base..." -ForegroundColor Cyan
$driverIds = ''
$ready = $false
for ($attempt = 1; $attempt -le 36; $attempt++) {
    try {
        $null = Invoke-RestMethod -Uri "$Base/health" -TimeoutSec 8 -ErrorAction Stop
        $resp = Invoke-RestMethod -Uri "$Base/api/drivers?pageSize=200" -TimeoutSec 15 -ErrorAction Stop
        $ids  = @($resp.items | ForEach-Object { $_.id }) | Where-Object { $_ }
        if ($ids.Count -gt 0) {
            $driverIds = $ids -join ','
            Write-Host "  Ready. Found $($ids.Count) real driver IDs." -ForegroundColor Green
            $ready = $true
            break
        }
        Write-Host "  No drivers yet - retrying ($attempt/36)..." -ForegroundColor Yellow
    } catch {
        Write-Host "  Not ready ($attempt/36): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    Start-Sleep -Seconds 5
}
if (-not $ready) {
    Write-Warning "API not ready after 180s - falling back to fake IDs (telemetry will hit Postgres on every request)."
}

$modeLabel = if ($Sync) { 'direct-postgres (baseline)' } else { 'write-behind' }
Write-Host ""
Write-Host "Running k6 | mode=$modeLabel | $DriverVus driver VUs -> $Base (HIGH STRESS)" -ForegroundColor Cyan
Write-Host ""

$k6Args = @(
    'run'
    '--env', "BASE=$Base"
    '--env', "DRIVER_VUS=$DriverVus"
    '--env', "WRITE_BEHIND=$(if ($Sync) { 'false' } else { 'true' })"
)
if ($driverIds) { $k6Args += '--env', "DRIVER_IDS=$driverIds" }
$k6Args += 'load-tests/gridtrack.js'

& k6 @k6Args
