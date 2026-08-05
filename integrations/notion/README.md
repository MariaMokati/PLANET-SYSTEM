# LMX → Notion trade journal

A Cloudflare Worker that turns LMX v3 TradingView alerts into rows in a Notion
trade-journal database.

> **HONESTY (same rule as the root README).** This Worker is **unit-tested
> locally only**. It has **never been deployed**, and has **never been run
> against a live Notion workspace or a live TradingView alert** from this repo.
> The Notion request shape is written to the documented API, not verified
> against it. The Pine side (`MODULE 17b`) is **hand-reviewed, not compiled**.
> Deploy it, fire one test alert, and check the row actually appears before you
> rely on it for anything.

---

## What it does

Pine Script is sandboxed: it **cannot** call an external API. The only thing a
strategy can do is emit a string via `alert()`. So the journal is a relay:

```
  ATCS_strategy.pine              alert() message = a JSON string
        │                         (built in MODULE 17b)
        ▼
  TradingView alert  ──── HTTPS POST ────►  Cloudflare Worker
  (Webhook URL = your Worker)               POST /webhook
                                                  │
                                                  │  validate + map
                                                  ▼
                                            Notion API
                                            POST /v1/pages
                                                  │
                                                  ▼
                                        Trade-journal database row
```

The Worker validates the payload against a fixed contract, maps it to Notion
properties, retries transient Notion failures, and returns the created page id.
It has **no runtime dependencies** — built-in `fetch` only.

---

## The alert payload contract

Defined once in [`src/payload.js`](./src/payload.js). Every numeric field also
accepts a **numeric string**, because TradingView serialises interpolated values
as text.

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `event` | string | yes | `entry` \| `exit` \| `sweep` |
| `secret` | string | see below | shared secret; not required *in the contract*, but the Worker rejects the request without it unless the `X-Webhook-Secret` header is used |
| `symbol` | string | yes | `syminfo.tickerid`, e.g. `OANDA:XAUUSD` |
| `timeframe` | string | yes | `timeframe.period`, e.g. `15` |
| `direction` | string | yes | `long` \| `short`, accepted case-insensitively, stored lowercase |
| `price` | number | yes | close of the signal bar |
| `entry` | number | no | planned limit entry |
| `stop` | number | no | structural stop |
| `target` | number | no | TP2 / the draw-on-liquidity target |
| `quality` | number | no | LMX quality score, 0–100 |
| `session` | string | no | `LONDON` \| `NEW YORK` \| `ASIA` \| `OFF` |
| `note` | string | no | the strategy's own reason string |
| `time` | string | no | ISO-8601; the Worker stamps receipt time if absent |

Example (an entry alert, as the strategy emits it):

```json
{"event":"entry","secret":"s3cret-abc","symbol":"OANDA:XAUUSD","timeframe":"15","direction":"long","price":4512.5,"entry":4508.25,"stop":4496.8,"target":4551.4,"quality":82,"session":"LONDON","note":"LONG A: SSL swept + bullish displacement + MSS + discount -> draw BSL @ 4551.40","time":"2026-08-05T13:45:00Z"}
```

Optional fields that the strategy does not have for a given event (an exit knows
the fill price, not the original entry/stop/target) are **omitted entirely** —
never sent as `null`, which Notion rejects on typed properties.

---

## The Notion database schema

Create a database with **exactly these property names and types**. Names are
case-sensitive and Notion will 400 on anything it does not recognise.

| Notion property | Type | Source field |
| --- | --- | --- |
| `Name` | Title | composed: `{SYMBOL} {DIRECTION} {EVENT}`, e.g. `OANDA:XAUUSD LONG ENTRY` |
| `Event` | Select | `event` |
| `Symbol` | Text (rich text) | `symbol` |
| `Timeframe` | Select | `timeframe` |
| `Direction` | Select | `direction` |
| `Price` | Number | `price` |
| `Entry` | Number | `entry` |
| `Stop` | Number | `stop` |
| `Target` | Number | `target` |
| `Quality` | Number | `quality` |
| `Session` | Select | `session` |
| `Note` | Text (rich text) | `note` |
| `Signal Time` | Date | `time` |

