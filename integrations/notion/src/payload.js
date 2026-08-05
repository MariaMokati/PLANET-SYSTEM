// =============================================================================
// PAYLOAD CONTRACT — the JSON that ATCS_strategy.pine emits via alert()
// -----------------------------------------------------------------------------
// This is the single source of truth for the alert contract. The Pine side
// (MODULE 17b in ATCS_strategy.pine) builds exactly this shape by hand, and the
// Worker refuses anything that does not match.
//
//   event      string   REQUIRED   "entry" | "exit" | "sweep" (free-form, but
//                                  those three are what the strategy emits)
//   secret     string   optional   shared secret; the Worker requires it here
//                                  OR in the X-Webhook-Secret header
//   symbol     string   REQUIRED   syminfo.tickerid, e.g. "OANDA:XAUUSD"
//   timeframe  string   REQUIRED   timeframe.period, e.g. "15"
//   direction  string   REQUIRED   "long" | "short" (case-insensitive in,
//                                  lowercase out)
//   price      number   REQUIRED   accepts numeric strings
//   entry      number   optional   accepts numeric strings
//   stop       number   optional   accepts numeric strings
//   target     number   optional   accepts numeric strings
//   quality    number   optional   LMX quality score 0-100
//   session    string   optional   "LONDON" | "NEW YORK" | "ASIA" | "OFF"
//   note       string   optional   human-readable reason string
//   time       string   optional   ISO-8601; stamped with receipt time if absent
//
// TradingView serialises everything it interpolates as text, so every numeric
// field must tolerate arriving as a string ("4512.5" not 4512.5).
// =============================================================================

/** Allowed `direction` values, after normalisation. */
export const DIRECTIONS = ['long', 'short'];

/** Required fields, for error messages and for the README table. */
export const REQUIRED_FIELDS = ['event', 'symbol', 'timeframe', 'direction', 'price'];

/** Optional numeric fields, in Notion display order. */
export const OPTIONAL_NUMBER_FIELDS = ['entry', 'stop', 'target', 'quality'];

/** Optional string fields, in Notion display order. */
export const OPTIONAL_STRING_FIELDS = ['session', 'note'];

/** Notion rich_text / title blocks reject anything longer than this. */
export const MAX_TEXT_LENGTH = 2000;

/**
 * Coerce a value to a finite number, accepting numeric strings.
 * Returns `undefined` when the value is absent, and `null` when it is present
 * but not coercible (so the caller can tell "missing" from "invalid").
 */
export function coerceNumber(value) {
  if (value === undefined || value === null || value === '') return undefined;
  if (typeof value === 'number') return Number.isFinite(value) ? value : null;
  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (trimmed === '') return undefined;
    // Number('') is 0 and Number(' ') is 0, hence the explicit empty checks.
    const n = Number(trimmed);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

/** Coerce to a trimmed, length-capped string. Empty means "absent". */
export function coerceString(value) {
  if (value === undefined || value === null) return undefined;
  const s = typeof value === 'string' ? value : String(value);
  const trimmed = s.trim();
  if (trimmed === '') return undefined;
  return trimmed.slice(0, MAX_TEXT_LENGTH);
}

/**
 * Validate and normalise a decoded alert body.
 *
 * @param {unknown} raw   the parsed JSON body
 * @param {{ now?: Date }} [options]
 * @returns {{ ok: true, value: object } | { ok: false, errors: string[] }}
 */
export function validatePayload(raw, options = {}) {
  const errors = [];

  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) {
    return { ok: false, errors: ['body must be a JSON object'] };
  }

  const value = {};

  // --- required strings ------------------------------------------------------
  for (const field of ['event', 'symbol', 'timeframe']) {
    const s = coerceString(raw[field]);
    if (s === undefined) {
      errors.push(`${field} is required`);
    } else {
      value[field] = s;
    }
  }

  // --- direction -------------------------------------------------------------
  const direction = coerceString(raw.direction);
  if (direction === undefined) {
    errors.push('direction is required');
  } else {
    const normalised = direction.toLowerCase();
    if (!DIRECTIONS.includes(normalised)) {
      errors.push(`direction must be one of ${DIRECTIONS.join(' | ')} (got "${direction}")`);
    } else {
      value.direction = normalised;
    }
  }

  // --- price (required number) ----------------------------------------------
  const price = coerceNumber(raw.price);
  if (price === undefined) {
    errors.push('price is required');
  } else if (price === null) {
    errors.push(`price must be a number (got "${raw.price}")`);
  } else {
    value.price = price;
  }

  // --- optional numbers ------------------------------------------------------
  for (const field of OPTIONAL_NUMBER_FIELDS) {
    const n = coerceNumber(raw[field]);
    if (n === null) {
      errors.push(`${field} must be a number when present (got "${raw[field]}")`);
    } else if (n !== undefined) {
      value[field] = n;
    }
  }

  // --- optional strings ------------------------------------------------------
  for (const field of OPTIONAL_STRING_FIELDS) {
    const s = coerceString(raw[field]);
    if (s !== undefined) value[field] = s;
  }

  // --- time ------------------------------------------------------------------
  const time = coerceString(raw.time);
  if (time === undefined) {
    // The strategy always sends one, but a hand-written alert might not.
    value.time = (options.now instanceof Date ? options.now : new Date()).toISOString();
    value.timeStamped = true;
  } else {
    const parsed = new Date(time);
    if (Number.isNaN(parsed.getTime())) {
      errors.push(`time must be an ISO-8601 timestamp when present (got "${time}")`);
    } else {
      value.time = parsed.toISOString();
      value.timeStamped = false;
    }
  }

  // NOTE: `secret` is deliberately NOT copied into `value`. It is an auth field,
  // it never reaches Notion, and it must never end up in a log line.

  if (errors.length > 0) return { ok: false, errors };
  return { ok: true, value };
}
