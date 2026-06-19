import json
import pathlib
import re
import sys

# Default file paths for k6 JSON results
DEFAULT_COMPARISON_FILE = "load-tests/results/comparison.json"
DEFAULT_STRESS_FILE = "load-tests/results/stress.json"


def parse_k6_results(json_path):
    """Parse a k6 JSON result file and extract metrics."""
    if not pathlib.Path(json_path).exists():
        return None

    with open(json_path) as f:
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

    return {
            "summary_table": summary_table,
            "latency_table": latency_table,
        }


def generate_section(title, run_label, results, note=""):
    """Generate a markdown section for k6 results."""
    if results is None:
        return f"""### {title}

*No results available yet. Run the {title.lower()} test first.*
"""

    latency_header = "| Path | Avg | Median | p90 | p95 | Max |\n|------|----:|-------:|----:|----:|----:|"
    latency_full = f"{latency_header}\n{results['latency_table']}"

    section = f"""### {title}

**{run_label}:**

{results["summary_table"]}

**Latency by path:**

{latency_full}
"""
    if note:
        section += f"\n{note}\n"
    return section


def update_readme(comparison_file, stress_file):
    """Update README.md with k6 results from comparison and stress tests."""
    readme = pathlib.Path("README.md")
    content = readme.read_text()

    # Parse results from both test files
    comparison_results = parse_k6_results(comparison_file)
    stress_results = parse_k6_results(stress_file)

    # Generate sections
    comparison_note = ""
    if comparison_results:
        comparison_note = """> Compared to the previous direct-Postgres baseline (which collapsed at 47–60 s timeouts
> under load), the current architecture maintains stable latency across all paths."""

    comparison_section = generate_section(
        "Comparison Test",
        "Latest run — Comparison (pre vs post architecture)",
        comparison_results,
        comparison_note
    )

    stress_section = generate_section(
        "Stress Test",
        "Latest run — CI stress test",
        stress_results
    )

    # Define markers for comparison test
    comparison_start = "<!-- K6_COMPARISON_START -->"
    comparison_end = "<!-- K6_COMPARISON_END -->"

    # Define markers for stress test
    stress_start = "<!-- K6_STRESS_START -->"
    stress_end = "<!-- K6_STRESS_END -->"

    # Check if markers exist for comparison, if not add them
    if comparison_start not in content:
        # Find the Load Test Results section and insert markers
        pattern = r"(## Load Test Results\n)"
        match = re.search(pattern, content)
        if match:
            insert_pos = match.end()
            content = content[:insert_pos] + f"\n{comparison_start}\n{comparison_end}\n\n{stress_start}\n{stress_end}\n" + content[insert_pos:]

    # Update comparison section between markers
    comparison_pattern = rf"({re.escape(comparison_start)}\n).*?({re.escape(comparison_end)})"
    comparison_replacement = f"{comparison_start}\n{comparison_section}{comparison_end}"
    content = re.sub(comparison_pattern, comparison_replacement, content, flags=re.DOTALL)

    # Update stress section between markers
    stress_pattern = rf"({re.escape(stress_start)}\n).*?({re.escape(stress_end)})"
    stress_replacement = f"{stress_start}\n{stress_section}{stress_end}"
    content = re.sub(stress_pattern, stress_replacement, content, flags=re.DOTALL)

    readme.write_text(content)
    print(f"README.md updated with k6 results")
    if comparison_file:
        print(f"  - Comparison: {comparison_file}")
    if stress_file:
        print(f"  - Stress: {stress_file}")


if __name__ == "__main__":
    # Accept file paths as command-line arguments
    comparison_file = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_COMPARISON_FILE
    stress_file = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_STRESS_FILE

    update_readme(comparison_file, stress_file)