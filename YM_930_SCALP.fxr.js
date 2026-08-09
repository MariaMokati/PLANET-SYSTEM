//@version=1
/* ============================================================================
 * YM 9:30 OPENING BREAK — minimal baseline
 *
 * YM / MYM only. 1-minute chart only.
 *   Levels  : the 9:29 ET candle's wick high and wick low
 *   Orders  : buy stop 1 tick above the high, sell stop 1 tick below the low
 *   Stop    : 10 ticks     Target: 30 ticks (3:1)
 *   Window  : the 9:30 candle only. Never 9:31.
 *   Limit   : one trade a day, first break only.
 *
 * Deliberately minimal API surface — indicator(), plot.line(), and reading the
 * bar. No inputs, no arrays, no top-level statements other than declarations.
 * Settings are the constants directly below.
 * ========================================================================= */

const TICK = 1; // YM and MYM tick 1.00 index point
const SL_TICKS = 10;
const TP_TICKS = 30;
const OFFSET_TICKS = 1; // how far beyond the level the stop order rests
const ENFORCE_1M = true; // draw nothing on any timeframe but 1-minute
const SHOW_BEFORE_OPEN = true; // show the levels during the 9:29 candle

const DAY_MS = 86400000;

let lastBarTime = 0;
let barIntervalMs = 0;
let curH = NaN;
let curL = NaN;

let dayKey = 0;
let buyStop = NaN;
let sellStop = NaN;
let armed = false;
let position = ''; // '' | 'long' | 'short'
let entry = NaN;
let stopPx = NaN;
let targetPx = NaN;
let outcome = ''; // '' | 'TP' | 'SL'
let done = false;

/* NaN is the "unset" marker for every number above. */
const isSet = (x) => x === x;
const num = (v) => (typeof v === 'number' && v === v && v * 0 === 0 ? v : NaN);
const floorDiv = (a, b) => Math.floor(a / b);

/* ---------------------------------------------------------------------------
 * Calendar math straight off epoch milliseconds — no Date, no timezone data.
 * Hinnant's civil-date algorithms; integer division only.
 * ------------------------------------------------------------------------ */
const civilFromDays = (z0) => {
  const z = z0 + 719468;
  const era = floorDiv(z, 146097);
  const doe = z - era * 146097;
  const yoe = floorDiv(
    doe - floorDiv(doe, 1460) + floorDiv(doe, 36524) - floorDiv(doe, 146096),
    365
  );
  const doy = doe - (365 * yoe + floorDiv(yoe, 4) - floorDiv(yoe, 100));
  const mp = floorDiv(5 * doy + 2, 153);
  const d = doy - floorDiv(153 * mp + 2, 5) + 1;
  const m = mp < 10 ? mp + 3 : mp - 9;
  return { y: yoe + era * 400 + (m <= 2 ? 1 : 0), m: m, d: d };
};

const daysFromCivil = (y0, m, d) => {
  const y = y0 - (m <= 2 ? 1 : 0);
  const era = floorDiv(y, 400);
  const yoe = y - era * 400;
  const mp = m > 2 ? m - 3 : m + 9;
  const doy = floorDiv(153 * mp + 2, 5) + d - 1;
  const doe = yoe * 365 + floorDiv(yoe, 4) - floorDiv(yoe, 100) + doy;
  return era * 146097 + doe - 719468;
};

/* Epoch day 0 is a Thursday, so +4 puts Sunday at 0. */
const nthSundayDays = (y, m, nth) => {
  const first = daysFromCivil(y, m, 1);
  const dow = ((first % 7) + 11) % 7;
  return first + ((7 - dow) % 7) + (nth - 1) * 7;
};

/* US DST since 2007: 2nd Sunday of March 07:00 UTC to 1st Sunday of
 * November 06:00 UTC. The 9:30 open is a New York wall-clock event, so it
 * sits on a different UTC hour in summer than in winter. */
const etOffsetMinutes = (utcMs) => {
  const y = civilFromDays(floorDiv(utcMs, DAY_MS)).y;
  const dstStart = nthSundayDays(y, 3, 2) * DAY_MS + 7 * 3600000;
  const dstEnd = nthSundayDays(y, 11, 1) * DAY_MS + 6 * 3600000;
  return utcMs >= dstStart && utcMs < dstEnd ? -240 : -300;
};

const etParts = (utcMs) => {
  const local = utcMs + etOffsetMinutes(utcMs) * 60000;
  const days = floorDiv(local, DAY_MS);
  const msOfDay = local - days * DAY_MS;
  const c = civilFromDays(days);
  return {
    key: c.y * 10000 + c.m * 100 + c.d,
    hh: floorDiv(msOfDay, 3600000),
    mm: floorDiv(msOfDay, 60000) % 60,
  };
};

/* ---------------------------------------------------------------------------
 * Host adapter
 * ------------------------------------------------------------------------ */
