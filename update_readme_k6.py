import json
import pathlib
import re
import sys

DEFAULT_COMPARISON_WB_FILE     = "load-tests/results/comparison-write-behind.json"
DEFAULT_COMPARISON_DIRECT_FILE = "load-tests/results/comparison-direct-postgres.json"
DEFAULT_STRESS_FILE            = "load-tests/results/stress-write-behind.json"

PATH_LABELS = {
    "gridtrack_driver_tel_latency": "Driver telemetry",
    "gridtrack_analytics_latency": "Analytics reads",
    "gridtrack_delivery_write_latency": "Delivery lifecycle",
    "gridtrack_district_group_latency": "District-group CRUD",
    "gridtrack_signalr_neg_latency": "SignalR negotiate",
    "http_req_duration": "**Overall HTTP**",
}

COMPARISON_METRICS = {
    "gridtrack_driver_tel_latency":     "Telemetry POST",
    "gridtrack_analytics_latency":      "Analytics reads",
    "gridtrack_delivery_write_latency": "Delivery writes",
    "gridtrack_district_group_latency": "District-group CRUD",
}


# ── shared helpers ────────────────────────────────────────────────────────────

def _load(path):
    p = pathlib.Path(path)
    if not p.exists():
        return None
    with open(p) as f:
        return json.load(f)


def _get(data, metric, stat="avg"):
    if data is None:
        return None
    return data.get("metrics", {}).get(metric, {}).get("values", {}).get(stat)


def _split_threshold_expr(expr):
    # "p(95)<300" -> ("p(95)", "<", 300.0) ; "rate<0.01" -> ("rate", "<", 0.01)
    m = re.match(r"([a-zA-Z0-9_().]+)\s*(<=|>=|<|>|==)\s*([\d.]+)", expr)
    if not m:
        return None, None, None
    return m.group(1), m.group(2), float(m.group(3))


def _threshold_results(metric_obj):
    """
    (expr, ok, stat, limit) tuples from a k6 metric's embedded threshold results.
    Defensive: k6's summary JSON shape for `thresholds` has shifted across versions
    (object map vs list). Unrecognized shapes degrade to an empty list (shown as N/A)
    rather than crashing the script.
    """
    raw = (metric_obj or {}).get("thresholds")
    out = []
    if raw is None:
        return out
    try:
        items = raw.items() if isinstance(raw, dict) else (
            [(t.get("source", t.get("name", "?")), t) for t in raw] if isinstance(raw, list) else []
        )
        for expr, result in items:
            ok = result.get("ok") if isinstance(result, dict) else None
            stat, _, limit = _split_threshold_expr(expr)
            out.append((expr, ok, stat, limit))
    except Exception as e:
        print(f"  ! couldn't parse thresholds shape: {e}")
    return out


def _fmt_ms(v):
    if v is None: return "N/A"
    if v >= 1000: return f"{v / 1000:.2f} s"
    if v >= 100:  return f"{v:.0f} ms"
    if v >= 10:   return f"{v:.1f} ms"
    if v >= 1:    return f"{v:.2f} ms"
    return f"{v * 1000:.0f} \u00b5s"


def _fmt_count(v):
    return "N/A" if v is None else f"{int(v):,}"


def _fmt_rate(v):
    return "N/A" if v is None else f"{v:.1f}/s"


def _fmt_pct(v):
    return "N/A" if v is None else f"{v * 100:.2f}%"


def _fmt_bytes_per_sec(v):
    if v is None: return "N/A"
    if v >= 1_000_000: return f"{v / 1_000_000:.1f} MB/s"
    if v >= 1_000: return f"{v / 1_000:.1f} kB/s"
    return f"{v:.0f} B/s"


def _speedup(wb, direct):
    if wb is None or direct is None or wb == 0:
        return "N/A"
    ratio = direct / wb
    if ratio >= 1.05:
        return f"{ratio:.1f}x faster"
    if ratio <= 0.95:
        return f"{(1 / ratio):.1f}x slower"
    return "~same"


