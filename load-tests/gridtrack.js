/**
 * GridTrack k6 load test
 *
 * Targets Render free tier: 0.5 vCPU / 256 MB .NET · 128 MB Python · 25 MB Redis
 * Run:
 *   k6 run --env BASE=https://gridtrack-api.onrender.com gridtrack.js
 *   k6 run --env BASE=http://localhost:5000 gridtrack.js          (local)
 *
 * Scenarios are staggered so the service doesn't hit all load simultaneously.
 * Each scenario is independently thresholded so you can see which path breaks first.
 */

import http from 'k6/http'
import { check, sleep, group } from 'k6'
import { Rate, Trend } from 'k6/metrics'

// ── Config ────────────────────────────────────────────────────────────────────

const BASE        = __ENV.BASE        || 'http://localhost:5000'
// How many simulated drivers to hammer the telemetry endpoint.
// Local smoke:   DRIVER_VUS=10   (~10 events/s, verifies the pipeline works)
// Stress test:   DRIVER_VUS=1000 (~1000 events/s, single-container limit test)
const DRIVER_VUS  = parseInt(__ENV.DRIVER_VUS  || '50')
// Optional Clerk JWT — required only for signalr_negotiate (hub is [Authorize]).
// If omitted that scenario still runs but skips the HTTP request (no error inflation).
// Obtain from browser DevTools: Network → dashboardHub/negotiate → Authorization header.
const JWT_TOKEN   = __ENV.JWT_TOKEN   || ''

// Seed data — replace with real IDs from your database before running remotely.
// LOCAL: run the app, fetch /api/deliveries, copy a few IDs here.
const SEED = {
    deliveryIds: [
        // 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
    ],
    driverIds: [
        // 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx',
    ],
    districtIds: ['mezzeh', 'malki', 'kafarsouseh'],
    // Damascus centre — matches APP_CONFIG.map.center
    sampleLat: 33.5138,
    sampleLng: 36.2765,
}

// ── Custom metrics ────────────────────────────────────────────────────────────

const analyticsLatency     = new Trend('gridtrack_analytics_latency',    true)
const signalrNegLatency    = new Trend('gridtrack_signalr_neg_latency',  true)
const deliveryWriteLatency = new Trend('gridtrack_delivery_write_latency', true)
const aiChatLatency        = new Trend('gridtrack_ai_chat_latency',      true)
const driverTelLatency     = new Trend('gridtrack_driver_tel_latency',   true)
const errorRate            = new Rate('gridtrack_error_rate')

// ── Options ───────────────────────────────────────────────────────────────────

export const options = {
    scenarios: {
        // 1. Read / analytics — simulates multiple dashboard browser tabs
        analytics_read: {
            executor: 'ramping-vus',
            startVUs: 0,
            stages: [
                { duration: '20s', target: 10 },  // warm up
                { duration: '60s', target: 30 },  // sustained moderate
                { duration: '30s', target: 50 },  // peak — Render's breaking point
                { duration: '20s', target: 0  },  // cool down
            ],
            gracefulRampDown: '10s',
            exec: 'analyticsRead',
        },

        // 2. SignalR negotiate — one per browser tab; each reconnects every ~30s on Render cold starts
        signalr_negotiate: {
            executor: 'constant-arrival-rate',
            rate: 3,               // 3 negotiates/sec = 180/min
            timeUnit: '1s',
            duration: '2m',
            preAllocatedVUs: 10,
            maxVUs: 20,
            startTime: '10s',      // start after analytics warms the service
            exec: 'signalrNegotiate',
        },

        // 3. Write path — delivery lifecycle under load
        delivery_writes: {
            executor: 'constant-vus',
            vus: 5,
            duration: '90s',
            startTime: '30s',
            exec: 'deliveryWrites',
        },

        // 4. AI chat — Groq-backed; test latency ceiling + 429 handling
        //    Low VU count because Groq free tier rate-limits aggressively.
        ai_chat: {
            executor: 'constant-vus',
            vus: 2,
            duration: '60s',
            startTime: '60s',
            exec: 'aiChat',
        },

        // 5. Driver GPS telemetry — the real stress test.
        //    Each VU = one driver posting one position update per second.
        //    DRIVER_VUS=1000 → 1000 events/s through the full pipeline
        //    (handler → EF → Postgres → DomainEvent → SignalR broadcast).
        //    Pair with `dotnet-counters monitor` to watch CPU/memory ceiling.
        //    Pre-condition: SEED.driverIds must be populated with real IDs,
        //    or run with Simulation:Enabled=false and let k6 be the only source.
        driver_telemetry: {
            executor: 'constant-vus',
            vus: DRIVER_VUS,
            duration: '2m',
            startTime: '15s',   // start after analytics warms the service
            exec: 'driverTelemetry',
        },
    },

    thresholds: {
        // Overall HTTP health
        http_req_failed:             ['rate<0.02'],          // <2% error rate
        http_req_duration:           ['p(95)<3000'],         // broad safety net

        // Per-scenario custom metrics
        'gridtrack_analytics_latency':     ['p(95)<500', 'p(99)<1500'],
        'gridtrack_signalr_neg_latency':   ['p(95)<300'],
        'gridtrack_delivery_write_latency':['p(95)<800'],
        'gridtrack_ai_chat_latency':       ['p(95)<8000'],   // Groq cold start can be slow
        'gridtrack_driver_tel_latency':    ['p(95)<200', 'p(99)<500'],  // hot path must be fast
        'gridtrack_error_rate':            ['rate<0.02'],
    },
}

