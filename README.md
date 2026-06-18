# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — write-behind GPS
ingest, PostGIS dispatch, Redis Streams → SignalR live map, ClickHouse history, and a
Python AI pipeline (urgency / demand-surge / forecast).

## Load Test Results

Single-instance Docker stack, write-behind hot path, k6. Each VU = one driver POSTing
1 GPS update/second.

**Headline run — 5,000 concurrent drivers (~3.5 min sustained):**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **5,195** |
| Checks passed | **914,538 / 914,538 (100%)** |
| Error rate | **0.00%** |
| Telemetry writes accepted | **764,572 (~3,614/s)** |
| Data transferred | 912 MB |

**Latency by path (ms):**

| Path | p50 | p90 | p95 |
|------|----:|----:|----:|
| Telemetry POST (write-behind) | 3.7 | 18.3 | 35.7 |
| Analytics reads | 2.8 | 14.1 | 31.4 |
| Delivery writes | 5.4 | 26.1 | 44.9 |
| District-group CRUD | 5.3 | 33.2 | 55.1 |

> At 150 VUs every path sits at **1–13 ms p95** with 100% checks passing. The write-behind
> buffer + per-key cache single-flight keep latency flat as load grows; the direct-Postgres
> baseline collapses (47–60 s, timeouts) under the same load. See
> `load-tests/results/comparison.md` for the side-by-side. CI re-runs a QUICK k6 pass on
> every push to `master` (`task k6-ci`).

## Code Coverage

We are proud of our high code coverage for the core layers of project.

<!-- COVERAGE_START -->
| Layer | Line Coverage |
|-------|---------------|
| Domain | 98.1% |
| Application | 88.4% |
| Infrastructure | 82.0% |
<!-- COVERAGE_END -->