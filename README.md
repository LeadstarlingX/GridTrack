# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).


<!-- K6_COMPARISON_START -->
### Comparison Test **✗ WRITE-BEHIND REGRESSED ON SOME PATHS**

> ClickHouse + Postgres + write-behind buffer (`write-behind`) vs Postgres-only synchronous
> writes (`direct-postgres`). ✓/✗ marks whether write-behind matched or beat the direct-postgres
> baseline at p95 — this is a relative check, not an absolute SLA (direct-postgres is meant
> to be the slower arm).

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
| Telemetry POST ✓ | 2.35 ms | 2.93 ms | 1.2x faster | 24.5 ms | 53.0 ms | 2.2x faster | 67.7 ms | 97.8 ms | 1.4x faster |
| Analytics reads ✗ | 2.11 ms | 1.61 ms | 1.3x slower | 19.0 ms | 8.59 ms | 2.2x slower | 39.8 ms | 19.8 ms | 2.0x slower |
| Delivery writes ✗ | 4.67 ms | 3.49 ms | 1.3x slower | 56.2 ms | 35.6 ms | 1.6x slower | 170 ms | 71.8 ms | 2.4x slower |
| District-group CRUD ✗ | 4.05 ms | 2.97 ms | 1.4x slower | 62.9 ms | 38.9 ms | 1.6x slower | 135 ms | 76.7 ms | 1.8x slower |

**Throughput:** write-behind 649.1 req/s vs direct-postgres 668.2 req/s

**Error rate:** write-behind 0.00% / direct-postgres 0.00%
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test **✗ SOME FAILED**

> Ceiling test — thresholds are informational regression markers (see Taskfile/gridtrack.js comments
> for derivation), not a contractual SLA. The goal here is finding where the system actually breaks.

**Latest run — CI ceiling test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **825** |
| Duration | **0.0s** |
| Total HTTP requests | **260,295** |
| Request throughput | **1230.2/s** |
| Iterations | **101,810 (481.2/s)** |
| Checks passed | **N/A / N/A (N/A)** |
| Error rate | **0.00%** |
| Data received | **4.6 MB/s** |
| Data sent | **185.3 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 93.1 ms | 46.4 ms | 263 ms | 351 ms | 1.60 s |
| Analytics reads ✓ | 86.9 ms | 42.5 ms | 237 ms | 332 ms | 3.32 s |
| Delivery lifecycle ✓ | 178 ms | 98.5 ms | 432 ms | 579 ms | 3.62 s |
| District-group CRUD ✗ | 151 ms | 96.7 ms | 365 ms | 473 ms | 1.51 s |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** ✓ | 90.7 ms | 44.5 ms | 250 ms | 344 ms | 3.62 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 351.11 ms | < 300.00 ms |
| ✓ Driver telemetry p(99) | N/A ms | < 1000.00 ms |
| ✓ Analytics reads p(99) | N/A ms | < 2500.00 ms |
| ✓ Analytics reads p(95) | 332.28 ms | < 600.00 ms |
| ✓ Delivery lifecycle p(95) | 578.62 ms | < 700.00 ms |
| ✗ District-group CRUD p(95) | 473.28 ms | < 450.00 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 150.00 ms |
| ✓ **Overall HTTP** p(95) | 343.89 ms | < 1500.00 ms |
<!-- K6_STRESS_END -->

## Code Coverage

We are proud of our high code coverage for the core layers of project.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.1% |
| Application | 87.6% |
| Infrastructure | 75.8% |
<!-- COVERAGE_END -->


## License

This project is licensed under the [MIT License](LICENSE).