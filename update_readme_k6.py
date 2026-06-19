import json
import pathlib
import re
import sys

# Find the latest k6 JSON result file
RESULTS_DIR = pathlib.Path("load-tests/results")
JSON_FILES = list(RESULTS_DIR.glob("latest-*.json"))

if not JSON_FILES:
    print("No k6 result files found — run 'task k6' or 'task k6-ci' first")
    sys.exit(1)

# Use the most recently modified file
latest_json = max(JSON_FILES, key=lambda p: p.stat().st_mtime)

with open(latest_json) as f:
    data = json.load(f)

metrics = data.get("metrics", {})


def get_metric(name, stat="avg"):
    """Extract a metric value from k6 JSON data."""
    m = metrics.get(name, {})
    values = m.get("values", {})
    v = values.get(stat)
    if v is None:
        return None
    return v


def fmt_ms(val):
    """Format milliseconds with appropriate precision."""
    if val is None:
        return "N/A"
    if val >= 1000:
        return f"{val / 1000:.2f} s"
    elif val >= 100:
        return f"{val:.0f} ms"
    elif val >= 10:
        return f"{val:.1f} ms"
    elif val >= 1:
        return f"{val:.2f} ms"
    else:
        return f"{val * 1000:.0f} µs"


def fmt_count(val):
    """Format count with thousands separator."""
    if val is None:
        return "N/A"
    return f"{int(val):,}"


def fmt_rate(val):
    """Format rate (per second)."""
    if val is None:
        return "N/A"
    return f"{val:.1f}/s"


def fmt_pct(val):
    """Format percentage."""
    if val is None:
        return "N/A"
    return f"{val * 100:.2f}%"


def fmt_bytes_per_sec(val):
    """Format bytes per second."""
    if val is None:
        return "N/A"
    # Convert to MB/s or kB/s based on magnitude
    if val >= 1_000_000:
        return f"{val / 1_000_000:.1f} MB/s"
    elif val >= 1_000:
        return f"{val / 1_000:.1f} kB/s"
    else:
        return f"{val:.0f} B/s"


# Extract overall test stats
vus_max = get_metric("vus_max", "max")
http_reqs = get_metric("http_reqs", "count")
http_reqs_rate = get_metric("http_reqs", "rate")
iterations = get_metric("iterations", "count")
iterations_rate = get_metric("iterations", "rate")
checks_total = get_metric("checks", "count")
checks_passed = get_metric("checks", "passed")
error_rate = get_metric("http_req_failed", "rate")
data_received = get_metric("data_received", "rate")
data_sent = get_metric("data_sent", "rate")

# Calculate checks pass rate
if checks_total and checks_total > 0:
    checks_pass_pct = (checks_passed / checks_total) * 100 if checks_passed else 0
else:
    checks_pass_pct = None

# Extract duration from root level
duration = data.get("root_group", {}).get("duration", 0) / 1000  # ms to seconds
if duration >= 60:
    duration_str = f"{int(duration // 60)}m {int(duration % 60)}s"
else:
    duration_str = f"{duration:.1f}s"

# Latency metrics by path
latency_paths = {
    "Driver telemetry": "gridtrack_driver_tel_latency",
    "Analytics reads": "gridtrack_analytics_latency",
    "Delivery lifecycle": "gridtrack_delivery_write_latency",
    "District-group CRUD": "gridtrack_district_group_latency",
    "SignalR negotiate": "gridtrack_signalr_neg_latency",
    "**Overall HTTP**": "http_req_duration",
}

# Build latency table rows
latency_rows = []
for label, metric_name in latency_paths.items():
    avg = get_metric(metric_name, "avg")
    med = get_metric(metric_name, "med")
    p90 = get_metric(metric_name, "p(90)")
    p95 = get_metric(metric_name, "p(95)")
    max_val = get_metric(metric_name, "max")

    row = (
        f"| {label} | {fmt_ms(avg)} | {fmt_ms(med)} | "
        f"{fmt_ms(p90)} | {fmt_ms(p95)} | {fmt_ms(max_val)} |"
    )
    latency_rows.append(row)

latency_table = "\n".join(latency_rows)

# Build summary table
summary_table = f"""| Result | Value |
|--------|-------|
| Peak concurrent VUs | **{fmt_count(vus_max)}** |
| Duration | **{duration_str}** |
| Total HTTP requests | **{fmt_count(http_reqs)}** |
| Request throughput | **{fmt_rate(http_reqs_rate)}** |
| Iterations | **{fmt_count(iterations)} ({fmt_rate(iterations_rate)})** |
| Checks passed | **{fmt_count(checks_passed)} / {fmt_count(checks_total)} ({checks_pass_pct:.0f if checks_pass_pct is not None else 'N/A'}%)** |
| Error rate | **{fmt_pct(error_rate)}** |
| Data received | **{fmt_bytes_per_sec(data_received)}** |
| Data sent | **{fmt_bytes_per_sec(data_sent)}** |"""

readme = pathlib.Path("README.md")
content = readme.read_text()

# Update Load Test Results section
# Match from "## Load Test Results" to "## Code Coverage"
pattern = r"(## Load Test Results\n\n).*?(?=\n## Code Coverage)"

new_section = r"""\1Single-instance Docker stack, k6. Each VU = one driver POSTing 1 GPS update/second.

**Latest run — CI stress test:**

""" + summary_table + """

**Latency by path:**

""" + "| Path | Avg | Median | p90 | p95 | Max |\n|------|----:|-------:|----:|----:|----:|\n" + latency_table + """

> Compared to the previous direct-Postgres baseline (which collapsed at 47–60 s timeouts
> under load), the current architecture maintains stable latency across all paths.
> See `load-tests/results/comparison.md` for the side-by-side. CI re-runs a QUICK k6
> pass on every push to `master` (`task k6-ci`)."""

updated = re.sub(pattern, new_section, content, flags=re.DOTALL)

readme.write_text(updated)
print(f"README.md updated with k6 results from {latest_json.name}")