/* NOTE: never write `typeof someObject.member` anywhere in this file. FXR's
 * engine does not evaluate typeof on a property access correctly — it reported
 * a plain object's missing .getTime as 'function' and then threw calling it.
 * Detect shapes by coercion and truthiness instead. */

const field = (src, a, b) => {
  if (!src) return NaN;
  let v = src[a];
  if (v === undefined || v === null) v = src[b];
  const direct = num(v);
  if (isSet(direct)) return direct;
  if (v && v.call) return num(v(0)); // accessor form, e.g. high(0)
  return NaN;
};

/* Epoch milliseconds from a number (seconds or ms), a Date, or a moment-like
 * object. Multiplying by 1 runs valueOf on all three without naming a method. */
const stamp = (m) => {
  let n = num(m);
  if (!isSet(n)) n = num(m * 1);
  if (!isSet(n)) return NaN;
  return n < 1e12 ? n * 1000 : n;
};

/* ------------------------------------------------------------------------ */
init = () => {
  indicator({ onMainPanel: true, format: 'inherit' });
};

/* ------------------------------------------------------------------------ */
onTick = (length, moment, series, ta, inputs) => {
  const draw = (a, b, c, d, e) => {
    plot.line('Buy Stop', a, color.green);
    plot.line('Sell Stop', b, color.red);
    plot.line('Entry', c, color.green);
    plot.line('Stop Loss', d, color.red);
    plot.line('Take Profit', e, color.green);
  };

  const t = stamp(moment);
  if (!isSet(t)) return draw(NaN, NaN, NaN, NaN, NaN);

  let src = series;
  if (src && src.length > 0 && src[src.length - 1]) src = src[src.length - 1];
  const h = field(src, 'high', 'h');
  const l = field(src, 'low', 'l');
  if (!isSet(h) || !isSet(l)) return draw(NaN, NaN, NaN, NaN, NaN);

  /* new bar, or the current one still forming? */
  if (lastBarTime === 0 || t > lastBarTime) {
    if (lastBarTime !== 0) barIntervalMs = t - lastBarTime;
    lastBarTime = t;
    curH = h;
    curL = l;
  } else {
    if (h > curH) curH = h;
    if (l < curL) curL = l;
  }

  if (ENFORCE_1M && barIntervalMs !== 0 && barIntervalMs !== 60000) {
    return draw(NaN, NaN, NaN, NaN, NaN);
  }

  const et = etParts(t);
  if (et.key !== dayKey) {
    dayKey = et.key;
    buyStop = NaN;
    sellStop = NaN;
    armed = false;
    position = '';
    entry = NaN;
    stopPx = NaN;
    targetPx = NaN;
    outcome = '';
    done = false;
  }

  const offset = OFFSET_TICKS * TICK;

  /* mark the levels on the 9:29 candle */
  if (et.hh === 9 && et.mm === 29) {
    buyStop = curH + offset;
    sellStop = curL - offset;
    armed = true;
  }

  /* the 9:30 candle is the only trigger window */
  if (et.hh === 9 && et.mm === 30 && armed && !done && position === '') {
    const hitLong = curH >= buyStop;
    const hitShort = curL <= sellStop;

    if (hitLong && hitShort) {
      /* both stops filled inside one bar and OHLC cannot say which was
       * first, so stand down for the day */
      done = true;
    } else if (hitLong) {
      position = 'long';
      entry = buyStop;
      stopPx = entry - SL_TICKS * TICK;
      targetPx = entry + TP_TICKS * TICK;
    } else if (hitShort) {
      position = 'short';
      entry = sellStop;
      stopPx = entry + SL_TICKS * TICK;
      targetPx = entry - TP_TICKS * TICK;
    }
  }

  /* no break on the 9:30 candle at all = no trade day */
  if (armed && position === '' && !done) {
    if (et.hh > 9 || (et.hh === 9 && et.mm > 30)) done = true;
  }

  /* resolve. A bar spanning both is scored as the stop. */
  if (position !== '' && outcome === '') {
    const stopHit = position === 'long' ? curL <= stopPx : curH >= stopPx;
    const targetHit = position === 'long' ? curH >= targetPx : curL <= targetPx;
    if (stopHit) outcome = 'SL';
    else if (targetHit) outcome = 'TP';
    if (outcome !== '') done = true;
  }

  const resting = armed && outcome === '' && position === '';
  const afterOpen = et.hh > 9 || (et.hh === 9 && et.mm >= 30);
  const show = resting && (SHOW_BEFORE_OPEN || afterOpen);
  const inTrade = position !== '' && outcome === '';

  draw(
    show ? buyStop : NaN,
    show ? sellStop : NaN,
    inTrade ? entry : NaN,
    inTrade ? stopPx : NaN,
    inTrade ? targetPx : NaN
  );
};