You do **not** need to pre-create the Select options — the Notion API adds a new
option when it sees an unfamiliar value.

**Renamed a column?** Change it in one place: `PROPERTY_NAMES` in
[`src/notion.js`](./src/notion.js). Nothing else hard-codes those strings.

---

## Setup

### 1. Create the Notion integration and get the token

1. Go to <https://www.notion.so/profile/integrations> (or *Settings →
   Connections → Develop or manage integrations*).
2. **New integration** → name it (e.g. `LMX Journal`) → pick the workspace →
   capabilities: **Insert content** is the only one required.
3. Copy the **Internal Integration Secret** (starts with `ntn_` on newer
   workspaces, `secret_` on older ones). This is your `NOTION_TOKEN`. Treat it
   like a password — it is not scoped to one database by itself.

### 2. Create the database

Create a **full-page database** (not an inline table if you can avoid it) with
the schema above.

### 3. Share the database with the integration — DO NOT SKIP THIS

**This is the single most common reason the integration "doesn't work".** A
Notion integration can see *nothing* until a page is explicitly shared with it.
The token being valid is not enough.

On the database page: **⋯ (top right) → Connections → Add connections →** pick
your integration → confirm.

If you skip this, the Worker returns **502** wrapping a Notion **404
`Could not find database with ID`** — which reads like a wrong id, but is almost
always an unshared database.

### 4. Get the database id

From the database URL:

```
https://www.notion.so/myworkspace/1f0a2b3c4d5e6f708192a3b4c5d6e7f8?v=...
                                  └──────── NOTION_DATABASE_ID ────────┘
```

It is the 32-hex-character chunk before the `?`. Dashed UUID form works too.

### 5. Set the three secrets

From `integrations/notion/`:

```bash
npm install                       # wrangler only; no runtime deps
npx wrangler login

npx wrangler secret put NOTION_TOKEN
npx wrangler secret put NOTION_DATABASE_ID
npx wrangler secret put WEBHOOK_SECRET   # any long random string, e.g. `openssl rand -hex 24`
```

Secrets go **only** through `wrangler secret put`. Do not put them in
`wrangler.toml` — that file is committed to git.

### 6. Deploy

```bash
npm run deploy
```

Wrangler prints the Worker URL, e.g.
`https://planet-system-notion.<your-subdomain>.workers.dev`.

Check it is alive:

```bash
curl https://planet-system-notion.<your-subdomain>.workers.dev/health
# {"ok":true}
```

And send a fake alert end-to-end:

```bash
curl -X POST https://planet-system-notion.<your-subdomain>.workers.dev/webhook \
  -H 'Content-Type: application/json' \
  -d '{"event":"entry","secret":"YOUR_WEBHOOK_SECRET","symbol":"TEST:XAUUSD","timeframe":"15","direction":"long","price":"4512.5","quality":"82"}'
# {"ok":true,"notionPageId":"..."}
```

A row should appear in the database. If it does not, read *Troubleshooting*.

### 7. Configure the TradingView alert

1. Open `ATCS_strategy.pine` on your chart. In the settings, under **Webhook
   (Notion trade journal)**: tick **Emit JSON alert() payloads** and paste your
   `WEBHOOK_SECRET` into **Webhook Secret**. (It is off by default, so the
   strategy behaves exactly as before until you turn it on.)
2. **Create Alert** → *Condition* = **LMX v3** → **`Any alert() function call`**.
   This is essential: the JSON is emitted by `alert()`, and the older
   `alertcondition()` events at MODULE 17 carry only their fixed plain-text
   message.
3. **Leave the alert Message box alone.** With `Any alert() function call` the
   message is supplied by the script — the JSON payload built in MODULE 17b.
   Anything you type there is ignored.
4. *Notifications* tab → tick **Webhook URL** → paste
   `https://planet-system-notion.<your-subdomain>.workers.dev/webhook`.