// ── Helpers ───────────────────────────────────────────────────────────────────

const JSON_HEADERS = { 'Content-Type': 'application/json', 'Accept': 'application/json' }

function today()     { return new Date().toISOString().slice(0, 10) }
function daysAgo(n)  { const d = new Date(); d.setDate(d.getDate() - n); return d.toISOString().slice(0, 10) }

function ok(res, tag) {
    const passed = check(res, {
        [`${tag} status 2xx`]: (r) => r.status >= 200 && r.status < 300,
    })
    errorRate.add(!passed)
    return passed
}

function randomFrom(arr) {
    return arr[Math.floor(Math.random() * arr.length)]
}

// ── Scenario 1: Analytics read ────────────────────────────────────────────────

export function analyticsRead() {
    group('analytics', () => {
        // Dashboard landing — these fire on every page open
        const summaryRes = http.get(`${BASE}/api/analytics/summary`, { tags: { name: 'analytics_summary' } })
        analyticsLatency.add(summaryRes.timings.duration)
        ok(summaryRes, 'analytics/summary')

        sleep(0.2)

        const distRes = http.get(`${BASE}/api/districts`, { tags: { name: 'districts' } })
        ok(distRes, 'districts')

        sleep(0.3)

        // Trend chart (7-day default)
        const trendRes = http.get(
            `${BASE}/api/analytics/trends?from=${daysAgo(7)}&to=${today()}&granularity=day`,
            { tags: { name: 'analytics_trends' } },
        )
        analyticsLatency.add(trendRes.timings.duration)
        ok(trendRes, 'analytics/trends')

        sleep(0.5)

        // Deliveries table (first page)
        const delRes = http.get(
            `${BASE}/api/deliveries?pageSize=6&from=${daysAgo(7)}&to=${today()}`,
            { tags: { name: 'deliveries_list' } },
        )
        analyticsLatency.add(delRes.timings.duration)
        ok(delRes, 'deliveries list')

        sleep(0.4)

        // Driver list
        const drvRes = http.get(`${BASE}/api/drivers?pageSize=8`, { tags: { name: 'drivers_list' } })
        ok(drvRes, 'drivers list')

        sleep(0.3)

        // Driver analytics (heavier — multiple joins)
        const drvAnaRes = http.get(`${BASE}/api/analytics/drivers`, { tags: { name: 'analytics_drivers' } })
        analyticsLatency.add(drvAnaRes.timings.duration)
        ok(drvAnaRes, 'analytics/drivers')

        sleep(randomFrom([0.5, 0.8, 1.2]))  // simulate human think time
    })
}

// ── Scenario 2: SignalR negotiate ─────────────────────────────────────────────

export function signalrNegotiate() {
    // Negotiate is the HTTP handshake that precedes the WebSocket upgrade.
    // k6 can't maintain long-lived WebSocket connections in arrival-rate mode,
    // so we test the most expensive part: the negotiate POST.
    //
    // The hub has [Authorize] — without JWT_TOKEN the request returns 401,
    // which is auth working correctly, not a pipeline error.
    // Pass --env JWT_TOKEN=<clerk_jwt> to test authenticated negotiate latency.
    if (!JWT_TOKEN) {
        sleep(1)
        return
    }

    const res = http.post(
        `${BASE}/dashboardHub/negotiate?negotiateVersion=1`,
        null,
        {
            headers: { 'Authorization': `Bearer ${JWT_TOKEN}` },
            tags: { name: 'signalr_negotiate' },
        },
    )
    signalrNegLatency.add(res.timings.duration)
    check(res, { 'negotiate 200': (r) => r.status === 200 })
    errorRate.add(res.status !== 200)
    sleep(0.2)
}

// ── Scenario 3: Delivery write path ───────────────────────────────────────────

