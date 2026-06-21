import http from 'k6/http'
import { check, sleep, group } from 'k6'
import { Rate, Trend, Counter } from 'k6/metrics'
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js'

// ── Config ─────────────────────────────────────────────────────────────────────

const BASE         = __ENV.BASE          || 'http://localhost:5098'
const DRIVER_VUS   = parseInt(__ENV.DRIVER_VUS || '600')
const THROUGHPUT_MAX_VUS = parseInt(__ENV.THROUGHPUT_MAX_VUS || '2000')
const DRIVER_IDS   = (__ENV.DRIVER_IDS   || '').split(',').filter(Boolean)
const JWT_TOKEN    = __ENV.JWT_TOKEN     || ''
// WRITE_BEHIND=true  → /api/telemetry/position  (buffered, default)
// WRITE_BEHIND=false → /api/telemetry/position/sync  (direct Postgres, baseline)
const WRITE_BEHIND = (__ENV.WRITE_BEHIND ?? 'true') !== 'false'
// QUICK=true  → shorter stages for side-by-side comparison runs
const QUICK        = __ENV.QUICK === 'true'
const THRESHOLD_PROFILE = __ENV.THRESHOLD_PROFILE || 'stress'
const TEST_MODE = __ENV.TEST_MODE || 'latency'  // 'latency' | 'throughput' | 'both'


const TEL_URL = WRITE_BEHIND
    ? `${BASE}/api/telemetry/position`
    : `${BASE}/api/telemetry/position/sync`

const MODE_LABEL = WRITE_BEHIND ? 'write-behind' : 'direct-postgres'

const DISTRICTS = ['mezzeh', 'malki', 'kafarsouseh', 'babtouma', 'kafrsousa', 'bab-sharqi', 'midan', 'qaboun']

// ── Custom metrics ──────────────────────────────────────────────────────────────

const telLatency  = new Trend('gridtrack_driver_tel_latency',      true)
const telThroughputLatency = new Trend('gridtrack_driver_tel_throughput_latency', true)
const readLatency = new Trend('gridtrack_analytics_latency',       true)
const delLatency  = new Trend('gridtrack_delivery_write_latency',  true)
const dgLatency   = new Trend('gridtrack_district_group_latency',  true)
const hubLatency  = new Trend('gridtrack_signalr_neg_latency',     true)
const errorRate   = new Rate('gridtrack_error_rate')
const accepted    = new Counter('gridtrack_tel_accepted')   // 204s only — real write-behind hits

const mixReqs = {
    telemetry: new Counter('gridtrack_mix_telemetry_reqs'),
    analytics: new Counter('gridtrack_mix_analytics_reqs'),
    delivery:  new Counter('gridtrack_mix_delivery_reqs'),
    district:  new Counter('gridtrack_mix_district_reqs'),
}

const COMPARE_THRESHOLDS = {
    http_req_failed:     ['rate<0.01'],
    gridtrack_error_rate: ['rate<0.01'],
}

// Catastrophic-breakage ceilings only. The goal is to catch an accidental synchronous 
// DB call on the hot path (which blows latency by 10x+, not 30%). These are loose 
// enough to be hardware-independent on standard runners.
const STRESS_THRESHOLDS = {
    http_req_failed:                    ['rate<0.01'],
    http_req_duration:                  ['p(95)<1500'],
    'gridtrack_driver_tel_latency':     ['p(95)<2000'],
    'gridtrack_analytics_latency':      ['p(95)<1000'],
    'gridtrack_delivery_write_latency': ['p(95)<2000'],
    'gridtrack_district_group_latency': ['p(95)<1200'],
    'gridtrack_signalr_neg_latency':    ['p(95)<500'],
    'gridtrack_error_rate':             ['rate<0.01'],
}

const CEILING_THRESHOLDS = {
    http_req_failed:     ['rate<0.05'],
    gridtrack_error_rate: ['rate<0.05'],
    // NO latency thresholds — the goal is to find where it breaks, not enforce fast speeds
}

// ── Scenarios ──────────────────────────────────────────────────────────────────

