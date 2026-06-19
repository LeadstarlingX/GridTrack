/**
 * GridTrack k6 stress test — production-grade
 *
 * Architecture under test:
 *   Telemetry hot path : HTTP → ConcurrentDictionary write (~ns) → Redis XADD (fire-and-forget)
 *                        → StreamPositionConsumer → SignalR fan-out (district + district-group subs)
 *   Write-behind flush : PositionFlushService drains buffer every 5 s → Postgres batch UPDATE
 *                        + ClickHouse bulk insert
 *
 * Usage:
 *   .\load-tests\stress.ps1               # 500 driver VUs, auto-fetches real driver IDs
 *   .\load-tests\stress.ps1 2000          # push toward the ceiling
 *   .\load-tests\stress.ps1 5000          # find where it breaks
 *
 *   k6 run --env BASE=http://localhost:5098 \
 *          --env DRIVER_VUS=1000          \
 *          --env DRIVER_IDS=id1,id2,...   \
 *          --env JWT_TOKEN=<clerk_jwt>    \   # enables SignalR negotiate scenario
 *          load-tests/gridtrack.js
 */

import http from 'k6/http'
import { check, sleep, group } from 'k6'
import { Rate, Trend, Counter } from 'k6/metrics'
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js'

// ── Config ─────────────────────────────────────────────────────────────────────

const BASE         = __ENV.BASE          || 'http://localhost:5098'
const DRIVER_VUS   = parseInt(__ENV.DRIVER_VUS || '500')
const DRIVER_IDS   = (__ENV.DRIVER_IDS   || '').split(',').filter(Boolean)
const JWT_TOKEN    = __ENV.JWT_TOKEN     || ''
// WRITE_BEHIND=true  → /api/telemetry/position  (buffered, default)
// WRITE_BEHIND=false → /api/telemetry/position/sync  (direct Postgres, baseline)
const WRITE_BEHIND = (__ENV.WRITE_BEHIND ?? 'true') !== 'false'
// QUICK=true  → shorter stages for side-by-side comparison runs
const QUICK        = __ENV.QUICK === 'true'

const TEL_URL = WRITE_BEHIND
    ? `${BASE}/api/telemetry/position`
    : `${BASE}/api/telemetry/position/sync`

const MODE_LABEL = WRITE_BEHIND ? 'write-behind' : 'direct-postgres'

const DISTRICTS = ['mezzeh', 'malki', 'kafarsouseh', 'babtouma', 'kafrsousa', 'bab-sharqi', 'midan', 'qaboun']

// ── Custom metrics ──────────────────────────────────────────────────────────────

const telLatency  = new Trend('gridtrack_driver_tel_latency',      true)
const readLatency = new Trend('gridtrack_analytics_latency',       true)
const delLatency  = new Trend('gridtrack_delivery_write_latency',  true)
const dgLatency   = new Trend('gridtrack_district_group_latency',  true)
const hubLatency  = new Trend('gridtrack_signalr_neg_latency',     true)
const errorRate   = new Rate('gridtrack_error_rate')
const accepted    = new Counter('gridtrack_tel_accepted')   // 204s only — real write-behind hits

// ── Scenarios ──────────────────────────────────────────────────────────────────

