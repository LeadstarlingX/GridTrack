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
| Telemetry POST ✓ | 169 ms | 131 ms | 1.3x slower | 424 ms | 60.00 s | 141.5x faster | 534 ms | 60.00 s | 112.4x faster |
| Analytics reads ✓ | 154 ms | 6.03 ms | 25.5x slower | 436 ms | 222 ms | 2.0x slower | 585 ms | 29.00 s | 49.6x faster |
| Delivery writes ✓ | 198 ms | 60.00 s | 303.0x faster | 569 ms | 60.00 s | 105.4x faster | 747 ms | 60.00 s | 80.3x faster |
| District-group CRUD ✓ | 178 ms | 60.00 s | 337.5x faster | 511 ms | 60.00 s | 117.4x faster | 658 ms | 60.00 s | 91.2x faster |

**Throughput:** write-behind 1251.4 req/s vs direct-postgres 41.8 req/s

**Error rate:** write-behind 0.48% / direct-postgres 30.95%
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
| Infrastructure | 75.8% |
<!-- COVERAGE_END -->


## License

This project is licensed under the [MIT License](LICENSE).