const baseScenarios = {

    // 1. Driver GPS telemetry — the write-behind hot path (or direct-postgres baseline).
    //    Each VU = one driver posting 1 position/s.
    //    Ramps up to DRIVER_VUS, holds to measure sustained throughput, then cools down.
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
    //    not the write-behind architecture. Full stress uses 20.
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

    // 5. District boundaries — heavy GeoJSON read (correctness-checked).
    district_boundaries: {
        executor: 'constant-vus',
        vus: 10,
        duration: QUICK ? '90s' : '3m',
        startTime: QUICK ? '15s' : '30s',
        exec: 'districtBoundaries',
    },

    // 6. Telemetry batch — the real B2B ingest path (partner posts batches).
    telemetry_batch: {
        executor: 'constant-vus',
        vus: QUICK ? 10 : 30,
        duration: QUICK ? '90s' : '3m',
        startTime: QUICK ? '15s' : '30s',
        exec: 'telemetryBatch',
    },

    // 7. AI endpoints — LIGHT smoke only (calls Python/Groq, rate-limited). Never stress.
    ai_smoke: {
        executor: 'constant-vus',
        vus: 5,
        duration: QUICK ? '60s' : '2m',
        startTime: QUICK ? '20s' : '40s',
        exec: 'aiSmoke',
    },

    // 8. SignalR — negotiate handshake. Auth is bypassed in the Docker (load) env, so no
    //    Clerk JWT is needed. Proves the hub is reachable + authorized under load.
    signalr: {
        executor: 'constant-arrival-rate',
        rate: 10,
        timeUnit: '1s',
        duration: QUICK ? '90s' : '3m',
        preAllocatedVUs: 20,
        maxVUs: 40,
        startTime: QUICK ? '15s' : '30s',
        exec: 'signalrConnect',
    },
}


const throughputScenario = {
    // 6. Measures maximum requests per second before errors or degradation.
    //    Uses constant arrival rate with no sleep.
    //    maxVUs controlled by THROUGHPUT_MAX_VUS env var to give k6 enough 
    //    goroutines to reach the 3000 RPS target stages.
    driver_telemetry_throughput: {
        executor: 'ramping-arrival-rate',
        startRate: 100,
        timeUnit: '1s',
        preAllocatedVUs: 100,
        maxVUs: THROUGHPUT_MAX_VUS,
        stages: [
            { duration: '30s', target: 500 },
            { duration: '30s', target: 1000 },
            { duration: '30s', target: 2000 },
            { duration: '30s', target: 3000 },
            { duration: '30s', target: 0 },
        ],
        exec: 'driverTelemetryNoSleep',
    },
}

const scenarios = TEST_MODE === 'throughput'
    ? throughputScenario
    : TEST_MODE === 'both'
        ? { ...baseScenarios, ...throughputScenario }
        : baseScenarios


export const options = {

    scenarios: scenarios,
    thresholds: THRESHOLD_PROFILE === 'compare'
        ? COMPARE_THRESHOLDS
        : THRESHOLD_PROFILE === 'ceiling'
            ? CEILING_THRESHOLDS
            : STRESS_THRESHOLDS,
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

// ── setup — fetch real district + delivery IDs once, share with all VUs ─────────

export function setup() {
    const data = { districtIds: [], deliveryIds: [] }

    const dRes = http.get(`${BASE}/api/districts`)
    if (dRes.status === 200) {
        try {
            const items = JSON.parse(dRes.body)
            data.districtIds = (Array.isArray(items) ? items : items.items || [])
                .map(d => d.id || d.districtId).filter(Boolean).slice(0, 20)
        } catch { /* ignore */ }
    }

    const delRes = http.get(`${BASE}/api/deliveries?pageSize=20`)
    if (delRes.status === 200) {
        try {
            data.deliveryIds = (JSON.parse(delRes.body).items || []).map(d => d.id).filter(Boolean)
        } catch { /* ignore */ }
    }

    return data
}

// ── 1. Driver telemetry ───────────────────────────────────────────────────────

export function driverTelemetry() {
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
    mixReqs.telemetry.add(1)
    telLatency.add(res.timings.duration)

    const hit = res.status === 204
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
            mixReqs.analytics.add(1)
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
        mixReqs.delivery.add(1)
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
        mixReqs.delivery.add(1)
        delLatency.add(assignRes.timings.duration)
        check(assignRes, { 'assign 2xx|409': (r) => r.status < 300 || r.status === 409 })

        sleep(jitter(0.5))

        const cancelRes = http.post(
            `${BASE}/api/deliveries/${deliveryId}/cancel`,
            JSON.stringify({ reason: 'k6 cleanup' }),
            { headers: JSON_HEADERS, tags: { name: 'delivery/cancel' } },
        )
        mixReqs.delivery.add(1)
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
        mixReqs.district.add(1)
        dgLatency.add(createRes.timings.duration)
        if (!ok(createRes, 'dg/create')) { sleep(2); return }

        let id
        try   { id = JSON.parse(createRes.body).id } catch {}
        if (!id) { sleep(2); return }

        sleep(jitter(0.2))

        const getRes = http.get(`${BASE}/api/district-groups/${id}`, { tags: { name: 'dg/get' } })
        mixReqs.district.add(1)
        dgLatency.add(getRes.timings.duration)
        ok(getRes, 'dg/get')

        sleep(jitter(0.2))

        const putRes = http.put(
            `${BASE}/api/district-groups/${id}`,
            JSON.stringify({ name: `k6-${__VU}-updated`, districtIds: [pick(DISTRICTS)] }),
            { headers: JSON_HEADERS, tags: { name: 'dg/update' } },
        )
        mixReqs.district.add(1)
        dgLatency.add(putRes.timings.duration)
        ok(putRes, 'dg/update')

        sleep(jitter(0.2))

        const delRes = http.del(`${BASE}/api/district-groups/${id}`, null, { tags: { name: 'dg/delete' } })
        mixReqs.district.add(1)
        dgLatency.add(delRes.timings.duration)
        ok(delRes, 'dg/delete')

        sleep(jitter(1.0))
    })
}