export const options = {
    scenarios: {

        // 1. Driver GPS telemetry — the write-behind hot path (or direct-postgres baseline).
        //    Each VU = one driver posting 1 position/s.
        //    Ramps up to DRIVER_VUS, holds for 2 min to measure sustained throughput,
        //    then cools down. Use DRIVER_VUS=2000-5000 to find the throughput ceiling.
        driver_telemetry: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: QUICK
                ? [
                    { duration: '15s', target: Math.floor(DRIVER_VUS * 0.25) },
                    { duration: '15s', target: DRIVER_VUS },
                    { duration: '45s', target: DRIVER_VUS },
                    { duration: '15s', target: 0 },
                ]
                : [
                    { duration: '30s', target: Math.floor(DRIVER_VUS * 0.25) },
                    { duration: '30s', target: DRIVER_VUS },
                    { duration: '2m',  target: DRIVER_VUS },
                    { duration: '20s', target: 0 },
                ],
            gracefulRampDown: '15s',
            exec: 'driverTelemetry',
        },

        // 2. Concurrent dashboard observers hitting all analytics endpoints.
        analytics_read: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: QUICK
                ? [
                    { duration: '15s', target: 50  },
                    { duration: '15s', target: 150 },
                    { duration: '45s', target: 150 },
                    { duration: '15s', target: 0   },
                ]
                : [
                    { duration: '30s', target: 100  },
                    { duration: '30s', target: 300 },
                    { duration: '2m',  target: 300 },
                    { duration: '20s', target: 0   },
                ],
            gracefulRampDown: '10s',
            exec: 'analyticsRead',
        },

        // 3. Delivery lifecycle: create → auto-assign → cancel.
        //    Reduced to 5 VUs in QUICK mode — OSRM route calc is the bottleneck here,
        //    not the write-behind architecture. Full stress.ps1 uses 20.
        delivery_lifecycle: {
            executor: 'constant-vus',
            vus: QUICK ? 5 : 20,
            duration: QUICK ? '90s' : '3m',
            startTime: QUICK ? '15s' : '30s',
            exec: 'deliveryLifecycle',
        },

        // 4. District group CRUD — exercises the new endpoints end-to-end.
        district_group_crud: {
            executor: 'constant-vus',
            vus: 5,
            duration: QUICK ? '75s' : '2m30s',
            startTime: QUICK ? '15s' : '30s',
            exec: 'districtGroupCrud',
        },

        // 5. SignalR negotiate — needs a Clerk JWT to reach the hub.
        //    Skipped (VU sleeps) when JWT_TOKEN is not provided.
        signalr_negotiate: {
            executor: 'constant-arrival-rate',
            rate: 10,
            timeUnit: '1s',
            duration: QUICK ? '90s' : '3m',
            preAllocatedVUs: 20,
            maxVUs: 40,
            startTime: QUICK ? '15s' : '30s',
            exec: 'signalrNegotiate',
        },
    },

    thresholds: {
        // HTTP health
        http_req_failed:   ['rate<0.01'],       // <1% errors globally
        http_req_duration: ['p(95)<1000'],      // broad safety net

        // Per-path
        'gridtrack_driver_tel_latency':     ['p(95)<20',   'p(99)<100'],  // write-behind: should be <10ms
        'gridtrack_analytics_latency':      ['p(95)<500',  'p(99)<2000'], // Postgres aggregates under load
        'gridtrack_delivery_write_latency': ['p(95)<500'],
        'gridtrack_district_group_latency': ['p(95)<300'],
        'gridtrack_signalr_neg_latency':    ['p(95)<100'],
        'gridtrack_error_rate':             ['rate<0.01'],
    },
}

// ── Helpers ─────────────────────────────────────────────────────────────────────

const JSON_HEADERS = { 'Content-Type': 'application/json', 'Accept': 'application/json' }

function ok(res, tag) {
    const passed = check(res, { [`${tag} 2xx`]: (r) => r.status >= 200 && r.status < 300 })
    errorRate.add(!passed)
    return passed
}

function jitter(base) { return base * (0.85 + Math.random() * 0.3) }
function pick(arr)    { return arr[Math.floor(Math.random() * arr.length)] }
function today()      { return new Date().toISOString().slice(0, 10) }
function daysAgo(n)   { const d = new Date(); d.setDate(d.getDate() - n); return d.toISOString().slice(0, 10) }

// Damascus centre + small random walk
function pos() {
    return {
        lat: 33.5138 + (Math.random() - 0.5) * 0.1,
        lng: 36.2765 + (Math.random() - 0.5) * 0.1,
    }
}

// ── 1. Driver telemetry ───────────────────────────────────────────────────────

export function driverTelemetry() {
    // Cycle through real IDs if provided; otherwise fake UUIDs exercise the
    // existence-check path and still validate the endpoint + middleware stack.
    const driverId = DRIVER_IDS.length > 0
        ? DRIVER_IDS[__VU % DRIVER_IDS.length]
        : `00000000-0000-0000-0000-${String(__VU).padStart(12, '0')}`

    const { lat, lng } = pos()
    const t0 = Date.now()

    const res = http.post(
        TEL_URL,
        JSON.stringify({ driverId, lat, lng }),
        { headers: JSON_HEADERS, tags: { name: 'telemetry/position' } },
    )
    telLatency.add(res.timings.duration)

    const hit = res.status === 204   // write-behind path fully exercised
    const ok  = hit || res.status === 404
    if (hit) accepted.add(1)
    check(res, { 'telemetry 204|404': () => ok })
    errorRate.add(!ok)

    // Target 1 Hz; subtract elapsed to keep rate stable
    const wait = Math.max(0, 1000 - (Date.now() - t0)) / 1000
    if (wait > 0) sleep(wait)
}