# ── stress (ceiling) test ───────────────────────────────────────────────────────

def parse_stress_results(json_path):
    data = _load(json_path)
    if data is None:
        return None

    metrics = data.get("metrics", {})

    vus_max         = _get(data, "vus_max", "max")
    http_reqs        = _get(data, "http_reqs", "count")
    http_reqs_rate   = _get(data, "http_reqs", "rate")
    iterations       = _get(data, "iterations", "count")
    iterations_rate  = _get(data, "iterations", "rate")
    checks_vals      = (data.get("metrics", {}).get("checks") or {}).get("values", {})
    checks_passed    = checks_vals.get("passes")
    checks_total     = (checks_passed or 0) + (checks_vals.get("fails") or 0)
    error_rate       = _get(data, "http_req_failed", "rate")
    data_received    = _get(data, "data_received", "rate")
    data_sent        = _get(data, "data_sent", "rate")

    checks_pass_pct = (checks_passed / checks_total * 100) if checks_total else None
    duration_ms = data.get("state", {}).get("testRunDurationMs", 0)
    duration = duration_ms / 1000.0
    duration_str = f"{int(duration // 60)}m {int(duration % 60)}s" if duration >= 60 else f"{duration:.1f}s"

    # Pass/fail comes straight from k6's own evaluation — no second hardcoded limits
    # table to drift out of sync with gridtrack.js.
    threshold_status = []
    all_passed = True
    for metric_name, label in PATH_LABELS.items():
        m = metrics.get(metric_name)
        if not m:
            continue
        for expr, ok, stat, limit in _threshold_results(m):
            if ok is None:
                continue
            if not ok:
                all_passed = False
            actual = _get(data, metric_name, stat) if stat else None
            is_rate = stat == "rate"
            disp_actual = actual * 100 if (is_rate and actual is not None) else actual
            disp_limit = limit * 100 if (is_rate and limit is not None) else limit
            unit = "%" if is_rate else ("ms" if stat and stat.startswith("p(") else "")
            threshold_status.append({
                "metric": label, "stat": stat, "actual": disp_actual,
                "limit": disp_limit, "passed": ok,
                "symbol": "\u2713" if ok else "\u2717", "unit": unit,
            })

    latency_rows = []
    for metric_name, label in PATH_LABELS.items():
        if metric_name not in metrics:
            continue
        avg = _get(data, metric_name, "avg")
        med = _get(data, metric_name, "med")
        p90 = _get(data, metric_name, "p(90)")
        p95 = _get(data, metric_name, "p(95)")
        max_val = _get(data, metric_name, "max")
        p95_oks = [ok for expr, ok, stat, _ in _threshold_results(metrics.get(metric_name, {}))
                   if stat == "p(95)" and ok is not None]
        status_symbol = ""
        if p95_oks:
            status_symbol = " \u2713" if all(p95_oks) else " \u2717"
        latency_rows.append(
            f"| {label}{status_symbol} | {_fmt_ms(avg)} | {_fmt_ms(med)} | {_fmt_ms(p90)} | {_fmt_ms(p95)} | {_fmt_ms(max_val)} |"
        )

    checks_pass_str = f"{checks_pass_pct:.0f}%" if checks_pass_pct is not None else "N/A"
    summary_table = f"""| Result | Value |
|--------|-------|
| Peak concurrent VUs | **{_fmt_count(vus_max)}** |
| Duration | **{duration_str}** |
| Total HTTP requests | **{_fmt_count(http_reqs)}** |
| Request throughput | **{_fmt_rate(http_reqs_rate)}** |
| Iterations | **{_fmt_count(iterations)} ({_fmt_rate(iterations_rate)})** |
| Checks passed | **{_fmt_count(checks_passed)} / {_fmt_count(checks_total)} ({checks_pass_str})** |
| Error rate | **{_fmt_pct(error_rate)}** |
| Data received | **{_fmt_bytes_per_sec(data_received)}** |
| Data sent | **{_fmt_bytes_per_sec(data_sent)}** |"""

    threshold_rows = []
    for ts in threshold_status:
        a = f"{ts['actual']:.2f}" if ts['actual'] is not None else "N/A"
        l = f"{ts['limit']:.2f}" if ts['limit'] is not None else "N/A"
        threshold_rows.append(f"| {ts['symbol']} {ts['metric']} {ts['stat']} | {a} {ts['unit']} | < {l} {ts['unit']} |")

    return {
        "summary_table": summary_table,
        "latency_table": "\n".join(latency_rows),
        "threshold_table": "\n".join(threshold_rows),
        "all_passed": all_passed,
        "threshold_status": threshold_status,
    }


