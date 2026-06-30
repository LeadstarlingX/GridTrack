# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet. A partner company's
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

> ClickHouse + Postgres + write-behind buffer (`write-behind`) vs Postgres-only synchronous
> writes (`direct-postgres`). Runs at high concurrency (≥600 driver VUs) to saturate the
> synchronous Postgres path — at low load both are fast and the comparison is meaningless.
> ✓/✗ marks whether write-behind matched or beat the direct-postgres baseline at p95.

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
| Telemetry POST ✓ | 1.59 ms | 2.18 ms | 1.4x faster | 2.66 ms | 3.76 ms | 1.4x faster | 4.82 ms | 5.22 ms | 1.1x faster |
| Analytics reads ✗ | 1.44 ms | 1.17 ms | 1.2x slower | 2.19 ms | 2.18 ms | ~same | 4.74 ms | 4.37 ms | 1.1x slower |
| Delivery writes ✓ | 3.19 ms | 2.76 ms | 1.2x slower | 5.65 ms | 10.0 ms | 1.8x faster | 14.0 ms | 15.2 ms | 1.1x faster |
| District-group CRUD ✗ | 2.71 ms | 2.33 ms | 1.2x slower | 5.56 ms | 3.75 ms | 1.5x slower | 13.9 ms | 6.53 ms | 2.1x slower |

**Measured traffic mix (req/s):**

| Path | Write-behind | Direct-postgres |
|------|-------------:|----------------:|
| Driver telemetry | 525.8/s | 527.7/s |
| Analytics reads | 592.1/s | 593.3/s |
| Delivery lifecycle | 7.8/s | 7.8/s |
| District-group CRUD | 8.6/s | 8.6/s |

**Throughput:** write-behind 996.4 req/s vs direct-postgres 996.8 req/s

**Error rate:** write-behind 0.79% / direct-postgres 1.66%
<!-- K6_COMPARISON_END -->

### Throughput ceiling
<!-- K6_THROUGHPUT_START -->
### Throughput Ceiling Test

> **What it does:** Aggressively ramps request rate until the system buckles — finds the absolute maximum RPS your API can handle before errors spike. This is NOT a performance benchmark, it's a capacity discovery test.
>
> **Why we run it:** Know your breaking point before production does. If we can serve 3,000 RPS with <1% errors, we know our scaling limits and can set proper autoscaling thresholds.
>
> **How it works:** Constant arrival rate executor pushes 100 → 500 → 1,000 → 2,000 → 3,000 requests/second with **no sleep between iterations**. No latency thresholds — only error rate <5% matters here.



**Latest run:**

| Result | Value |
|--------|-------|
| Peak RPS | **1309.9/s** |
| Peak concurrent VUs | **100** |
| Total HTTP requests | **196,501** |
| Error rate | **2.00%** |

**Telemetry Latency at Peak:**

| Avg | Median | p90 | p95 | Max |
|----:|-------:|----:|----:|----:|
| 1.1 ms | 1.1 ms | 1.6 ms | 1.6 ms | 0.05 s |<!-- K6_THROUGHPUT_END -->

### Stress test
<!-- K6_STRESS_START -->
### Stress Test **✓ PASSED**

> Latency numbers are informational — they reflect the 2-vCPU shared-runner environment and
> should not be compared across different hardware. Only error rate is thresholded: a passing
> test means the system handled the load without requests failing. Latency thresholds exist
> inside k6 as catastrophic-breakage ceilings (e.g. accidental synchronous DB on the hot path)
> but are not badged here — the numbers themselves are the signal.

**Latest run — CI stress test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **990** |
| Duration | **3m 32s** |
| Total HTTP requests | **408,641** |
| Request throughput | **1926.7/s** |
| Iterations | **152,271 (718.0/s)** |
| Checks passed | **419,104 / 422,687 (99%)** |
| Error rate | **0.77%** |
| Data received | **11.6 MB/s** |
| Data sent | **336.1 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry | 3.24 ms | 1.64 ms | 5.83 ms | 7.73 ms | 706 ms |
| Analytics reads | 2.30 ms | 1.59 ms | 2.74 ms | 5.87 ms | 700 ms |
| Delivery lifecycle | 9.63 ms | 3.20 ms | 10.8 ms | 22.7 ms | 673 ms |
| District-group CRUD | 8.89 ms | 2.71 ms | 8.69 ms | 19.0 ms | 637 ms |
| SignalR negotiate | 2.35 ms | 572 µs | 1.57 ms | 3.37 ms | 539 ms |
| Overall HTTP | 2.69 ms | 1.60 ms | 3.86 ms | 6.87 ms | 706 ms |

**Error-rate compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✓ http_req_failed rate | 0.77 % | < 1.00 % |
| ✓ gridtrack_error_rate rate | 0.71 % | < 1.00 % |
<!-- K6_STRESS_END -->

## Code coverage

High coverage on the layers that hold the business rules — and we measure the HTTP surface
(Presentation) through the integration suite, not just services in isolation.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.1% |
| Application | 87.6% |
| Infrastructure | 75.7% |
| Presentation | — |
<!-- COVERAGE_END -->

## License

This project is licensed under the [MIT License](LICENSE).