// ── 2. Analytics read ─────────────────────────────────────────────────────────

export function analyticsRead() {
    group('analytics', () => {
        const reads = [
            [`${BASE}/api/analytics/summary`,                                               'analytics/summary'],
            [`${BASE}/api/analytics/trends?from=${daysAgo(7)}&to=${today()}&granularity=day`, 'analytics/trends'],
            [`${BASE}/api/analytics/drivers`,                                               'analytics/drivers'],
            [`${BASE}/api/deliveries?pageSize=10&from=${daysAgo(7)}&to=${today()}`,         'deliveries/list'],
            [`${BASE}/api/drivers?pageSize=10`,                                             'drivers/list'],
            [`${BASE}/api/districts`,                                                       'districts'],
            [`${BASE}/api/district-groups`,                                                 'district-groups/list'],
        ]

        for (const [url, tag] of reads) {
            const res = http.get(url, { tags: { name: tag } })
            readLatency.add(res.timings.duration)
            ok(res, tag)
            sleep(jitter(0.1))
        }

        sleep(jitter(0.4))
    })
}

// ── 3. Delivery lifecycle ─────────────────────────────────────────────────────

export function deliveryLifecycle() {
    group('delivery', () => {
        const { lat, lng } = pos()

        const createRes = http.post(
            `${BASE}/api/deliveries`,
            JSON.stringify({ lat, lng, districtId: null }),
            { headers: JSON_HEADERS, tags: { name: 'delivery/create' } },
        )
        delLatency.add(createRes.timings.duration)
        if (!ok(createRes, 'delivery/create')) { sleep(1); return }

        let deliveryId
        try   { deliveryId = JSON.parse(createRes.body).deliveryId } catch {}
        if (!deliveryId) { sleep(1); return }

        sleep(jitter(0.3))

        const assignRes = http.post(
            `${BASE}/api/deliveries/${deliveryId}/auto-assign`, null,
            { tags: { name: 'delivery/auto-assign' } },
        )
        delLatency.add(assignRes.timings.duration)
        check(assignRes, { 'assign 2xx|409': (r) => r.status < 300 || r.status === 409 })

        sleep(jitter(0.5))

        const cancelRes = http.post(
            `${BASE}/api/deliveries/${deliveryId}/cancel`,
            JSON.stringify({ reason: 'k6 cleanup' }),
            { headers: JSON_HEADERS, tags: { name: 'delivery/cancel' } },
        )
        delLatency.add(cancelRes.timings.duration)
        ok(cancelRes, 'delivery/cancel')

        sleep(jitter(0.8))
    })
}

// ── 4. District group CRUD ────────────────────────────────────────────────────

export function districtGroupCrud() {
    group('district-groups', () => {
        const createRes = http.post(
            `${BASE}/api/district-groups`,
            JSON.stringify({
                name: `k6-${__VU}-${Date.now()}`,
                districtIds: [pick(DISTRICTS), pick(DISTRICTS)].filter((v, i, a) => a.indexOf(v) === i),
            }),
            { headers: JSON_HEADERS, tags: { name: 'dg/create' } },
        )
        dgLatency.add(createRes.timings.duration)
        if (!ok(createRes, 'dg/create')) { sleep(2); return }

        let id
        try   { id = JSON.parse(createRes.body).id } catch {}
        if (!id) { sleep(2); return }

        sleep(jitter(0.2))

        const getRes = http.get(`${BASE}/api/district-groups/${id}`, { tags: { name: 'dg/get' } })
        dgLatency.add(getRes.timings.duration)
        ok(getRes, 'dg/get')

        sleep(jitter(0.2))

        const putRes = http.put(
            `${BASE}/api/district-groups/${id}`,
            JSON.stringify({ name: `k6-${__VU}-updated`, districtIds: [pick(DISTRICTS)] }),
            { headers: JSON_HEADERS, tags: { name: 'dg/update' } },
        )
        dgLatency.add(putRes.timings.duration)
        ok(putRes, 'dg/update')

        sleep(jitter(0.2))

        const delRes = http.del(`${BASE}/api/district-groups/${id}`, null, { tags: { name: 'dg/delete' } })
        dgLatency.add(delRes.timings.duration)
        ok(delRes, 'dg/delete')

        sleep(jitter(1.0))
    })
}

