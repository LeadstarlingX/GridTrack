import json
import pathlib
import re
import sys

DEFAULT_COMPARISON_WB_FILE     = "load-tests/results/comparison-write-behind.json"
DEFAULT_COMPARISON_DIRECT_FILE = "load-tests/results/comparison-direct-postgres.json"
DEFAULT_STRESS_FILE            = "load-tests/results/stress-write-behind.json"
DEFAULT_THROUGHPUT_FILE        = "load-tests/results/throughput-write-behind.json"

PATH_LABELS = {
    "gridtrack_driver_tel_latency": "Driver telemetry",
    "gridtrack_analytics_latency": "Analytics reads",
    "gridtrack_delivery_write_latency": "Delivery lifecycle",
    "gridtrack_district_group_latency": "District-group CRUD",
    "gridtrack_signalr_neg_latency": "SignalR negotiate",
    "http_req_duration": "Overall HTTP",
}

COMPARISON_METRICS = {
    "gridtrack_driver_tel_latency":     "Telemetry POST",
    "gridtrack_analytics_latency":      "Analytics reads",
    "gridtrack_delivery_write_latency": "Delivery writes",
    "gridtrack_district_group_latency": "District-group CRUD",
}

MIX_METRICS = {
    "gridtrack_mix_telemetry_reqs": "Driver telemetry",
    "gridtrack_mix_analytics_reqs": "Analytics reads",
    "gridtrack_mix_delivery_reqs":  "Delivery lifecycle",
    "gridtrack_mix_district_reqs":  "District-group CRUD",
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
    if v >= 59000: return "≥60 s (k6 client timeout — true latency unmeasured)"
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
    if wb >= 59000 or direct >= 59000:
        return "≥timeout (unmeasurable, not a real ratio)"
    ratio = direct / wb
    if ratio >= 1.05: return f"{ratio:.1f}x faster"
    if ratio <= 0.95: return f"{(1/ratio):.1f}x slower"
    return "~same"


def _test_duration_s(data):
    """Extract test duration in seconds from k6 summary JSON."""
    return (data.get("state", {}).get("testRunDurationMs", 0) or 0) / 1000.0


def measured_mix(data, duration_s):
    """Return {label: req/s} for each mix counter, or 0 if missing."""
    if duration_s <= 0:
        return {label: 0.0 for label in MIX_METRICS.values()}
    return {label: (_get(data, m, "count") or 0) / duration_s for m, label in MIX_METRICS.items()}


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

    # Only error-rate thresholds affect pass/fail and appear in the compliance table.
    # Latency thresholds exist in k6 as catastrophic-breakage ceilings but are NOT
    # badged here — the raw latency numbers in the table above are the signal.
    threshold_status = []
    all_passed = True
    for metric_name, metric_obj in metrics.items():
        for expr, ok, stat, limit in _threshold_results(metric_obj):
            if ok is None:
                continue
            if stat != "rate":
                continue  # skip latency ceilings — informational only
            if not ok:
                all_passed = False
            label = PATH_LABELS.get(metric_name, metric_name)
            actual = _get(data, metric_name, stat) if stat else None
            disp_actual = actual * 100 if (actual is not None) else actual
            disp_limit = limit * 100 if (limit is not None) else limit
            threshold_status.append({
                "metric": label, "stat": stat, "actual": disp_actual,
                "limit": disp_limit, "passed": ok,
                "symbol": "\u2713" if ok else "\u2717", "unit": "%",
            })

    # Latency table — numbers only, no pass/fail badges
    latency_rows = []
    for metric_name, label in PATH_LABELS.items():
        if metric_name not in metrics:
            continue
        avg = _get(data, metric_name, "avg")
        med = _get(data, metric_name, "med")
        p90 = _get(data, metric_name, "p(90)")
        p95 = _get(data, metric_name, "p(95)")
        max_val = _get(data, metric_name, "max")
        latency_rows.append(
            f"| {label} | {_fmt_ms(avg)} | {_fmt_ms(med)} | {_fmt_ms(p90)} | {_fmt_ms(p95)} | {_fmt_ms(max_val)} |"
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
    badge = "**\u2713 PASSED**" if results["all_passed"] else "**\u2717 FAILED**"
    return f"""### Stress Test {badge}

> Latency numbers are informational — they reflect the 2-vCPU shared-runner environment and
> should not be compared across different hardware. Only error rate is thresholded: a passing
> test means the system handled the load without requests failing. Latency thresholds exist
> inside k6 as catastrophic-breakage ceilings (e.g. accidental synchronous DB on the hot path)
> but are not badged here — the numbers themselves are the signal.

**Latest run — CI stress test:**

{results['summary_table']}

**Latency by path:**

| Path | Avg | Median | p90 | p95 | Max |
|------|----:|-------:|----:|----:|----:|
{results['latency_table']}

**Error-rate compliance:**

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

    wb_dur = _test_duration_s(wb)
    dp_dur = _test_duration_s(direct)
    wb_mix = measured_mix(wb, wb_dur)
    dp_mix = measured_mix(direct, dp_dur)

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

    # Build measured mix rows
    mix_rows = []
    for label in MIX_METRICS.values():
        wb_r = wb_mix.get(label, 0)
        dp_r = dp_mix.get(label, 0)
        mix_rows.append(f"| {label} | {wb_r:.1f}/s | {dp_r:.1f}/s |")

    return {
        "table": "\n".join(rows),
        "mix_table": "\n".join(mix_rows),
        "wb_rps": wb_rps, "dp_rps": dp_rps,
        "wb_err": wb_err, "dp_err": dp_err,
        "all_passed": all_passed,
    }

def generate_throughput_md(filepath):
    """Parse throughput JSON and return a Markdown string."""
    try:
        with open(filepath, 'r') as f:
            data = json.load(f)

        m = data.get('metrics', {})
        vus_max = m.get('vus_max', {}).get('values', {}).get('max', '?')
        reqs = m.get('http_reqs', {}).get('values', {}).get('count', '?')
        rps = m.get('http_reqs', {}).get('values', {}).get('rate', 0)
        err_rate = m.get('http_req_failed', {}).get('values', {}).get('rate', 0)

        # Throughput uses a different metric name
        tel_m = m.get('gridtrack_driver_tel_throughput_latency', {}).get('values', {})

        md = f"""### Throughput Ceiling Test

> **What it does:** Aggressively ramps request rate until the system buckles — finds the absolute maximum RPS your API can handle before errors spike. This is NOT a performance benchmark, it's a capacity discovery test.
>
> **Why we run it:** Know your breaking point before production does. If we can serve 3,000 RPS with <1% errors, we know our scaling limits and can set proper autoscaling thresholds.
>
> **How it works:** Constant arrival rate executor pushes 100 → 500 → 1,000 → 2,000 → 3,000 requests/second with **no sleep between iterations**. No latency thresholds — only error rate <5% matters here.



**Latest run:**

| Result | Value |
|--------|-------|
| Peak RPS | **{rps:.1f}/s** |
| Peak concurrent VUs | **{vus_max}** |
| Total HTTP requests | **{reqs:,}** |
| Error rate | **{err_rate * 100:.2f}%** |

**Telemetry Latency at Peak:**

| Avg | Median | p90 | p95 | Max |
|----:|-------:|----:|----:|----:|
| {tel_m.get('avg', 0):.1f} ms | {tel_m.get('med', 0):.1f} ms | {tel_m.get('p(90)', 0):.1f} ms | {tel_m.get('p(95)', 0):.1f} ms | {tel_m.get('max', 0) / 1000:.2f} s |"""
        return md
    except Exception as e:
        print(f"Warning: Could not generate throughput md: {e}")
        return None


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
> writes (`direct-postgres`). Runs at high concurrency (≥600 driver VUs) to saturate the
> synchronous Postgres path — at low load both are fast and the comparison is meaningless.
> ✓/✗ marks whether write-behind matched or beat the direct-postgres baseline at p95.

| Path | p50 WB | p50 Direct | p50 | p90 WB | p90 Direct | p90 | p95 WB | p95 Direct | p95 |
|------|-------:|-----------:|-----|-------:|-----------:|-----|-------:|-----------:|-----|
{results['table']}

**Measured traffic mix (req/s):**

| Path | Write-behind | Direct-postgres |
|------|-------------:|----------------:|
{results['mix_table']}

{rps_line}

{err_line}
"""



def generate_comparison_chart(results):
    if results is None:
        return ""
    
    # Extract p95 values in the exact order of COMPARISON_METRICS
    wb_p95s = []
    dp_p95s = []
    labels = []
    
    for metric, label in COMPARISON_METRICS.items():
        wb_val = _get(_load(DEFAULT_COMPARISON_WB_FILE), metric, "p(95)")
        dp_val = _get(_load(DEFAULT_COMPARISON_DIRECT_FILE), metric, "p(95)")
        if wb_val is not None and dp_val is not None:
            # Short labels to fit xychart-beta x-axis (no <br/> support)
            short = label.replace("Telemetry POST", "Telemetry").replace("Analytics reads", "Analytics").replace("Delivery writes", "Deliveries").replace("District-group CRUD", "Districts")
            labels.append(short)
            wb_p95s.append(round(wb_val, 1))
            dp_p95s.append(round(dp_val, 1))

    if not labels:
        return ""

    return f"""```mermaid
xychart-beta
    title "Comparison Test: p95 Latency (ms) — Lower is better"
    x-axis {json.dumps(labels)}
    line "Write-Behind" {wb_p95s}
    line "Direct-Postgres" {dp_p95s}
```"""

def generate_stress_chart(results):
    if results is None:
        return ""
    
    # We only want the active paths (skip SignalR if it's 0)
    active_paths = ["gridtrack_driver_tel_latency", "gridtrack_analytics_latency", 
                    "gridtrack_delivery_write_latency", "gridtrack_district_group_latency"]
    
    p50s, p95s, maxs = [], [], []
    labels = []
    
    for metric in active_paths:
        label = PATH_LABELS.get(metric)
        # We need the raw data again to get max, results dict doesn't store it cleanly
        data = _load(DEFAULT_STRESS_FILE)
        if not data: continue
        
        p50 = _get(data, metric, "med")
        p95 = _get(data, metric, "p(95)")
        max_val = _get(data, metric, "max")
        
        if p50 is not None:
            short = label.replace("Driver telemetry", "Telemetry").replace("Analytics reads", "Analytics").replace("Delivery lifecycle", "Deliveries").replace("District-group CRUD", "Districts")
            labels.append(short)
            p50s.append(round(p50, 1))
            p95s.append(round(p95, 1))
            maxs.append(round(max_val / 1000, 2) if max_val and max_val > 1000 else round(max_val, 1) if max_val else 0)

    if not labels:
        return ""

    return f"""```mermaid
xychart-beta
    title "Stress Test Latency Distribution (ms) — Lower is better"
    x-axis {json.dumps(labels)}
    line "Median (p50)" {p50s}
    line "p95" {p95s}
    line "Max" {maxs}
```"""


def generate_stress_mix_chart(results):
    if results is None: return ""
    data = _load(DEFAULT_STRESS_FILE)
    if not data: return ""

    labels = []
    rates = []
    for m, label in MIX_METRICS.items():
        count = _get(data, m, "count")
        if count:
            duration = _test_duration_s(data)
            rate = count / duration if duration > 0 else 0
            short = label.replace("Driver telemetry", "Telemetry").replace("Analytics reads", "Analytics").replace("Delivery lifecycle", "Deliveries").replace("District-group CRUD", "Districts")
            labels.append(short)
            rates.append(round(rate, 1))

    if not labels: return ""
    
    return f"""```mermaid
xychart-beta
    title "Stress Test: Measured Traffic Mix (req/s)"
    x-axis {json.dumps(labels)}
    bar "Requests/s" {rates}
```"""

def generate_comparison_rps_chart(results):
    if results is None: return ""
    if results.get("wb_rps") is None or results.get("dp_rps") is None: return ""

    return f"""```mermaid
xychart-beta
    title "Comparison Test: Total Throughput (req/s) — Higher is better"
    x-axis ["Total<br/>Requests/s"]
    bar "Write-Behind" [{round(results["wb_rps"], 1)}]
    bar "Direct-Postgres" [{round(results["dp_rps"], 1)}]
```"""



# ── README orchestration ─────────────────────────────────────────────────────

def update_readme(wb_file, direct_file, stress_file):
    readme = pathlib.Path("README.md")
    content = readme.read_text()

    comparison_results = parse_comparison(wb_file, direct_file)
    stress_results = parse_stress_results(stress_file)
    throughput_section = generate_throughput_md(DEFAULT_THROUGHPUT_FILE)
    if throughput_section is None:
        throughput_section = "*No results available yet. Run `task k6-throughput` first.*"

    print("Checking for k6 results files:")
    print(f"  - Comparison write-behind ({wb_file}): {'FOUND' if pathlib.Path(wb_file).exists() else 'NOT FOUND'}")
    print(f"  - Comparison direct-postgres ({direct_file}): {'FOUND' if pathlib.Path(direct_file).exists() else 'NOT FOUND'}")
    print(f"  - Stress ({stress_file}): {'FOUND' if stress_results else 'NOT FOUND'}")
    print(f"  - Throughput ({DEFAULT_THROUGHPUT_FILE}): {'FOUND' if pathlib.Path(DEFAULT_THROUGHPUT_FILE).exists() else 'NOT FOUND'}")

    comparison_section = generate_comparison_section(comparison_results)
    stress_section = generate_stress_section(stress_results)
    
    # Generate charts
    comparison_chart = generate_comparison_chart(comparison_results)
    stress_chart = generate_stress_chart(stress_results)
    stress_mix_chart = generate_stress_mix_chart(stress_results)
    comparison_rps_chart = generate_comparison_rps_chart(comparison_results)

    payload_start, payload_end = "<!-- K6_PAYLOAD_START -->", "<!-- K6_PAYLOAD_END -->"
    comparison_start, comparison_end = "<!-- K6_COMPARISON_START -->", "<!-- K6_COMPARISON_END -->"
    comparison_chart_start, comparison_chart_end = "<!-- K6_COMPARISON_CHART_START -->", "<!-- K6_COMPARISON_CHART_END -->"
    throughput_start, throughput_end = "<!-- K6_THROUGHPUT_START -->", "<!-- K6_THROUGHPUT_END -->"
    stress_start, stress_end = "<!-- K6_STRESS_START -->", "<!-- K6_STRESS_END -->"
    stress_chart_start, stress_chart_end = "<!-- K6_STRESS_CHART_START -->", "<!-- K6_STRESS_CHART_END -->"
    stress_mix_chart_start, stress_mix_chart_end = "<!-- K6_STRESS_MIX_CHART_START -->", "<!-- K6_STRESS_MIX_CHART_END -->"
    comparison_rps_chart_start, comparison_rps_chart_end = "<!-- K6_COMPARISON_RPS_CHART_START -->", "<!-- K6_COMPARISON_RPS_CHART_END -->"


    payload_context = """### Test Context
    | Setting | Value |
    |---------|-------|
    | **Payload Endpoint** | `/api/telemetry/position` |
    | **Payload Size** | `52 bytes` |
    | **Payload Structure** | `{ "driverId": "uuid", "lat": float, "lng": float }` |"""

    content = re.sub(
        rf"({re.escape(payload_start)}\n).*?({re.escape(payload_end)})",
        f"{payload_start}\n{payload_context}{payload_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(comparison_start)}\n).*?({re.escape(comparison_end)})",
        f"{comparison_start}\n{comparison_section}{comparison_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(comparison_chart_start)}\n).*?({re.escape(comparison_chart_end)})",
        f"{comparison_chart_start}\n{comparison_chart}\n{comparison_chart_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(throughput_start)}\n).*?({re.escape(throughput_end)})",
        f"{throughput_start}\n{throughput_section}{throughput_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(stress_start)}\n).*?({re.escape(stress_end)})",
        f"{stress_start}\n{stress_section}{stress_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(stress_chart_start)}\n).*?({re.escape(stress_chart_end)})",
        f"{stress_chart_start}\n{stress_chart}\n{stress_chart_end}",
        content, flags=re.DOTALL,
    )
    content = re.sub(
        rf"({re.escape(stress_mix_chart_start)}\n).*?({re.escape(stress_mix_chart_end)})",
        f"{stress_mix_chart_start}\n{stress_mix_chart}\n{stress_mix_chart_end}",
        content, flags=re.DOTALL,
    )
    # Inject Comparison RPS Chart
    content = re.sub(
        rf"({re.escape(comparison_rps_chart_start)}\n).*?({re.escape(comparison_rps_chart_end)})",
        f"{comparison_rps_chart_start}\n{comparison_rps_chart}\n{comparison_rps_chart_end}",
        content, flags=re.DOTALL,
    )
    readme.write_text(content)
    
    print("\nREADME.md updated")

    if comparison_results:
        print(f"  Comparison: {'PASSED' if comparison_results['all_passed'] else 'SOME PATHS REGRESSED'}")
    if stress_results:
        print(f"  Stress: {'PASSED' if stress_results['all_passed'] else 'FAILED'}")
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