// ── 5. District boundaries — heavy GeoJSON read, correctness-checked ───────────

export function districtBoundaries() {
    const res = http.get(`${BASE}/api/districts/boundaries`, { tags: { name: 'districts/boundaries' } })
    readLatency.add(res.timings.duration)
    mixReqs.analytics.add(1)

    // Not just 2xx — the payload must actually contain district geometry.
    const good = check(res, {
        'boundaries 200': (r) => r.status === 200,
        'boundaries has data': (r) => {
            try {
                const b = JSON.parse(r.body)
                return (b.features?.length || b.items?.length || b.districts?.length || 0) > 0
            } catch { return false }
        },
    })
    errorRate.add(!good)
    sleep(jitter(0.5))
}

// ── 6. Telemetry batch — real B2B ingest path; assert it persisted, not just 202 ─

export function telemetryBatch() {
    const events = []
    for (let i = 0; i < 10; i++) {
        const driverId = DRIVER_IDS.length > 0
            ? DRIVER_IDS[(__VU + i) % DRIVER_IDS.length]
            : `00000000-0000-0000-0000-${String(__VU).padStart(12, '0')}`
        const { lat, lng } = pos()
        events.push({ type: 'position', driverId, lat, lng })
    }

    const res = http.post(
        `${BASE}/api/telemetry/batch`,
        JSON.stringify({ events }),
        { headers: JSON_HEADERS, tags: { name: 'telemetry/batch' } },
    )
    telLatency.add(res.timings.duration)
    mixReqs.telemetry.add(events.length)

    const good = check(res, {
        'batch 202': (r) => r.status === 202,
        'batch processed all': (r) => {
            try { return JSON.parse(r.body).processed === events.length } catch { return false }
        },
    })
    errorRate.add(!good)
    sleep(jitter(0.5))
}

// ── 7. AI smoke — LIGHT only. Verify the endpoints respond (incl. graceful degrade) ─
//      Deliberately NOT added to errorRate: with Python absent these degrade by design.

export function aiSmoke(data) {
    const districtIds = (data && data.districtIds) || []
    const deliveryIds = (data && data.deliveryIds) || []

    if (districtIds.length > 0) {
        const r1 = http.get(`${BASE}/api/ai/district-summary/${pick(districtIds)}`,
            { tags: { name: 'ai/district-summary' } })
        check(r1, { 'district-summary responded <500': (r) => r.status > 0 && r.status < 500 })
    }

    if (deliveryIds.length > 0) {
        const r2 = http.get(`${BASE}/api/ai/delivery/${pick(deliveryIds)}/recommendation`,
            { tags: { name: 'ai/recommendation' } })
        check(r2, { 'recommendation responded <500': (r) => r.status > 0 && r.status < 500 })
    }

    sleep(jitter(2.0))  // light cadence — never hammer the AI path
}