5. Trigger: **Once Per Bar Close**.

The secret travels in the JSON body because TradingView cannot set custom
request headers. The Worker accepts `X-Webhook-Secret` too, for `curl` and for
self-hosted relays that can set headers.

---

## Local development

```bash
cp .dev.vars.example .dev.vars     # then fill in the three values
npm run dev                        # wrangler dev, on http://localhost:8787
```

`.dev.vars` is gitignored and must never be committed. Note that `wrangler dev`
still calls the **real** Notion API, so rows created while testing are real rows.

Run the tests (built-in `node:test`, no test framework to install):

```bash
npm test        # == node --test
```

---

## Behaviour reference

| Request | Response |
| --- | --- |
| `GET /health` | `200 {"ok":true}` |
| `POST /webhook`, everything good | `200 {"ok":true,"notionPageId":"..."}` |
| any other method or path | `405` |
| body is not valid JSON | `400` with a pointer at the Pine template |
| missing / bad secret | `401` |
| payload fails the contract | `400` with a `details` array listing each problem |
| `WEBHOOK_SECRET` or the Notion credentials unset | `500` (fails closed) |
| Notion rejects the page | `502` with `notionStatus` and a truncated `notionError` |

Retries: the Notion call is attempted up to **3 times** on `429` and `5xx`,
honouring `Retry-After` when present, otherwise backing off 250 ms → 500 ms →
1000 ms. Any other `4xx` is permanent and is not retried.

Logging: payload fields only (event, symbol, timeframe, direction, price,
quality, time). The shared secret and the Notion token are never logged.

---

## Troubleshooting

**`401 unauthorized: bad or missing webhook secret`**
The secret in the alert does not match `WEBHOOK_SECRET`. Re-run
`npx wrangler secret put WEBHOOK_SECRET` and re-paste the same value into the
strategy's *Webhook Secret* input. Watch for a trailing space or newline when
copying. Note that MODULE 17b sanitises `"` and `\` out of the JSON, so a secret
containing either will never match — use letters, digits and dashes.

**`400 body is not valid JSON`**
TradingView forwards the alert message verbatim, so the Worker received
something that is not JSON. Usual causes: the alert was created on an
`alertcondition()` event (which sends plain text) instead of `Any alert()
function call`; or you typed your own text into the alert Message box. Check
what actually arrived with `npx wrangler tail`.

**`400 invalid payload`**
The `details` array names each offending field. Most often `direction` is not
`long`/`short`, or `price` came through empty.

**`502` wrapping a Notion `404 Could not find database with ID`**
The database is **not shared with the integration** — go back to setup step 3.
Failing that, the id is wrong (check you copied the *database* URL, not a page
inside it).

**`502` wrapping a Notion `400 ... is not a property that exists`**
A column name or type does not match the schema table above. Either fix the
column in Notion, or remap it in `PROPERTY_NAMES` in `src/notion.js`.

**`502` wrapping a Notion `401 API token is invalid`**
The token is wrong or was rotated. Re-run `npx wrangler secret put NOTION_TOKEN`.

**`500 worker misconfigured`**
One of the three secrets was never set on the deployed Worker. `npx wrangler
secret list` shows what is there.

**Nothing arrives at all**
Check TradingView's *Alerts → Log* for the delivery result, then `npx wrangler
tail` to see whether the request reached the Worker. TradingView only allows
webhooks on paid plans, and the URL must be `https` on a standard port.

---

## Files

| Path | What it is |
| --- | --- |
| `src/index.js` | the Worker: routing, auth, retries, error mapping |
| `src/payload.js` | the payload contract: validation + normalisation |
| `src/notion.js` | payload → Notion page mapper, and `PROPERTY_NAMES` |
| `test/payload.test.js` | contract tests |
| `test/notion.test.js` | mapping tests |
| `wrangler.toml` | Worker config (no secrets) |
| `.dev.vars.example` | template for local secrets |
