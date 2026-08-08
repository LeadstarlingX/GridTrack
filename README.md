In response to trends in the gaming industry, as of 1st of September 2026, GridTrack will cease 
production of the CD pipeline into docker containers and shift to production of floppy disks.

Developers can still order a pigeon carrier to receive the latest updates within a stainless-steel
container right to their doorstep.


# GridTrack

AI Agent-integrable real-time fleet tracking & dispatch for a Damascus delivery fleet. A partner company's
backend POSTs driver GPS telemetry (B2B, server-to-server); GridTrack tracks deliveries
through their lifecycle, detects anomalies, forecasts district demand, and surfaces
AI-assisted dispatch decisions — designed to stay correct and smooth at a target load of
**10,000 concurrent drivers and 50 dashboard observers on a single host**.

Current k6 tests results are limited by the GitHub CI runner,  not the system's real performance :(

## Related repositories

- **GridTrack** (this repo) — .NET 9 dispatch API.
- [gridtrack-forecasting](https://github.com/LeadstarlingX/gridtrack-forecasting) — Python AI/ML pipeline (urgency scoring, demand-surge detection, incident clustering, staffing forecast, chatbot).
- [GridTrack.Web](https://github.com/LeadstarlingX/GridTrack.Web) — React real-time operator dashboard (SignalR live map).

## What it does

- **Real-time tracking** — driver positions and delivery status pushed to the dashboard over SignalR, scoped per district group.
- **Dispatch** — weighted nearest-driver assignment (proximity · on-time rate · load · shift) over PostGIS + H3.
- **Anomaly & demand intelligence** — 5 anomaly types, urgency scoring, rolling-z-score demand surge, incident clustering, and a staffing forecast, produced by the Python pipeline and pushed live.
- **Route economics** — every assigned delivery gets an OSRM route, ETA, and a cost (base + per-km + per-minute, SYP).
- **Operator analytics** — KPI summary, trends, district volume, cancellation/anomaly breakdowns, driver utilization, and a historical H3 heatmap with date + intra-day hour filters.

## Architecture & development methodology

**Clean Architecture**, dependencies pointing inward:

```
Domain  ←  Application  ←  Infrastructure
                ↑               ↑
            Presentation  ←  Api (host)
```

- **CQRS via Wolverine** — controllers are thin; every action dispatches a command or query through the message bus. Write path: EF Core aggregates raise domain events that cascade to integration handlers. Read path: Dapper read services return DTOs (no tracking).
- **Telemetry hot path** — HTTP → in-memory write-behind buffer (returns immediately) → `PositionFlushService` batches to Postgres + ClickHouse every 5 s; a Redis Stream fans positions out to SignalR. Nothing on the request path waits on the database.
- **Spatial** — PostGIS geometry + H3 indexing for dispatch and density and spatial querying in O(1) .
- **Validation** — FluentValidation runs at the HTTP boundary and returns `400` with field errors.
  - **Test pyramid (TUnit):** domain / application / infrastructure unit tests · integration tests on **Testcontainers** (real Postgres + Redis) · end-to-end tests against the full stack including the Python container (using cross repo integration tests for the CI)· architecture & naming-convention tests enforced in CI.
- **CI/CD** — GitHub Actions builds, runs the full test suite with coverage on every push, and runs a k6 load test on pushes to `master`.

## Running locally

**Full stack (Docker):**
```bash
docker compose up -d            # db, redis, rabbitmq, clickhouse, osrm, seq, api, python
# then, in GridTrack.Web:
npm run dev                     # dashboard on :5173
```
API on `:5098`, Python on `:8000`, Seq on `:8080`. Secrets (incl. `GROQ_API_KEY`) come from `.env` at the repo root. The DB is cleared and re-seeded with fresh data on every API startup.

**Infra-only + local services (faster inner loop):**
```bash
docker compose up -d gridtrack.db gridtrack.redis gridtrack.rabbitmq
dotnet run --project GridTrack.Api          # :5098
uvicorn app.main:app --reload               # gridtrack-forecasting, :8000
npm run dev                                  # GridTrack.Web, :5173
```

**Tests & load tests** (via [Task](https://taskfile.dev), no need to type commands manually):
```bash
task test-all            # full TUnit suite
task coverage            # suite + HTML/Cobertura coverage report
task k6                  # one QUICK k6 pass against a running stack
task k6-compare          # write-behind vs direct-postgres
task k6-stress           # high-VU stress
task k6-throughput       # arrival-rate ceiling
```

## MCP server — AI agent integration

The Python pipeline ([gridtrack-forecasting](https://github.com/LeadstarlingX/gridtrack-forecasting))
exposes an **MCP server** so AI agents can query live fleet data as native tools.

- **Endpoint:** `http://localhost:8000/mcp/sse` (SSE transport)
- **Auth:** `Authorization: Bearer <MCP_API_KEY>` — key is in `.env` at this repo's root
- **Tools:** `get_active_drivers`, `get_anomalies`, `get_deliveries_summary`, `get_district_status`, `get_stalled_drivers`, `get_activity_trend`, `get_peak_hours`

### Claude Code

Add to `.claude/settings.json` (or `~/.claude/settings.json` for global):

```json
{
  "mcpServers": {
    "gridtrack": {
      "type": "sse",
      "url": "http://localhost:8000/mcp/sse",
      "headers": { "Authorization": "Bearer gridtrack-mcp-2026" }
    }
  }
}
```

### Claude Desktop (Windows)

Claude Desktop requires a stdio bridge because it does not support SSE servers natively.

**1. Install `mcp-remote` globally (one-time):**
```bash
npm install -g mcp-remote
```

**2. Add to `%APPDATA%\Claude\claude_desktop_config.json`:**
```json
{
  "mcpServers": {
    "gridtrack": {
      "command": "mcp-remote.cmd",
      "args": [
        "http://localhost:8000/mcp/sse",
        "--header",
        "Authorization: Bearer gridtrack-mcp-2026"
      ]
    }
  }
}
```

**3.** Make sure the Docker stack is running (`docker compose up -d`), then fully restart Claude Desktop.

> **Why `mcp-remote.cmd` instead of `npx`?** Claude Desktop launches servers via `cmd.exe`. On Windows, paths with spaces (e.g. `C:\Program Files\nodejs\npx`) break the command lookup. Installing `mcp-remote` globally puts `mcp-remote.cmd` in `%APPDATA%\Roaming\npm\` — no spaces, no quoting issues.

Full documentation and client examples: [gridtrack-forecasting README](https://github.com/LeadstarlingX/gridtrack-forecasting#mcp-server--ai-agent-integration).

## Load testing & honest benchmarks

The benchmark tables below are generated from real k6 runs in CI. We deliberately keep them
**honest** rather than flattering:

- **Latency is environment-bound, so it is reported, not gated.** Every container *and* the
  k6 generator share one small CI runner (see specs below). Absolute milliseconds reflect that
  shared box — they are **not** comparable across hardware. Only **error rate / correctness**
  is thresholded: a pass means the system served the load without failing requests.
- **The write-behind vs direct-postgres comparison is apples-to-apples.** Both endpoints have
  identical semantics and both ultimately persist to Postgres; the only variable is the *write
  strategy* (buffered+batched vs synchronous-per-request). It is run at high concurrency
  (≥ 600 driver VUs) because at low load both are fast and the comparison is meaningless. The
  write-behind path is actually measured doing **more** work (Redis stream + SignalR fan-out)
  than the baseline.
- **Correctness, not just `200`.** k6 checks assert response bodies and do read-after-write
  verification where it matters — the goal is a *correct* response under load, not just a live one.
- **Reproducible.** The runner specs and the exact `task` commands are documented, so a run on
  comparable hardware lands in the same ballpark.

### Test environment (for reproducibility)

| Resource | Value |
|----------|-------|
| Runner | GitHub-hosted `ubuntu-latest` (free tier) |
| vCPU / RAM | **2 vCPU · 7 GB RAM** |
| Per-container limits | **none** — all 7 services *and* the k6 process share the runner (`compose.ci.yaml`) |
| Stack under test | Postgres/PostGIS, Redis, RabbitMQ, ClickHouse, OSRM (canned-route stub), API |
| k6 load generator | runs on the **same** runner (competes for the same 2 vCPU) |
| Telemetry payload | `{ "driverId": "uuid", "lat": float, "lng": float }` (~52 bytes) |

> Because the load generator shares the runner with the system under test, these numbers are a
> **conservative floor**. Run `task k6-stress` on hardware matching your deployment target for
> capacity planning.

<!-- K6_PAYLOAD_START -->
### Test Context
    | Setting | Value |
    |---------|-------|
    | **Payload Endpoint** | `/api/telemetry/position` |
    | **Payload Size** | `52 bytes` |
    | **Payload Structure** | `{ "driverId": "uuid", "lat": float, "lng": float }` |<!-- K6_PAYLOAD_END -->

### Write-behind vs direct-postgres
<!-- K6_COMPARISON_START -->
### Comparison Test **✗ WRITE-BEHIND REGRESSED ON SOME PATHS**

> **What it does:** Benchmarks write-behind buffering (Redis queue → ClickHouse + Postgres async) against direct synchronous Postgres writes — the core architectural trade-off in GridTrack's telemetry pipeline.
>
> **Why we run it:** Validates that the write-behind buffer is actually faster, not just complexity. If write-behind regresses at p95, the optimization is wrong or broken.
>
> **How it works:** Two full runs at ≥600 driver VUs across 6 parallel scenarios, with a full stack teardown between arms. ✓/✗ marks whether write-behind matched or beat direct-postgres at p95. Error rate < 1% is also required — faster but lossy doesn't count.

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
| Telemetry POST ✓ | 184 ms | 2.04 ms | 90.1x slower | 433 ms | 60.00 s | 138.7x faster | 529 ms | 60.00 s | 113.5x faster |
| Analytics reads ✓ | 164 ms | 1.26 ms | 130.2x slower | 435 ms | 6.63 ms | 65.6x slower | 551 ms | 60.00 s | 108.9x faster |
| Delivery writes ✓ | 213 ms | 60.00 s | 281.6x faster | 558 ms | 60.00 s | 107.6x faster | 733 ms | 60.00 s | 81.9x faster |
| District-group CRUD ✓ | 188 ms | 60.00 s | 318.8x faster | 485 ms | 60.00 s | 123.6x faster | 618 ms | 60.00 s | 97.1x faster |

**Measured traffic mix (req/s):**

| Path | Write-behind | Direct-postgres |
|------|-------------:|----------------:|
| Driver telemetry | 1734.0/s | 50.6/s |
| Analytics reads | 637.8/s | 54.8/s |
| Delivery lifecycle | 43.4/s | 0.8/s |
| District-group CRUD | 18.1/s | 0.2/s |

**Throughput:** write-behind 1786.9 req/s vs direct-postgres 98.1 req/s

**Error rate:** write-behind 0.51% / direct-postgres 21.80%
<!-- K6_COMPARISON_END -->

### Throughput ceiling
<!-- K6_THROUGHPUT_START -->
### Throughput Ceiling Test

> **What it does:** Finds the absolute maximum RPS the telemetry pipeline can sustain before error rate spikes — a capacity-discovery test, not a benchmark.
>
> **Why we run it:** Know the breaking point before production does. Sets the upper bound for autoscaling thresholds and reveals whether the write-behind buffer degrades gracefully or hard-fails under extreme load.
>
> **How it works:** `ramping-arrival-rate` executor escalates 100 → 500 → 1,500 → 3,000 → 5,000 → 8,000 → 12,000 req/s over ~6 min with no sleep. Up to 15,000 VUs pre-allocated. No latency thresholds — only error rate < 5% matters here.



**Latest run:**

| Result | Value |
|--------|-------|
| Peak RPS | **754.1/s** |
| Peak concurrent VUs | **500** |
| Total HTTP requests | **271,501** |
| Error rate | **0.00%** |

**Telemetry Latency at Peak:**

| Avg | Median | p90 | p95 | Max |
|----:|-------:|----:|----:|----:|
| 0.8 ms | 0.6 ms | 1.1 ms | 1.6 ms | 0.05 s |<!-- K6_THROUGHPUT_END -->

### Stress test
<!-- K6_STRESS_START -->
### Stress Test **✓ PASSED**

> **What it does:** High-concurrency soak across all 6 scenarios simultaneously (driver telemetry, analytics reads, delivery lifecycle, district-group CRUD, SignalR, batch ingest) — realistic mixed-workload at production-scale VU counts.
>
> **Why we run it:** Catch regressions that only appear under combined load: connection-pool exhaustion, Redis saturation, slow queries that serialize under concurrency, memory leaks invisible in single-scenario runs.
>
> **How it works:** Ramp 0 → peak VUs over 30 s, hold for 2 min, ramp down. Only error rate is thresholded (< 1%). Latency numbers are hardware-relative (2-vCPU CI runner) — compare the trend across runs, not the absolute values.

**Latest run — CI stress test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **2,201** |
| Duration | **3m 31s** |
| Total HTTP requests | **376,716** |
| Request throughput | **1778.2/s** |
| Iterations | **256,983 (1213.1/s)** |
| Checks passed | **393,765 / 395,566 (100%)** |
| Error rate | **0.54%** |
| Data received | **10.8 MB/s** |
| Data sent | **446.4 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry | 239 ms | 192 ms | 469 ms | 573 ms | 2.18 s |
| Analytics reads | 217 ms | 171 ms | 465 ms | 592 ms | 2.78 s |
| Delivery lifecycle | 300 ms | 227 ms | 607 ms | 769 ms | 3.08 s |
| District-group CRUD | 259 ms | 190 ms | 550 ms | 716 ms | 2.65 s |
| SignalR negotiate | 190 ms | 147 ms | 408 ms | 516 ms | 1.19 s |
| Overall HTTP | 233 ms | 185 ms | 472 ms | 587 ms | 3.08 s |

**Error-rate compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✓ http_req_failed rate | 0.54 % | < 1.00 % |
| ✓ gridtrack_error_rate rate | 0.00 % | < 1.00 % |
<!-- K6_STRESS_END -->

## Code coverage

High coverage on the layers that hold the business rules — and we measure the HTTP surface
(Presentation) through the integration suite, not just services in isolation.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.8% |
| Application | 91.7% |
| Infrastructure | 78.8% |
| Presentation | 86.2% |
<!-- COVERAGE_END -->

## License

This project is licensed under the [MIT License](LICENSE).