def generate_stress_section(results):
    if results is None:
        return """### Stress Test

*No results available yet. Run `task k6-stress` first.*
"""
    badge = "**\u2713 ALL PASSED**" if results["all_passed"] else "**\u2717 SOME FAILED**"
    return f"""### Stress Test {badge}

> Ceiling test — thresholds are informational regression markers (see Taskfile/gridtrack.js comments
> for derivation), not a contractual SLA. The goal here is finding where the system actually breaks.

**Latest run — CI ceiling test:**

{results['summary_table']}

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
{results['latency_table']}

**Threshold compliance:**

| Status | Metric | Actual | Threshold |
|--------|--------|--------|-----------|
{results['threshold_table']}
"""


# ── comparison test ──────────────────────────────────────────────────────────

def parse_comparison(wb_path, direct_path):
    wb = _load(wb_path)
    direct = _load(direct_path)
    if wb is None or direct is None:
        return None

    rows = []
    all_passed = True
    for metric, label in COMPARISON_METRICS.items():
        wb_p50, dp_p50 = _get(wb, metric, "med"), _get(direct, metric, "med")
        wb_p90, dp_p90 = _get(wb, metric, "p(90)"), _get(direct, metric, "p(90)")
        wb_p95, dp_p95 = _get(wb, metric, "p(95)"), _get(direct, metric, "p(95)")

        # Pass = write-behind matched or beat the direct-postgres baseline at p95.
        # That's the entire premise of the buffer + ClickHouse split — if it doesn't
        # hold, the optimization regressed on this path.
        passed = wb_p95 is not None and dp_p95 is not None and wb_p95 <= dp_p95
        if not passed:
            all_passed = False
        symbol = "\u2713" if passed else "\u2717"

        rows.append(
            f"| {label} {symbol} | {_fmt_ms(wb_p50)} | {_fmt_ms(dp_p50)} | {_speedup(wb_p50, dp_p50)} "
            f"| {_fmt_ms(wb_p90)} | {_fmt_ms(dp_p90)} | {_speedup(wb_p90, dp_p90)} "
            f"| {_fmt_ms(wb_p95)} | {_fmt_ms(dp_p95)} | {_speedup(wb_p95, dp_p95)} |"
        )

    wb_rps, dp_rps = _get(wb, "http_reqs", "rate"), _get(direct, "http_reqs", "rate")
    wb_err, dp_err = _get(wb, "http_req_failed", "rate"), _get(direct, "http_req_failed", "rate")

    if (wb_err is not None and wb_err >= 0.01) or (dp_err is not None and dp_err >= 0.01):
        all_passed = False

    return {
        "table": "\n".join(rows),
        "wb_rps": wb_rps, "dp_rps": dp_rps,
        "wb_err": wb_err, "dp_err": dp_err,
        "all_passed": all_passed,
    }


