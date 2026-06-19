# compare.ps1 - runs k6 twice (write-behind vs direct-postgres) in QUICK mode,
# then generates a side-by-side Markdown comparison table for the README.
#
# Usage:
#   .\load-tests\compare.ps1
#   .\load-tests\compare.ps1 -DriverVus 300
#
# Assumes: API + all infra containers are already running.

param(
    [int]$DriverVus = 150,
    [string]$Base   = 'http://localhost:5098'
)

$null = New-Item -ItemType Directory -Force load-tests/results

# Wait for API to be ready, then fetch real driver IDs.
# Fake UUIDs cause every telemetry request to hit Postgres (driver metadata cache never warms),
# which exhausts the connection pool and kills everything. Real IDs hit the 10-min cache instead.
Write-Host "Waiting for API at $Base to be ready..." -ForegroundColor Cyan
$driverIds = ''
$ready = $false
for ($attempt = 1; $attempt -le 36; $attempt++) {
    try {
        # Use /health first to confirm connectivity, then check drivers
        $null = Invoke-RestMethod -Uri "$Base/health" -TimeoutSec 8 -ErrorAction Stop
        $resp = Invoke-RestMethod -Uri "$Base/api/drivers?pageSize=200" -TimeoutSec 15 -ErrorAction Stop
        $ids  = @($resp.items | ForEach-Object { $_.id }) | Where-Object { $_ }
        if ($ids.Count -gt 0) {
            $driverIds = $ids -join ','
            Write-Host "  API ready. Found $($ids.Count) real driver IDs." -ForegroundColor Green
            $ready = $true
            break
        }
        Write-Host "  API up but no drivers seeded yet - retrying ($attempt/36)..." -ForegroundColor Yellow
    } catch {
        Write-Host "  Not ready ($attempt/36): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    Start-Sleep -Seconds 5
}
if (-not $ready) {
    Write-Error "API did not become ready after 180s. Check: docker ps, docker logs GridTrack.Api"
    exit 1
}

function Run-K6([bool]$WriteBehind) {
    $label = if ($WriteBehind) { 'write-behind' } else { 'direct-postgres' }
    Write-Host ""
    Write-Host "=== Run: $label ===" -ForegroundColor Magenta
    Write-Host ""

    $k6Args = @(
        'run'
        '--env', "BASE=$Base"
        '--env', "DRIVER_VUS=$DriverVus"
        '--env', "WRITE_BEHIND=$(if ($WriteBehind) { 'true' } else { 'false' })"
        '--env', 'QUICK=true'
    )
    if ($driverIds) { $k6Args += '--env', "DRIVER_IDS=$driverIds" }
    $k6Args += 'load-tests/gridtrack.js'

    & k6 @k6Args
    # k6 exits non-zero when thresholds fail - expected for baseline run
    Write-Host ""
}

Run-K6 -WriteBehind $true
# Copy the write-behind result as the comparison baseline
Copy-Item -Force 'load-tests/results/latest-write-behind.json' 'load-tests/results/comparison-write-behind.json'
Copy-Item -Force 'load-tests/results/latest-write-behind.md' 'load-tests/results/comparison-write-behind.md'

Run-K6 -WriteBehind $false
# Copy the direct-postgres result as the comparison baseline
Copy-Item -Force 'load-tests/results/latest-direct-postgres.json' 'load-tests/results/comparison-direct-postgres.json'
Copy-Item -Force 'load-tests/results/latest-direct-postgres.md' 'load-tests/results/comparison-direct-postgres.md'


# Parse results and emit comparison table

# Returns a stat in ms, rounded to nearest integer
function Get-Stat([string]$JsonPath, [string]$Metric, [string]$Stat) {
    if (-not (Test-Path $JsonPath)) { return 'N/A' }
    $j = Get-Content $JsonPath -Raw | ConvertFrom-Json
    $v = $j.metrics.$Metric.values.$Stat
    if ($null -eq $v) { return 'N/A' }
    return [int][math]::Round($v)
}

function Get-Rps([string]$JsonPath) {
    if (-not (Test-Path $JsonPath)) { return 'N/A' }
    $j = Get-Content $JsonPath -Raw | ConvertFrom-Json
    $v = $j.metrics.http_reqs.values.rate
    if ($null -eq $v) { return 'N/A' }
    return [math]::Round($v, 1)
}

function Speedup([object]$wb, [object]$dp) {
    if ($wb -eq 'N/A' -or $dp -eq 'N/A') { return '-' }
    $wbN = [double]$wb
    $dpN = [double]$dp
    if ($wbN -eq 0) { return '-' }
    $ratio = [math]::Round($dpN / $wbN, 1)
    if ($ratio -gt 1) { return "${ratio}x faster" }
    elseif ($ratio -lt 1) { $inv = [math]::Round($wbN / $dpN, 1); return "${inv}x slower" }
    else { return "same" }
}

function Fmt([object]$v) {
    if ($v -eq 'N/A') { return 'N/A' }
    return "${v} ms"
}

$wbJson = 'load-tests/results/comparison-write-behind.json'
$dpJson = 'load-tests/results/comparison-direct-postgres.json'

# Telemetry POST latency
$telP50Wb  = Get-Stat $wbJson 'gridtrack_driver_tel_latency' 'med'
$telP50Dp  = Get-Stat $dpJson 'gridtrack_driver_tel_latency' 'med'
$telP90Wb  = Get-Stat $wbJson 'gridtrack_driver_tel_latency' 'p(90)'
$telP90Dp  = Get-Stat $dpJson 'gridtrack_driver_tel_latency' 'p(90)'
$telP95Wb  = Get-Stat $wbJson 'gridtrack_driver_tel_latency' 'p(95)'
$telP95Dp  = Get-Stat $dpJson 'gridtrack_driver_tel_latency' 'p(95)'

# Analytics reads latency
$anaP50Wb  = Get-Stat $wbJson 'gridtrack_analytics_latency' 'med'
$anaP50Dp  = Get-Stat $dpJson 'gridtrack_analytics_latency' 'med'
$anaP90Wb  = Get-Stat $wbJson 'gridtrack_analytics_latency' 'p(90)'
$anaP90Dp  = Get-Stat $dpJson 'gridtrack_analytics_latency' 'p(90)'
$anaP95Wb  = Get-Stat $wbJson 'gridtrack_analytics_latency' 'p(95)'
$anaP95Dp  = Get-Stat $dpJson 'gridtrack_analytics_latency' 'p(95)'

# Delivery write latency
$delP50Wb  = Get-Stat $wbJson 'gridtrack_delivery_write_latency' 'med'
$delP50Dp  = Get-Stat $dpJson 'gridtrack_delivery_write_latency' 'med'
$delP90Wb  = Get-Stat $wbJson 'gridtrack_delivery_write_latency' 'p(90)'
$delP90Dp  = Get-Stat $dpJson 'gridtrack_delivery_write_latency' 'p(90)'
$delP95Wb  = Get-Stat $wbJson 'gridtrack_delivery_write_latency' 'p(95)'
$delP95Dp  = Get-Stat $dpJson 'gridtrack_delivery_write_latency' 'p(95)'

# District-group CRUD latency
$dgP50Wb   = Get-Stat $wbJson 'gridtrack_district_group_latency' 'med'
$dgP50Dp   = Get-Stat $dpJson 'gridtrack_district_group_latency' 'med'
$dgP90Wb   = Get-Stat $wbJson 'gridtrack_district_group_latency' 'p(90)'
$dgP90Dp   = Get-Stat $dpJson 'gridtrack_district_group_latency' 'p(90)'
$dgP95Wb   = Get-Stat $wbJson 'gridtrack_district_group_latency' 'p(95)'
$dgP95Dp   = Get-Stat $dpJson 'gridtrack_district_group_latency' 'p(95)'

$rpsWb     = Get-Rps $wbJson
$rpsDp     = Get-Rps $dpJson

$table = @"

## Architecture Comparison - Write-Behind vs Direct Postgres

> **Scenario:** $DriverVus driver VUs (QUICK mode) - $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm UTC'))
> Route calc is async (Wolverine local queue, max 5 parallel OSRM calls) - delivery p95 no longer includes OSRM wait.

| Metric                | p50 WB          | p50 Direct      | p50 speedup            | p90 WB          | p90 Direct      | p90 speedup            | p95 WB          | p95 Direct      | p95 speedup            |
|-----------------------|----------------:|----------------:|------------------------|----------------:|----------------:|------------------------|----------------:|----------------:|------------------------|
| Telemetry POST        | $(Fmt $telP50Wb) | $(Fmt $telP50Dp) | $(Speedup $telP50Wb $telP50Dp) | $(Fmt $telP90Wb) | $(Fmt $telP90Dp) | $(Speedup $telP90Wb $telP90Dp) | $(Fmt $telP95Wb) | $(Fmt $telP95Dp) | $(Speedup $telP95Wb $telP95Dp) |
| Analytics reads       | $(Fmt $anaP50Wb) | $(Fmt $anaP50Dp) | $(Speedup $anaP50Wb $anaP50Dp) | $(Fmt $anaP90Wb) | $(Fmt $anaP90Dp) | $(Speedup $anaP90Wb $anaP90Dp) | $(Fmt $anaP95Wb) | $(Fmt $anaP95Dp) | $(Speedup $anaP95Wb $anaP95Dp) |
| Delivery writes       | $(Fmt $delP50Wb) | $(Fmt $delP50Dp) | $(Speedup $delP50Wb $delP50Dp) | $(Fmt $delP90Wb) | $(Fmt $delP90Dp) | $(Speedup $delP90Wb $delP90Dp) | $(Fmt $delP95Wb) | $(Fmt $delP95Dp) | $(Speedup $delP95Wb $delP95Dp) |
| District-group CRUD   | $(Fmt $dgP50Wb)  | $(Fmt $dgP50Dp)  | $(Speedup $dgP50Wb $dgP50Dp)   | $(Fmt $dgP90Wb)  | $(Fmt $dgP90Dp)  | $(Speedup $dgP90Wb $dgP90Dp)   | $(Fmt $dgP95Wb)  | $(Fmt $dgP95Dp)  | $(Speedup $dgP95Wb $dgP95Dp)   |
| **Throughput (req/s)** | **$rpsWb**     |                 |                        |                 | **$rpsDp**      |                        |                 |                 |                        |

"@

$outPath = 'load-tests/results/comparison.md'
$table | Out-File -FilePath $outPath -Encoding utf8
Write-Host $table -ForegroundColor Cyan
Write-Host "Saved to $outPath" -ForegroundColor Green
