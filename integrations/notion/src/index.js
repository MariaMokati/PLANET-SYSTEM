// =============================================================================
// PLANET-SYSTEM -> NOTION WEBHOOK WORKER
// -----------------------------------------------------------------------------
// Pine Script is sandboxed and cannot make HTTP calls; it can only emit an
// alert() string. So the dataflow is:
//
//   ATCS_strategy.pine  --alert() JSON-->  TradingView webhook
//                       --POST /webhook-->  THIS WORKER
//                       --POST /v1/pages-->  Notion trade-journal database
//
// Dependency-free on purpose: built-in fetch only, no npm runtime packages.
//
// LOGGING POLICY: we log payload fields (symbol / event / direction / status)
// and never the shared secret or the Notion token. Do not add a log line that
// dumps `env`, the raw request body, or an outbound request's headers.
// =============================================================================

import { validatePayload } from './payload.js';
import { buildNotionPage } from './notion.js';

const NOTION_API_URL = 'https://api.notion.com/v1/pages';
const NOTION_VERSION = '2022-06-28';

/** Backoff schedule (ms) used when Notion does not send a Retry-After header. */
const BACKOFF_MS = [250, 500, 1000];

/** Max attempts for the Notion call (1 initial + 2 retries = 3 total). */
const MAX_ATTEMPTS = 3;

/** How much of a Notion error body we echo back to the caller. */
const ERROR_BODY_LIMIT = 500;

const json = (status, body, extraHeaders = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json; charset=utf-8', ...extraHeaders },
  });

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Constant-time string comparison.
 *
 * A plain `a === b` on a secret leaks its prefix through timing: the comparison
 * exits at the first differing byte. This compares every byte of the longer
 * input and folds the length difference into the same accumulator, so the work
 * done does not depend on where (or whether) the inputs diverge.
 */
export function constantTimeEqual(a, b) {
  const encoder = new TextEncoder();
  const left = encoder.encode(typeof a === 'string' ? a : '');
  const right = encoder.encode(typeof b === 'string' ? b : '');

  let diff = left.length ^ right.length;
  const length = Math.max(left.length, right.length);
  for (let i = 0; i < length; i++) {
    diff |= (i < left.length ? left[i] : 0) ^ (i < right.length ? right[i] : 0);
  }
  return diff === 0;
}

/**
 * Work out how long to wait before the next attempt.
 * Honours `Retry-After` (seconds, or an HTTP date) when Notion sends one,
 * otherwise falls back to the fixed exponential schedule.
 */
export function retryDelayMs(response, attemptIndex) {
  const fallback = BACKOFF_MS[Math.min(attemptIndex, BACKOFF_MS.length - 1)];
  const header = response?.headers?.get?.('Retry-After');
  if (!header) return fallback;

  const seconds = Number(header);
  if (Number.isFinite(seconds) && seconds >= 0) return Math.min(seconds * 1000, 30_000);

  const asDate = Date.parse(header);
  if (!Number.isNaN(asDate)) return Math.min(Math.max(asDate - Date.now(), 0), 30_000);

  return fallback;
}

/** True for statuses worth retrying: rate limiting and server-side faults. */
const isRetryable = (status) => status === 429 || status >= 500;

/**
 * POST the page to Notion, retrying 429/5xx up to MAX_ATTEMPTS.
 * Other 4xx responses are permanent (bad token, bad schema, database not
 * shared with the integration) and are returned immediately.
 */
