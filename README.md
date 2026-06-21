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
<!-- K6_COMPARISON_END -->

<!-- K6_COMPARISON_CHART_START -->
<!-- K6_COMPARISON_CHART_END -->

<!-- K6_PAYLOAD_START -->
<!-- K6_PAYLOAD_END -->

<!-- K6_THROUGHPUT_START -->
<!-- K6_THROUGHPUT_END -->

<!-- K6_STRESS_START -->
<!-- K6_STRESS_END -->

<!-- K6_STRESS_CHART_START -->
<!-- K6_STRESS_CHART_END -->

<!-- K6_STRESS_MIX_CHART_START -->
<!-- K6_STRESS_MIX_CHART_END -->

<!-- K6_COMPARISON_RPS_CHART_START -->
<!-- K6_COMPARISON_RPS_CHART_END -->

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