export function deliveryWrites() {
    group('delivery_write', () => {
        // Create a delivery
        const createRes = http.post(
            `${BASE}/api/deliveries`,
            JSON.stringify({
                lat: SEED.sampleLat + (Math.random() - 0.5) * 0.05,
                lng: SEED.sampleLng + (Math.random() - 0.5) * 0.05,
                districtId: null,   // let backend H3-detect
            }),
            { headers: JSON_HEADERS, tags: { name: 'delivery_create' } },
        )
        deliveryWriteLatency.add(createRes.timings.duration)
        const created = ok(createRes, 'delivery create')

        if (!created) { sleep(1); return }

        let deliveryId = null
        try { deliveryId = JSON.parse(createRes.body).deliveryId } catch {}
        if (!deliveryId) { sleep(1); return }

        sleep(0.5)

        // Auto-assign a driver (exercises PostGIS nearest-driver query + Wolverine pipeline)
        const assignRes = http.post(
            `${BASE}/api/deliveries/${deliveryId}/auto-assign`,
            null,
            { tags: { name: 'delivery_auto_assign' } },
        )
        deliveryWriteLatency.add(assignRes.timings.duration)
        // 409 = no driver available — not an error for our purposes
        check(assignRes, { 'auto-assign 2xx or 409': (r) => r.status < 300 || r.status === 409 })

        sleep(0.8)

        // Cancel the delivery (clean up, avoids polluting DB with test data)
        const cancelRes = http.post(
            `${BASE}/api/deliveries/${deliveryId}/cancel`,
            JSON.stringify({ reason: 'k6 load test cleanup' }),
            { headers: JSON_HEADERS, tags: { name: 'delivery_cancel' } },
        )
        ok(cancelRes, 'delivery cancel')

        sleep(randomFrom([0.5, 1.0, 1.5]))
    })
}

// ── Scenario 5: Driver GPS telemetry ─────────────────────────────────────────
//
// Usage:
//   k6 run --env BASE=http://localhost:5000 --env DRIVER_VUS=1000 gridtrack.js
//
// Each VU simulates one driver moving around Damascus at ~1 update/s.
// Jitter keeps positions realistic (± 0.05°) and avoids identical coordinates.
// If SEED.driverIds is populated, VUs cycle through real IDs; otherwise a
// stable random UUID per VU is used so the endpoint returns 404 (expected
// when Simulation is still running and drivers exist in DB, fill the seed).
//
// Pair with: dotnet-counters monitor --process-name GridTrack.Api
//   to watch GC pressure, thread pool queue depth, CPU ceiling in real time.

export function driverTelemetry() {
    // Stable "driver" identity for this VU — reuses same ID each iteration
    // so position updates look like one continuous driver trace.
    const driverId = SEED.driverIds.length > 0
        ? SEED.driverIds[__VU % SEED.driverIds.length]
        : `00000000-0000-0000-0000-${String(__VU).padStart(12, '0')}`

    const lat = SEED.sampleLat + (Math.random() - 0.5) * 0.05
    const lng = SEED.sampleLng + (Math.random() - 0.5) * 0.05

    const res = http.post(
        `${BASE}/api/telemetry/position`,
        JSON.stringify({ driverId, lat, lng }),
        { headers: JSON_HEADERS, tags: { name: 'driver_telemetry' } },
    )

    driverTelLatency.add(res.timings.duration)

    // 404 = driver not in DB (seed not populated) — expected in smoke runs,
    // not counted as a pipeline error so the test run still finishes cleanly.
    const pipelineError = res.status !== 204 && res.status !== 404
    check(res, { 'telemetry 204 or 404': (r) => !pipelineError })
    errorRate.add(pipelineError)

    // 1 Hz: subtract the request duration so we don't drift above 1/s
    const remaining = Math.max(0, 1000 - res.timings.duration) / 1000
    if (remaining > 0) sleep(remaining)
}

// ── Scenario 4: AI chat ───────────────────────────────────────────────────────

export function aiChat() {
    // Use non-streaming endpoint — streaming needs k6 ws or experimental fetch.
    // This still exercises: .NET → Groq API → JSON parse → response.
    const res = http.post(
        `${BASE}/api/analysis/chat`,
        JSON.stringify({
            messages: [
                { role: 'user', content: 'What is the current status of district mezzeh? Be brief.' },
            ],
            csvData: '',  // minimal payload — tests Groq latency, not CSV parsing
        }),
        {
            headers: JSON_HEADERS,
            tags: { name: 'ai_chat' },
            timeout: '15s',   // Groq cold start can be 8–12s
        },
    )
    aiChatLatency.add(res.timings.duration)

    // 429 = Groq rate limit — expected under load, not a system failure
    const rateLimited = res.status === 429
    check(res, {
        'ai chat ok or rate-limited': (r) => r.status === 200 || r.status === 429,
    })
    errorRate.add(res.status !== 200 && !rateLimited)

    // Back off if rate-limited
    sleep(rateLimited ? 3 : randomFrom([2, 3, 4]))
}