async function createNotionPage(body, env) {
  let lastResponse = null;
  let lastText = '';

  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    let response;
    try {
      response = await fetch(NOTION_API_URL, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${env.NOTION_TOKEN}`,
          'Notion-Version': NOTION_VERSION,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
      });
    } catch (networkError) {
      // Transport-level failure: treat like a 5xx and retry.
      lastResponse = null;
      lastText = `network error: ${networkError?.message ?? 'unknown'}`;
      if (attempt < MAX_ATTEMPTS - 1) {
        await sleep(BACKOFF_MS[Math.min(attempt, BACKOFF_MS.length - 1)]);
        continue;
      }
      break;
    }

    const text = await response.text();
    if (response.ok) {
      let parsed = {};
      try {
        parsed = JSON.parse(text);
      } catch {
        parsed = {};
      }
      return { ok: true, status: response.status, pageId: parsed.id ?? null };
    }

    lastResponse = response;
    lastText = text;

    if (!isRetryable(response.status) || attempt === MAX_ATTEMPTS - 1) break;

    const delay = retryDelayMs(response, attempt);
    console.log(`notion retry: status=${response.status} attempt=${attempt + 1} delay_ms=${delay}`);
    await sleep(delay);
  }

  return {
    ok: false,
    status: lastResponse?.status ?? 502,
    error: lastText.slice(0, ERROR_BODY_LIMIT),
  };
}

async function handleWebhook(request, env) {
  // --- configuration: fail closed, never silently accept everything ----------
  if (!env.WEBHOOK_SECRET) {
    console.error(
      'WEBHOOK_SECRET is not configured. Refusing every request. ' +
        'Set it with: wrangler secret put WEBHOOK_SECRET',
    );
    return json(500, { ok: false, error: 'worker misconfigured: WEBHOOK_SECRET is not set' });
  }
  if (!env.NOTION_TOKEN || !env.NOTION_DATABASE_ID) {
    console.error(
      'NOTION_TOKEN and/or NOTION_DATABASE_ID are not configured. ' +
        'Set them with: wrangler secret put NOTION_TOKEN / NOTION_DATABASE_ID',
    );
    return json(500, { ok: false, error: 'worker misconfigured: Notion credentials are not set' });
  }

  // --- body ------------------------------------------------------------------
  const rawBody = await request.text();
  let parsed;
  try {
    parsed = JSON.parse(rawBody);
  } catch {
    return json(400, {
      ok: false,
      error:
        'body is not valid JSON. TradingView sends the alert message verbatim, so this is ' +
        'almost always a malformed JSON template in the Pine alert message — check for an ' +
        'unbalanced quote or brace, or a placeholder that expanded to an empty value.',
    });
  }

  // --- auth: header first, then body field ----------------------------------
  // TradingView cannot set custom headers on every plan, so the body-secret path
  // is the practical one; the header path exists for curl / self-hosted relays.
  const headerSecret = request.headers.get('X-Webhook-Secret');
  const bodySecret = parsed && typeof parsed === 'object' ? parsed.secret : undefined;
  const presented = typeof headerSecret === 'string' && headerSecret.length > 0 ? headerSecret : bodySecret;

  if (!constantTimeEqual(presented, env.WEBHOOK_SECRET)) {
    console.warn('rejected webhook: secret mismatch');
    return json(401, { ok: false, error: 'unauthorized: bad or missing webhook secret' });
  }

  // --- contract --------------------------------------------------------------
  const result = validatePayload(parsed);
  if (!result.ok) {
    console.warn(`rejected webhook: invalid payload (${result.errors.join('; ')})`);
    return json(400, { ok: false, error: 'invalid payload', details: result.errors });
  }
  const payload = result.value;

  // Payload fields only — no secret, no token.
  console.log(
    `alert accepted: event=${payload.event} symbol=${payload.symbol} ` +
      `tf=${payload.timeframe} direction=${payload.direction} price=${payload.price} ` +
      `quality=${payload.quality ?? '-'} time=${payload.time}`,
  );

  const page = buildNotionPage(payload, env.NOTION_DATABASE_ID);
  const notion = await createNotionPage(page, env);

  if (!notion.ok) {
    console.error(`notion create failed: status=${notion.status} symbol=${payload.symbol}`);
    return json(502, {
      ok: false,
      error: 'notion request failed',
      notionStatus: notion.status,
      notionError: notion.error,
    });
  }

  console.log(`notion page created: id=${notion.pageId} symbol=${payload.symbol}`);
  return json(200, { ok: true, notionPageId: notion.pageId });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === 'GET' && url.pathname === '/health') {
      return json(200, { ok: true });
    }

    if (url.pathname === '/webhook' && request.method === 'POST') {
      return handleWebhook(request, env);
    }

    if (url.pathname === '/webhook') {
      return json(405, { ok: false, error: 'method not allowed: use POST /webhook' }, { Allow: 'POST' });
    }

    return json(405, {
      ok: false,
      error: 'not a route: use POST /webhook or GET /health',
    }, { Allow: 'POST, GET' });
  },
};