// ── 8. SignalR — negotiate handshake (auth bypassed in Docker, no JWT) ─────────

export function signalrConnect() {
    const res = http.post(
        `${BASE}/hubs/dashboard/negotiate?negotiateVersion=1`, null,
        { tags: { name: 'signalr/negotiate' } },
    )
    hubLatency.add(res.timings.duration)
    const good = check(res, { 'negotiate 200': (r) => r.status === 200 })
    errorRate.add(!good)
    sleep(0.1)
}

// ── 6. System's Throughput ──────────────────────────────────────────────────────

export function driverTelemetryNoSleep() {
    const driverId = DRIVER_IDS.length > 0
        ? DRIVER_IDS[__VU % DRIVER_IDS.length]
        : `00000000-0000-0000-0000-${String(__VU).padStart(12, '0')}`

    const { lat, lng } = pos()

    const res = http.post(
        TEL_URL,
        JSON.stringify({ driverId, lat, lng }),
        { headers: JSON_HEADERS, tags: { name: 'telemetry/position' } },
    )
    mixReqs.telemetry.add(1)
    telThroughputLatency.add(res.timings.duration)

    const hit = res.status === 204
    const ok = hit || res.status === 404
    if (hit) accepted.add(1)
    check(res, { 'telemetry 204|404': () => ok })
    errorRate.add(!ok)
    // NO sleep — fire as fast as the arrival rate dictates
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

    const payloadStr = JSON.stringify({ driverId: "uuid", lat: 33.5, lng: 36.2 });
    const payloadBytes = payloadStr.length;

    const contextBlock = `
    ### Test Context
    | Setting | Value |
    |---------|-------|
    | **Endpoint** | \`${TEL_URL}\` |
    | **Write-Behind** | \`${WRITE_BEHIND}\` |
    | **Payload Size** | \`${payloadBytes} bytes\` |
    | **Payload Structure** | \`{ driverId: "uuid", lat: float, lng: float }\` |
    
    <details>
    <summary>Example Payload</summary>
    
    \`\`\`json
     ${payloadStr}
    \`\`\`
    </details>
    `

    const md = `## GridTrack Load Test — \`${MODE_LABEL}\`

> **VUs:** ${vus} &nbsp;|&nbsp; **Requests:** ${reqs} &nbsp;|&nbsp; **RPS:** ${rps} &nbsp;|&nbsp; **Error rate:** ${errR}
    
    ${contextBlock}
    
### Latency by path (ms)

| Path | p(50) | p(90) | p(95) | p(99) |
|------|------:|------:|------:|------:|
| Telemetry (${MODE_LABEL}) | ${m('gridtrack_driver_tel_latency','p(50)')} | ${m('gridtrack_driver_tel_latency','p(90)')} | ${m('gridtrack_driver_tel_latency','p(95)')} | ${m('gridtrack_driver_tel_latency','p(99)')} |
| Analytics reads | ${m('gridtrack_analytics_latency','p(50)')} | ${m('gridtrack_analytics_latency','p(90)')} | ${m('gridtrack_analytics_latency','p(95)')} | ${m('gridtrack_analytics_latency','p(99)')} |
| Delivery lifecycle | ${m('gridtrack_delivery_write_latency','p(50)')} | ${m('gridtrack_delivery_write_latency','p(90)')} | ${m('gridtrack_delivery_write_latency','p(95)')} | ${m('gridtrack_delivery_write_latency','p(99)')} |
| District group CRUD | ${m('gridtrack_district_group_latency','p(50)')} | ${m('gridtrack_district_group_latency','p(90)')} | ${m('gridtrack_district_group_latency','p(95)')} | ${m('gridtrack_district_group_latency','p(99)')} |

*Generated by k6 ${new Date().toISOString()}*
`

    const outDir  = 'load-tests/results'
    const RUN_TYPE = THRESHOLD_PROFILE === 'compare'
        ? 'comparison'
        : THRESHOLD_PROFILE === 'ceiling'
            ? 'throughput'
            : 'stress'
    const outBase  = `${outDir}/${RUN_TYPE}-${MODE_LABEL}`

    return {
        stdout:              textSummary(data, { indent: ' ', enableColors: true }),
        [`${outBase}.md`]:   md,
        [`${outBase}.json`]: JSON.stringify(data, null, 2),
    }
}