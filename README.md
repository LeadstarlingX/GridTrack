# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).


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
| Telemetry POST ✓ | 7.28 ms | 14.7 ms | 2.0x faster | 67.8 ms | 125 ms | 1.8x faster | 128 ms | 223 ms | 1.7x faster |
| Analytics reads ✗ | 5.84 ms | 5.42 ms | 1.1x slower | 54.3 ms | 54.0 ms | ~same | 102 ms | 93.9 ms | 1.1x slower |
| Delivery writes ✓ | 7.95 ms | 11.8 ms | 1.5x faster | 110 ms | 136 ms | 1.2x faster | 271 ms | 284 ms | ~same |
| District-group CRUD ✗ | 8.51 ms | 7.78 ms | 1.1x slower | 106 ms | 90.6 ms | 1.2x slower | 223 ms | 189 ms | 1.2x slower |

**Throughput:** write-behind 599.8 req/s vs direct-postgres 582.4 req/s

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
| Duration | **3m 31s** |
| Total HTTP requests | **219,559** |
| Request throughput | **1037.4/s** |
| Iterations | **95,015 (448.9/s)** |
| Checks passed | **219,559 / 219,559 (100%)** |
| Error rate | **0.00%** |
| Data received | **3.8 MB/s** |
| Data sent | **164.6 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 164 ms | 105 ms | 387 ms | 521 ms | 1.64 s |
| Analytics reads ✓ | 148 ms | 93.6 ms | 362 ms | 495 ms | 4.11 s |
| Delivery lifecycle ✗ | 260 ms | 182 ms | 608 ms | 781 ms | 4.44 s |
| District-group CRUD ✗ | 235 ms | 165 ms | 531 ms | 660 ms | 1.60 s |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** ✓ | 156 ms | 98.9 ms | 378 ms | 513 ms | 4.44 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 520.78 ms | < 300.00 ms |
| ✓ Driver telemetry p(99) | N/A ms | < 1000.00 ms |
| ✓ Analytics reads p(95) | 495.21 ms | < 600.00 ms |
| ✓ Analytics reads p(99) | N/A ms | < 2500.00 ms |
| ✗ Delivery lifecycle p(95) | 781.25 ms | < 700.00 ms |
| ✗ District-group CRUD p(95) | 660.06 ms | < 450.00 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 150.00 ms |
| ✓ **Overall HTTP** p(95) | 513.19 ms | < 1500.00 ms |
<!-- K6_STRESS_END -->

## Code Coverage

We are proud of our high code coverage for the core layers of project.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.1% |
| Application | 87.6% |
| Infrastructure | 75.9% |
<!-- COVERAGE_END -->


## License

This project is licensed under the [MIT License](LICENSE).