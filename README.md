# GridTrack

Real-time fleet tracking & dispatch for a Damascus delivery fleet — PostGIS dispatch,
Redis Streams → SignalR live map, ClickHouse history, and a Python AI pipeline
(urgency / demand-surge / forecast).


<!-- K6_COMPARISON_START -->
### Comparison Test

*No results available yet. Run the comparison test test first.*
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test **✗ SOME FAILED**

**Latest run — CI stress test:**

| Result | Value |
|--------|-------|
| Peak concurrent VUs | **480** |
| Duration | **0.0s** |
| Total HTTP requests | **71,767** |
| Request throughput | **673.4/s** |
| Iterations | **27,791 (260.8/s)** |
| Checks passed | **N/A / N/A (N/A)** |
| Error rate | **0.00%** |
| Data received | **2.3 MB/s** |
| Data sent | **100.1 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry ✗ | 42.4 ms | 13.2 ms | 105 ms | 190 ms | 1.94 s |
| Analytics reads ✓ | 31.1 ms | 7.65 ms | 71.3 ms | 136 ms | 2.25 s |
| Delivery lifecycle ✓ | 70.5 ms | 9.56 ms | 164 ms | 440 ms | 2.03 s |
| District-group CRUD ✗ | 51.1 ms | 10.1 ms | 127 ms | 334 ms | 841 ms |
| SignalR negotiate ✓ | 0 µs | 0 µs | 0 µs | 0 µs | 0 µs |
| **Overall HTTP** | 34.7 ms | 8.99 ms | 80.9 ms | 153 ms | 2.25 s |

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✗ Driver telemetry p(95) | 189.70 ms | < 20 ms |
| ✓ Analytics reads p(95) | 136.38 ms | < 500 ms |
| ✓ Delivery lifecycle p(95) | 439.64 ms | < 500 ms |
| ✗ District-group CRUD p(95) | 334.07 ms | < 300 ms |
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
| Infrastructure | 75.8% |
<!-- COVERAGE_END -->


## License

This project is licensed under the [MIT License](LICENSE).