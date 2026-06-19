# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).


<!-- K6_COMPARISON_START -->
### Comparison Test **✗ SOME FAILED**

**Latest run — Comparison (pre vs post architecture):**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **480** |
| Duration | **0.0s** |
| Total HTTP requests | **68,786** |
| Request throughput | **646.3/s** |
| Iterations | **27,327 (256.8/s)** |
| Checks passed | **N/A / N/A (N/A)** |
| Error rate | **0.00%** |
| Data received | **2.2 MB/s** |
| Data sent | **97.3 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 55.4 ms | 16.9 ms | 146 ms | 239 ms | 2.72 s |
| Analytics reads ✓ | 40.6 ms | 12.5 ms | 102 ms | 167 ms | 2.09 s |
| Delivery lifecycle ✓ | 83.1 ms | 14.3 ms | 225 ms | 485 ms | 1.48 s |
| District-group CRUD ✗ | 68.2 ms | 17.2 ms | 189 ms | 323 ms | 941 ms |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** | 45.5 ms | 13.8 ms | 114 ms | 193 ms | 2.72 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 239.27 ms | < 20 ms |
| ✓ Analytics reads p(95) | 166.97 ms | < 500 ms |
| ✓ Delivery lifecycle p(95) | 484.73 ms | < 500 ms |
| ✗ District-group CRUD p(95) | 322.81 ms | < 300 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 100 ms |
| ✓ http_req_failed rate | 0.00 % | < 0.01 % |

> Compared to the previous direct-Postgres baseline (which collapsed at 47–60 s timeouts
> under load), the current architecture maintains stable latency across all paths.
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test **✗ SOME FAILED**

**Latest run — CI stress test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **480** |
| Duration | **0.0s** |
| Total HTTP requests | **68,786** |
| Request throughput | **646.3/s** |
| Iterations | **27,327 (256.8/s)** |
| Checks passed | **N/A / N/A (N/A)** |
| Error rate | **0.00%** |
| Data received | **2.2 MB/s** |
| Data sent | **97.3 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 55.4 ms | 16.9 ms | 146 ms | 239 ms | 2.72 s |
| Analytics reads ✓ | 40.6 ms | 12.5 ms | 102 ms | 167 ms | 2.09 s |
| Delivery lifecycle ✓ | 83.1 ms | 14.3 ms | 225 ms | 485 ms | 1.48 s |
| District-group CRUD ✗ | 68.2 ms | 17.2 ms | 189 ms | 323 ms | 941 ms |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** | 45.5 ms | 13.8 ms | 114 ms | 193 ms | 2.72 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 239.27 ms | < 20 ms |
| ✓ Analytics reads p(95) | 166.97 ms | < 500 ms |
| ✓ Delivery lifecycle p(95) | 484.73 ms | < 500 ms |
| ✗ District-group CRUD p(95) | 322.81 ms | < 300 ms |
| ✓ SignalR negotiate p(95) | 0.00 ms | < 100 ms |
| ✓ http_req_failed rate | 0.00 % | < 0.01 % |
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