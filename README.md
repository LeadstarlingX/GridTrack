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
| Telemetry POST ✓ | 21.4 ms | 36.8 ms | 1.7x faster | 137 ms | 233 ms | 1.7x faster | 213 ms | 370 ms | 1.7x faster |
| Analytics reads ✗ | 11.8 ms | 10.2 ms | 1.2x slower | 98.2 ms | 99.5 ms | ~same | 167 ms | 163 ms | ~same |
| Delivery writes ✓ | 14.5 ms | 17.4 ms | 1.2x faster | 221 ms | 269 ms | 1.2x faster | 406 ms | 481 ms | 1.2x faster |
| District-group CRUD ✓ | 15.8 ms | 20.9 ms | 1.3x faster | 156 ms | 186 ms | 1.2x faster | 257 ms | 307 ms | 1.2x faster |

**Throughput:** write-behind 650.0 req/s vs direct-postgres 644.6 req/s

**Error rate:** write-behind 0.00% / direct-postgres 0.00%
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test **✗ SOME FAILED**

> Ceiling test — thresholds are informational regression markers (see Taskfile/gridtrack.js comments
> for derivation), not a contractual SLA. The goal here is finding where the system actually breaks.

**Latest run — CI ceiling test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **795** |
| Duration | **3m 31s** |
| Total HTTP requests | **206,075** |
| Request throughput | **973.8/s** |
| Iterations | **89,205 (421.5/s)** |
| Checks passed | **206,075 / 206,075 (100%)** |
| Error rate | **0.00%** |
| Data received | **3.6 MB/s** |
| Data sent | **154.3 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 172 ms | 111 ms | 408 ms | 534 ms | 1.85 s |
| Analytics reads ✓ | 169 ms | 104 ms | 416 ms | 558 ms | 5.48 s |
| Delivery lifecycle ✗ | 374 ms | 236 ms | 822 ms | 1.16 s | 8.16 s |
| District-group CRUD ✗ | 316 ms | 226 ms | 674 ms | 868 ms | 1.93 s |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** ✓ | 175 ms | 108 ms | 423 ms | 563 ms | 8.16 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 534.08 ms | < 300.00 ms |
| ✓ Driver telemetry p(99) | N/A ms | < 1000.00 ms |
| ✓ Analytics reads p(99) | N/A ms | < 2500.00 ms |
| ✓ Analytics reads p(95) | 558.39 ms | < 600.00 ms |
| ✗ Delivery lifecycle p(95) | 1158.76 ms | < 700.00 ms |
| ✗ District-group CRUD p(95) | 867.82 ms | < 450.00 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 150.00 ms |
| ✓ **Overall HTTP** p(95) | 563.12 ms | < 1500.00 ms |
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