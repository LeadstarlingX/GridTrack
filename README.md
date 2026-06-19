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
| Telemetry POST ✗ | 6.53 ms | 5.04 ms | 1.3x slower | 93.1 ms | 76.6 ms | 1.2x slower | 230 ms | 130 ms | 1.8x slower |
| Analytics reads ✗ | 4.22 ms | 3.13 ms | 1.4x slower | 56.7 ms | 38.9 ms | 1.5x slower | 101 ms | 69.1 ms | 1.5x slower |
| Delivery writes ✗ | 6.45 ms | 5.52 ms | 1.2x slower | 102 ms | 94.8 ms | 1.1x slower | 319 ms | 204 ms | 1.6x slower |
| District-group CRUD ✗ | 5.95 ms | 4.98 ms | 1.2x slower | 90.5 ms | 74.3 ms | 1.2x slower | 248 ms | 151 ms | 1.6x slower |

**Throughput:** write-behind 605.5 req/s vs direct-postgres 635.7 req/s

**Error rate:** write-behind 1.40% / direct-postgres 1.33%
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test **✓ ALL PASSED**

> Ceiling test — thresholds are informational regression markers (see Taskfile/gridtrack.js comments
> for derivation), not a contractual SLA. The goal here is finding where the system actually breaks.

**Latest run — CI ceiling test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **1,000** |
| Duration | **2m 30s** |
| Total HTTP requests | **172,738** |
| Request throughput | **1151.6/s** |
| Iterations | **172,738 (1151.6/s)** |
| Checks passed | **172,738 / 172,738 (100%)** |
| Error rate | **0.00%** |
| Data received | **124.4 kB/s** |
| Data sent | **311.9 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| **Overall HTTP** | 111 ms | 53.5 ms | 291 ms | 394 ms | 1.13 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|

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