def generate_comparison_section(results):
    if results is None:
        return """### Comparison Test

*No results available yet. Run `task k6-compare` first.*
"""
    badge = "**\u2713 WRITE-BEHIND WINS ACROSS THE BOARD**" if results["all_passed"] \
        else "**\u2717 WRITE-BEHIND REGRESSED ON SOME PATHS**"

    rps_line = (f"**Throughput:** write-behind {results['wb_rps']:.1f} req/s vs "
                f"direct-postgres {results['dp_rps']:.1f} req/s"
                if results["wb_rps"] is not None and results["dp_rps"] is not None else "")
    err_line = (f"**Error rate:** write-behind {results['wb_err']*100:.2f}% / "
                f"direct-postgres {results['dp_err']*100:.2f}%"
                if results["wb_err"] is not None and results["dp_err"] is not None else "")

    return f"""### Comparison Test {badge}

> ClickHouse + Postgres + write-behind buffer (`write-behind`) vs Postgres-only synchronous
> writes (`direct-postgres`). \u2713/\u2717 marks whether write-behind matched or beat the direct-postgres
> baseline at p95 \u2014 this is a relative check, not an absolute SLA (direct-postgres is meant
> to be the slower arm).

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
{results['table']}

{rps_line}

{err_line}
"""


# ── README orchestration ─────────────────────────────────────────────────────

def update_readme(wb_file, direct_file, stress_file):
    readme = pathlib.Path("README.md")
    content = readme.read_text()

    comparison_results = parse_comparison(wb_file, direct_file)
    stress_results = parse_stress_results(stress_file)

    print("Checking for k6 results files:")
    print(f"  - Comparison write-behind ({wb_file}): {'FOUND' if pathlib.Path(wb_file).exists() else 'NOT FOUND'}")
    print(f"  - Comparison direct-postgres ({direct_file}): {'FOUND' if pathlib.Path(direct_file).exists() else 'NOT FOUND'}")
    print(f"  - Stress ({stress_file}): {'FOUND' if stress_results else 'NOT FOUND'}")

    comparison_section = generate_comparison_section(comparison_results)
    stress_section = generate_stress_section(stress_results)

    comparison_start, comparison_end = "<!-- K6_COMPARISON_START -->", "<!-- K6_COMPARISON_END -->"
    stress_start, stress_end = "<!-- K6_STRESS_START -->", "<!-- K6_STRESS_END -->"

    if comparison_start not in content:
        match = re.search(r"(## Load Test Results\n)", content)
        if match:
            pos = match.end()
            content = (content[:pos]
                       + f"\n{comparison_start}\n{comparison_end}\n\n{stress_start}\n{stress_end}\n"
                       + content[pos:])

    content = re.sub(
        rf"({re.escape(comparison_start)}\n).*?({re.escape(comparison_end)})",
        f"{comparison_start}\n{comparison_section}{comparison_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(stress_start)}\n).*?({re.escape(stress_end)})",
        f"{stress_start}\n{stress_section}{stress_end}",
        content, flags=re.DOTALL,
    )

    readme.write_text(content)
    print("\nREADME.md updated")

    if comparison_results:
        print(f"  Comparison: {'PASSED' if comparison_results['all_passed'] else 'SOME PATHS REGRESSED'}")
    if stress_results:
        print(f"  Stress: {'PASSED' if stress_results['all_passed'] else 'SOME THRESHOLDS FAILED'}")
        failed = [t for t in stress_results["threshold_status"] if not t["passed"]]
        for t in failed:
            actual_str = f"{t['actual']:.2f}" if t['actual'] is not None else "N/A"
            limit_str = f"{t['limit']:.2f}" if t['limit'] is not None else "N/A"
            print(f"    \u2717 {t['metric']} {t['stat']}: {actual_str}{t['unit']} (limit < {limit_str}{t['unit']})")
                        

if __name__ == "__main__":
    wb_file = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_COMPARISON_WB_FILE
    direct_file = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_COMPARISON_DIRECT_FILE
    stress_file = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_STRESS_FILE
    update_readme(wb_file, direct_file, stress_file)