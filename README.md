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
| Telemetry POST ✓ | 408 ms | 183 ms | 2.2x slower | 966 ms | 60.00 s | 62.1x faster | 1.15 s | 60.00 s | 52.2x faster |
| Analytics reads ✓ | 404 ms | 7.91 ms | 51.1x slower | 1.06 s | 327 ms | 3.3x slower | 1.28 s | 60.00 s | 46.8x faster |
| Delivery writes ✓ | 498 ms | 60.00 s | 120.4x faster | 1.46 s | 60.00 s | 41.1x faster | 1.78 s | 60.00 s | 33.8x faster |
| District-group CRUD ✓ | 524 ms | 30.39 s | 58.0x faster | 1.41 s | 60.00 s | 42.5x faster | 1.72 s | 60.00 s | 35.0x faster |

**Throughput:** write-behind 1163.7 req/s vs direct-postgres 42.6 req/s

**Error rate:** write-behind 0.51% / direct-postgres 30.46%
<!-- K6_COMPARISON_END -->

<!-- K6_STRESS_START -->
### Stress Test

*No results available yet. Run `task k6-stress` first.*
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