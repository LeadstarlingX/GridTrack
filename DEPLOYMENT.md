# GridTrack — Deployment Runbook

Stack: **Neon** (Postgres/PostGIS) · **Render** (API + Python + Redis) · **CloudAMQP** (RabbitMQ) · **Vercel** (Frontend)

---

## 1. One-time account setup

| Service | Free tier | Sign up |
|---|---|---|
| Neon | Unlimited storage, no expiry, PostGIS extension | https://neon.tech |
| Render | 750 instance-hrs/month shared across web services | https://render.com |
| CloudAMQP | Lemur plan — 1 M msgs/month | https://cloudamqp.com |
| Vercel | Unlimited deployments, always-on CDN | https://vercel.com |

---

## 2. Neon — Postgres + PostGIS

1. Create project → choose region closest to your users.
2. In the SQL editor run:
   ```sql
   CREATE EXTENSION IF NOT EXISTS postgis;
   ```
3. Copy the **connection string** from Settings → Connection string.  
   Format: `Host=<host>;Port=5432;Database=neondb;User Id=<user>;Password=<pass>;SSLMode=Require`
4. Save it — you'll paste it into Render as `ConnectionStrings__DefaultConnection`.

> Migrations run automatically on API startup via `app.ApplyMigrations()`.  
> `SeedService` seeds initial districts/drivers when the database is empty.

---

## 3. CloudAMQP — RabbitMQ

1. Create instance → **Lemur** plan (free).
2. Copy the **AMQP URL** from the instance dashboard.  
   Format: `amqps://user:pass@host/vhost`
3. Save it — used as `ConnectionStrings__Queue` (API) and `RABBITMQ_URL` (Python).

> **Budget:** 1 M messages/month. A 1 Hz position event stream = ~2.6 M msgs/month — exhausts the cap. Keep the mock generator off in production. Manual demo bursts are well within budget.

---

## 4. Render — API service

1. Connect your GitHub account to Render.
2. **New Web Service** → select the `GridTrack` backend repo.
3. Render auto-detects `render.yaml` in the repo root and proposes both services (`gridtrack-api` + `gridtrack-redis`). Approve.
4. After the Redis service is created, Render auto-populates `ConnectionStrings__Cache` in the API's env vars.
5. Fill in the remaining `sync: false` env vars in the Render dashboard:

   | Key | Value |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | Neon connection string (step 2) |
   | `ConnectionStrings__Queue` | CloudAMQP AMQP URL (step 3) |
   | `Clerk__Authority` | `https://<your-instance>.clerk.accounts.dev` |
   | `Cors__AllowedOrigin` | Your Vercel frontend URL, e.g. `https://gridtrack.vercel.app` |
   | `Python__BaseUrl` | Your Python service URL, e.g. `https://gridtrack-forecasting.onrender.com` |

6. Deploy — first build takes ~3 min. Watch logs for `ApplyMigrations` completing.
7. Test: `curl https://gridtrack-api.onrender.com/health` → `"ok"`.

> **Cold start:** Render free tier spins down after 15 min of inactivity. Wake-up takes ~50 s.  
> Pre-warm before a demo: hit `/health` once, wait for 200 OK, then proceed.

---

## 5. Render — Python forecasting service

1. **New Web Service** → select the `gridtrack-forecasting` repo.
2. Render auto-detects `render.yaml`. Approve.
3. Fill in env vars:

   | Key | Value |
   |---|---|
   | `RABBITMQ_URL` | CloudAMQP AMQP URL (step 3) |
   | `GROQ_API_KEY` | https://console.groq.com/keys |
   | `GOOGLE_API_KEY` | Google AI Studio API key (fallback LLM) |

4. Test: `curl https://gridtrack-forecasting.onrender.com/health` → `{"status":"ok"}`.

---

## 6. Redis connection string note

Render provides the Redis connection string in `redis://red-xxxxx:6379` format.  
StackExchange.Redis (v2.x) parses this correctly via `ConnectionMultiplexer.Connect(url)`.  
If you see a connection error at startup, override `ConnectionStrings__Cache` manually with the **Internal Connection String** in `host:port` format from the Render Redis dashboard.

---

## 7. Vercel — Frontend

1. Import the `GridTrack.Web` repo in Vercel.
2. **Framework preset:** Vite.
3. Set environment variables:

   | Key | Value |
   |---|---|
   | `VITE_CLERK_PUBLISHABLE_KEY` | Your Clerk publishable key |
   | `VITE_API_BASE_URL` | `https://gridtrack-api.onrender.com` |
   | `VITE_HUB_URL` | `https://gridtrack-api.onrender.com/hubs/dashboard` |
   | `VITE_USE_MOCK_SIGNALR` | `false` |

4. Deploy. The `vercel.json` in the repo root handles SPA rewrite (`/*` → `index.html`).
5. Copy the Vercel deployment URL (e.g. `https://gridtrack.vercel.app`) and paste it into Render as `Cors__AllowedOrigin`.

---

## 8. On/off cycle

The frontend (Vercel CDN) is always reachable — no action needed.

**Suspend backend** (to save the 750 hrs/month Render budget):  
Render dashboard → service → **Suspend**.  
Frontend degrades gracefully: SignalR shows "disconnected", REST queries time out with an offline banner.

**Resume backend:**  
Render dashboard → service → **Resume**.  
The frontend's `withAutomaticReconnect` and REST retry logic reconnect automatically within ~60 s of the service becoming healthy.

---

## 9. Optional: keep-alive pinger

If you want the API to stay warm for a demo window without manually pre-warming:

Add a free **UptimeRobot** monitor (`https://gridtrack-api.onrender.com/health`, 5 min interval).  
This counts toward the 750 hrs/month cap — one service at 730 hrs fits; enabling keep-alive on both API **and** Python exceeds the cap.  
Recommended: keep-alive the API only; let Python cold-start on demand (forecast/anomaly scoring still works, just with a ~50 s first-request delay).

---

## 10. Post-deploy smoke test

```
# 1. API health
curl https://gridtrack-api.onrender.com/health

# 2. Python health
curl https://gridtrack-forecasting.onrender.com/health

# 3. Full anomaly round-trip (requires auth token)
# Open the frontend → trigger a delivery anomaly via the mock generator
# → check the Alerts page for an AI urgency note within ~10 s

# 4. Frontend graceful degradation
# Suspend the API from Render dashboard
# → Frontend should show the offline/reconnecting banner within ~30 s
# Resume → frontend reconnects automatically
```
