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
| Telemetry POST ✓ | 1.75 ms | 1.89 ms | 1.1x faster | 5.10 ms | 5.41 ms | 1.1x faster | 7.55 ms | 8.09 ms | 1.1x faster |
| Analytics reads ✗ | 1.42 ms | 1.32 ms | 1.1x slower | 3.90 ms | 3.37 ms | 1.2x slower | 5.85 ms | 5.17 ms | 1.1x slower |
| Delivery writes ✓ | 2.65 ms | 2.68 ms | ~same | 7.17 ms | 14.9 ms | 2.1x faster | 17.2 ms | 26.0 ms | 1.5x faster |
| District-group CRUD ✗ | 2.02 ms | 1.88 ms | 1.1x slower | 5.49 ms | 5.41 ms | ~same | 8.71 ms | 8.30 ms | ~same |

**Measured traffic mix (req/s):**

| Path | Write-behind | Direct-postgres |
|------|-------------:|----------------:|
| Driver telemetry | 241.3/s | 241.9/s |
| Analytics reads | 573.3/s | 573.8/s |
| Delivery lifecycle | 7.8/s | 7.8/s |
| District-group CRUD | 8.6/s | 8.7/s |

**Throughput:** write-behind 839.6 req/s vs direct-postgres 840.6 req/s

**Error rate:** write-behind 1.01% / direct-postgres 1.01%
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

> **What it does:** Aggressively ramps request rate until the system buckles — finds the absolute maximum RPS your API can handle before errors spike. This is NOT a performance benchmark, it's a capacity discovery test.
>
> **Why we run it:** Know your breaking point before production does. If we can serve 3,000 RPS with <1% errors, we know our scaling limits and can set proper autoscaling thresholds.
>
> **How it works:** Constant arrival rate executor pushes 100 → 500 → 1,000 → 2,000 → 3,000 requests/second with **no sleep between iterations**. No latency thresholds — only error rate <5% matters here.



**Latest run:**

| Result | Value |
|--------|-------|
| Peak RPS | **1308.8/s** |
| Peak concurrent VUs | **127** |
| Total HTTP requests | **196,317** |
| Error rate | **0.00%** |

**Telemetry Latency at Peak:**

| Avg | Median | p90 | p95 | Max |
|----:|-------:|----:|----:|----:|
| 2.9 ms | 1.6 ms | 5.9 ms | 9.4 ms | 0.10 s |<!-- K6_THROUGHPUT_END --><!-- K6_THROUGHPUT_END -->


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
| Peak concurrent VUs | **795** |
| Duration | **3m 31s** |
| Total HTTP requests | **355,484** |
| Request throughput | **1680.3/s** |
| Iterations | **112,634 (532.4/s)** |
| Checks passed | **353,683 / 355,484 (99%)** |
| Error rate | **0.51%** |
| Data received | **7.4 MB/s** |
| Data sent | **230.0 kB/s** |

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
| Driver telemetry | 11.6 ms | 4.51 ms | 24.0 ms | 41.2 ms | 637 ms |
| Analytics reads | 9.60 ms | 3.93 ms | 20.7 ms | 34.1 ms | 632 ms |
| Delivery lifecycle | 18.3 ms | 6.18 ms | 29.3 ms | 51.2 ms | 1.18 s |
| District-group CRUD | 17.8 ms | 5.72 ms | 27.0 ms | 50.0 ms | 641 ms |
| SignalR negotiate | 8.51 ms | 1.72 ms | 13.8 ms | 26.5 ms | 528 ms |
| Overall HTTP | 10.2 ms | 4.08 ms | 21.5 ms | 35.7 ms | 1.18 s |

**Error-rate compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
| ✓ gridtrack_error_rate rate | 0.00 % | < 1.00 % |
| ✓ http_req_failed rate | 0.51 % | < 1.00 % |
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