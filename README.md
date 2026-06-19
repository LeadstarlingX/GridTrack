I'm too lazy to write a real README, currently this is a draft,so here are some k6 test results instead.


# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).

## Related Repositories

- [gridtrack-forecast](https://github.com/LeadstarlingX/gridtrack-forecasting) - AI/ML pipeline for urgency detection, demand surge prediction, and forecasting
- [GridTrack.Web](https://github.com/LeadstarlingX/GridTrack.Web) - Web frontend with real-time SignalR map visualization

> **Test environment:** GitHub Actions `ubuntu-latest` (2 vCPU, 7 GB RAM).  
> These results measure behavior under shared-runner constraints, not bare-metal performance.  
> For production capacity planning, run `task k6-stress` on hardware matching your deployment target.


<!-- K6_COMPARISON_START -->
### Comparison Test **✗ WRITE-BEHIND REGRESSED ON SOME PATHS**

> ClickHouse + Postgres + write-behind buffer (`write-behind`) vs Postgres-only synchronous
> writes (`direct-postgres`). ✓/✗ marks whether write-behind matched or beat the direct-postgres
> baseline at p95 — this is a relative check, not an absolute SLA (direct-postgres is meant
> to be the slower arm).

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
| Telemetry POST ✓ | 31.2 ms | 244 ms | 7.8x faster | 210 ms | 10.02 s | 47.6x faster | 315 ms | 49.10 s | 155.8x faster |
| Analytics reads ✓ | 21.8 ms | 12.6 ms | 1.7x slower | 139 ms | 746 ms | 5.4x faster | 227 ms | 4.93 s | 21.7x faster |
| Delivery writes ✓ | 29.0 ms | 15.9 ms | 1.8x slower | 285 ms | 1.46 s | 5.1x faster | 532 ms | 7.23 s | 13.6x faster |
| District-group CRUD ✓ | 31.3 ms | 181 ms | 5.8x faster | 189 ms | 5.18 s | 27.5x faster | 305 ms | 12.31 s | 40.4x faster |

**Throughput:** write-behind 672.5 req/s vs direct-postgres 102.9 req/s

**Error rate:** write-behind 0.00% / direct-postgres 1.15%
<!-- K6_COMPARISON_END -->


<!-- K6_PAYLOAD_START -->
### Test Context
    | Setting | Value |
    |---------|-------|
    | **Payload Endpoint** | `/api/telemetry/position` |
    | **Payload Size** | `52 bytes` |
    | **Payload Structure** | `{ "driverId": "uuid", "lat": float, "lng": float }` |<!-- K6_PAYLOAD_END -->



<!-- K6_THROUGHPUT_START -->
### Throughput Ceiling Test

> Measures maximum sustained RPS before degradation. Uses a constant-arrival-rate executor (up to 3,000 target RPS) with no sleep, pushing the API to its absolute limit.

### Throughput Ceiling Test

> **What it does:** Aggressively ramps request rate until the system buckles — finds the absolute maximum RPS your API can handle before errors spike. This is NOT a performance benchmark, it's a capacity discovery test.
>
> **Why we run it:** Know your breaking point before production does. If we can serve 3,000 RPS with <1% errors, we know our scaling limits and can set proper autoscaling thresholds.
>
> **How it works:** Constant arrival rate executor pushes 100 → 500 → 1,000 → 2,000 → 3,000 requests/second with **no sleep between iterations**. No latency thresholds — only error rate <5% matters here.



**Latest run:**

| Result | Value |
|--------|-------|
| Peak RPS | **1020.7/s** |
| Peak concurrent VUs | **1000** |
| Total HTTP requests | **153,112** |
| Error rate | **0.00%** |

**Telemetry Latency at Peak:**

| Avg | Median | p90 | p95 | Max |
|----:|-------:|----:|----:|----:|
| 174.1 ms | 107.4 ms | 395.9 ms | 569.5 ms | 1.82 s |
<!-- K6_THROUGHPUT_END --><!-- K6_THROUGHPUT_END -->


<!-- K6_STRESS_START -->
### Stress Test **✗ SOME FAILED**

> Ceiling test — thresholds are informational regression markers (see Taskfile/gridtrack.js comments
> for derivation), not a contractual SLA. The goal here is finding where the system actually breaks.

**Latest run — CI ceiling test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **795** |
| Duration | **3m 31s** |
| Total HTTP requests | **198,520** |
| Request throughput | **937.8/s** |
| Iterations | **87,502 (413.4/s)** |
| Checks passed | **198,520 / 198,520 (100%)** |
| Error rate | **0.00%** |
| Data received | **3.4 MB/s** |
| Data sent | **150.1 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 214 ms | 148 ms | 501 ms | 651 ms | 1.53 s |
| Analytics reads ✗ | 184 ms | 119 ms | 443 ms | 607 ms | 3.87 s |
| Delivery lifecycle ✗ | 356 ms | 230 ms | 832 ms | 1.13 s | 4.62 s |
| District-group CRUD ✗ | 314 ms | 234 ms | 669 ms | 861 ms | 1.77 s |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** ✓ | 198 ms | 130 ms | 476 ms | 640 ms | 4.62 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 651.02 ms | < 300.00 ms |
| ✓ Driver telemetry p(99) | N/A ms | < 1000.00 ms |
| ✗ Analytics reads p(95) | 607.32 ms | < 600.00 ms |
| ✓ Analytics reads p(99) | N/A ms | < 2500.00 ms |
| ✗ Delivery lifecycle p(95) | 1132.84 ms | < 700.00 ms |
| ✗ District-group CRUD p(95) | 861.08 ms | < 450.00 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 150.00 ms |
| ✓ **Overall HTTP** p(95) | 639.50 ms | < 1500.00 ms |
<!-- K6_STRESS_END -->


## Code Coverage

We are proud of our high code coverage for the core layers of project.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.1% |
| Application | 87.6% |
| Infrastructure | 75.7% |
<!-- COVERAGE_END -->


## License

This project is licensed under the [MIT License](LICENSE).