// ── 5. SignalR negotiate ──────────────────────────────────────────────────────

export function signalrNegotiate() {
    if (!JWT_TOKEN) { sleep(1); return }

    const res = http.post(
        `${BASE}/dashboardHub/negotiate?negotiateVersion=1`, null,
        {
            headers: { Authorization: `Bearer ${JWT_TOKEN}` },
            tags: { name: 'signalr/negotiate' },
        },
    )
    hubLatency.add(res.timings.duration)
    check(res, { 'negotiate 200': (r) => r.status === 200 })
    errorRate.add(res.status !== 200)
    sleep(0.1)
}

// ── handleSummary — writes results/latest-{mode}.{md,json} ───────────────────

export function handleSummary(data) {
    const m = (metric, stat) => {
        const v = data.metrics?.[metric]?.values?.[stat]
        return v !== undefined ? v.toFixed(2) : 'N/A'
    }
    const pct = (metric) => {
        const v = data.metrics?.[metric]?.values?.['rate']
        return v !== undefined ? (v * 100).toFixed(2) + '%' : 'N/A'
    }

    const vus   = data.metrics?.vus_max?.values?.max ?? '?'
    const reqs  = data.metrics?.http_reqs?.values?.count ?? '?'
    const rps   = data.metrics?.http_reqs?.values?.rate?.toFixed(1) ?? '?'
    const errR  = pct('http_req_failed')

    const md = `## GridTrack Load Test — \`${MODE_LABEL}\`

> **VUs:** ${vus} &nbsp;|&nbsp; **Requests:** ${reqs} &nbsp;|&nbsp; **RPS:** ${rps} &nbsp;|&nbsp; **Error rate:** ${errR}

### Latency by path (ms)

| Path | p(50) | p(90) | p(95) | p(99) |
|------|------:|------:|------:|------:|
| Telemetry (${MODE_LABEL}) | ${m('gridtrack_driver_tel_latency','p(50)')} | ${m('gridtrack_driver_tel_latency','p(90)')} | ${m('gridtrack_driver_tel_latency','p(95)')} | ${m('gridtrack_driver_tel_latency','p(99)')} |
| Analytics reads | ${m('gridtrack_analytics_latency','p(50)')} | ${m('gridtrack_analytics_latency','p(90)')} | ${m('gridtrack_analytics_latency','p(95)')} | ${m('gridtrack_analytics_latency','p(99)')} |
| Delivery lifecycle | ${m('gridtrack_delivery_write_latency','p(50)')} | ${m('gridtrack_delivery_write_latency','p(90)')} | ${m('gridtrack_delivery_write_latency','p(95)')} | ${m('gridtrack_delivery_write_latency','p(99)')} |
| District group CRUD | ${m('gridtrack_district_group_latency','p(50)')} | ${m('gridtrack_district_group_latency','p(90)')} | ${m('gridtrack_district_group_latency','p(95)')} | ${m('gridtrack_district_group_latency','p(99)')} |
| SignalR negotiate | ${m('gridtrack_signalr_neg_latency','p(50)')} | ${m('gridtrack_signalr_neg_latency','p(90)')} | ${m('gridtrack_signalr_neg_latency','p(95)')} | ${m('gridtrack_signalr_neg_latency','p(99)')} |

### Thresholds

| Metric | Limit | Result |
|--------|-------|--------|
| Telemetry p(95) | < 20 ms | ${m('gridtrack_driver_tel_latency','p(95)')} ms |
| Telemetry p(99) | < 100 ms | ${m('gridtrack_driver_tel_latency','p(99)')} ms |
| Analytics p(95) | < 500 ms | ${m('gridtrack_analytics_latency','p(95)')} ms |
| Analytics p(99) | < 2000 ms | ${m('gridtrack_analytics_latency','p(99)')} ms |
| Global error rate | < 1% | ${errR} |

*Generated by k6 ${new Date().toISOString()}*
`

    const outDir  = 'load-tests/results'
    const outBase = `${outDir}/latest-${MODE_LABEL}`

    return {
        stdout:              textSummary(data, { indent: ' ', enableColors: true }),
        [`${outBase}.md`]:   md,
        [`${outBase}.json`]: JSON.stringify(data, null, 2),
    }
}
