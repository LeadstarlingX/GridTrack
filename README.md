# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).

## Load Test Results

Single-instance Docker stack, k6. Each VU = one driver POSTing 1 GPS update/second.

**Latest run — CI stress test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **480** |
| Duration | **1m 46s** |
| Total HTTP requests | **64,694** |
| Request throughput | **606.8/s** |
| Iterations | **26,738 (250.8/s)** |
| Checks passed | **64,694 / 64,694 (100%)** |
| Error rate | **0.00%** |
| Data received | **212 MB (2.0 MB/s)** |
| Data sent | **9.9 MB (93 kB/s)** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry | 77.17 ms | 47.28 ms | 184.51 ms | 259.17 ms | 899.9 ms |
| Analytics reads | 58.71 ms | 34.10 ms | 132.56 ms | 199.65 ms | 2.37 s |
| Delivery lifecycle | 104.12 ms | 41.09 ms | 261.68 ms | 445.85 ms | 1.89 s |
| District-group CRUD | 89.85 ms | 42.06 ms | 194.30 ms | 340.45 ms | 1.01 s |
| SignalR negotiate | 0 ms | 0 ms | 0 ms | 0 ms | 0 ms |
| **Overall HTTP** | **65.03 ms** | **37.97 ms** | **150.83 ms** | **223.52 ms** | **2.37 s** |

> Compared to the previous direct-Postgres baseline (which collapsed at 47–60 s timeouts
> under load), the current architecture maintains stable latency across all paths.
> See `load-tests/results/comparison.md` for the side-by-side. CI re-runs a QUICK k6
> pass on every push to `master` (`task k6-ci`).

## Code Coverage

We are proud of our high code coverage for the core layers of project.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 97.1% |
| Application | 87.6% |
| Infrastructure | 75.7% |
<!-- COVERAGE_END -->