//@version=1
/* ============================================================================
 * YM 9:30 OPENING BREAK — "The Boring Scalp"
 * FXR Script custom indicator. YM / MYM only, 1-minute chart only.
 *
 *   1. YM only, 1-minute.
 *   2. Mark the levels at 9:29 ET, wicks not bodies.
 *   3. Buy stop above the high, sell stop below the low.
 *   4. Stop loss 10 ticks, fixed.
 *   5. Take profit 30 ticks, fixed 3:1.
 *   6. Enter ONLY on the 9:30 candle. Never 9:31.
 *   7. One trade a day, first break only.
 *
 * API arity matches the FXR starter template exactly:
 *   input.int / input.float ... 7 args (title, default, key, min, max, step, group)
 *   input.bool ............... 4 args (title, default, key, group)
 *   input.str ................ 6 args (title, default, key, options, tooltip, group)
 *   plot.line ................ 3 args (title, value, color)
 * Colours are color.* constants, not hex strings.
 * ========================================================================= */

const G_SETUP = 'Setup';
const G_RISK = 'Risk';
const G_SAFETY = 'Safety';

const MODE_CANDLE = '9:29 Candle High/Low';
const MODE_PIVOT = 'Swing Pivots (frozen at 9:29)';
const TIE_SKIP = 'Skip the day';
const TIE_DIRECTION = 'Follow candle direction';

const DAY_MS = 86400000;
const MAX_BARS = 1200;

/* Bar history as parallel number arrays. Seeded with a number then emptied so
 * they type as number[] rather than as an empty array of unknown element. */
const barH = [0];
const barL = [0];
barH.pop();
barL.pop();

let lastBarTime = 0;
let barIntervalMs = 0;
let pivotHigh = NaN;
let pivotLow = NaN;

let dayKey = 0;
let buyStop = NaN;
let sellStop = NaN;
let upperDead = false;
let lowerDead = false;
let levelsFrozen = false;
let position = ''; // '' | 'long' | 'short'
let entry = NaN;
let stopPx = NaN;
let targetPx = NaN;
let outcome = ''; // '' | 'TP' | 'SL'
let dayFinished = false;

/* NaN is the "unset" marker for every number above. */
const isSet = (x) => x === x;

/* ---------------------------------------------------------------------------
 * Calendar math on raw epoch milliseconds — no Date anywhere.
 * Hinnant's civil-date algorithms; exact, integer division only.
 * ------------------------------------------------------------------------ */
const floorDiv = (a, b) => Math.floor(a / b);

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

/* Eastern Time from the US DST rule: 2nd Sunday of March 07:00 UTC to
 * 1st Sunday of November 06:00 UTC. No timezone library, no host dependency. */
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
 * Host adapter — the only place the shape of FXR's data matters.
 * ------------------------------------------------------------------------ */
const asNumber = (v) => (typeof v === 'number' && v === v && v * 0 === 0 ? v : NaN);

const pick = (src, names) => {
  if (!src) return NaN;
  for (let i = 0; i < names.length; i++) {
    let v = src[names[i]];
    if (v === undefined || v === null) continue;
    if (typeof v === 'function') v = v(0);
    if (v && typeof v === 'object' && typeof v.length === 'number' && v.length > 0) {
      v = v[v.length - 1];
    }
    const n = asNumber(v);
    if (isSet(n)) return n;
  }
  return NaN;
};

const toEpochMs = (m) => {
  let v = m;
  if (v && typeof v === 'object') {
    if (typeof v.getTime === 'function') v = v.getTime();
    else if (typeof v.unix === 'function') v = v.unix() * 1000;
    else if (typeof v.valueOf === 'function') v = v.valueOf();
  }
  const n = asNumber(v);
  if (!isSet(n)) return NaN;
  return n < 1e12 ? n * 1000 : n;
};

let inO = NaN;
let inH = NaN;
let inL = NaN;
let inC = NaN;

const readBar = (series) => {
  let src = series;
  if (src && typeof src === 'object' && typeof src.length === 'number' && src.length > 0) {
    const last = src[src.length - 1];
    if (last && typeof last === 'object') src = last;
  }
  const h = pick(src, ['high', 'h', 'High']);
  const l = pick(src, ['low', 'l', 'Low']);
  if (!isSet(h) || !isSet(l)) return false;
  const c = pick(src, ['close', 'c', 'Close', 'closeC']);
  const o = pick(src, ['open', 'o', 'Open']);
  inH = h;
  inL = l;
  inC = isSet(c) ? c : h;
  inO = isSet(o) ? o : inC;
  return true;
};

/* ---------------------------------------------------------------------------
 * Pivots. Confirmed `len` bars late, so they never repaint. Strict on both
 * sides: an equal neighbour disqualifies, otherwise a flat stretch registers
 * every bar as a pivot and the real swing is lost to the newest chop.
 * ------------------------------------------------------------------------ */
const updatePivots = (len) => {
  const i = barH.length - 1 - len;
  if (i < len) return;
  let high = true;
  let low = true;
  for (let k = i - len; k <= i + len; k++) {
    if (k === i || k < 0 || k >= barH.length) continue;
    if (barH[k] >= barH[i]) high = false;
    if (barL[k] <= barL[i]) low = false;
  }
  if (high) pivotHigh = barH[i];
  if (low) pivotLow = barL[i];
};

const resetDay = (key) => {
  dayKey = key;
  buyStop = NaN;
  sellStop = NaN;
  upperDead = false;
  lowerDead = false;
  levelsFrozen = false;
  position = '';
  entry = NaN;
  stopPx = NaN;
  targetPx = NaN;
  outcome = '';
  dayFinished = false;
};

/* ------------------------------------------------------------------------ */
init = () => {
  indicator({ onMainPanel: true, format: 'inherit' });

  input.str(
    'Level source',
    MODE_CANDLE,
    'mode',
    [MODE_CANDLE, MODE_PIVOT],
    'Candle: the 9:29 bar wick high and wick low. Pivots: the last confirmed swing before 9:29.',
    G_SETUP
  );
  input.int('Pivot lookback', 5, 'pivotLen', 2, 50, 1, G_SETUP);
  input.str(
    'If both break on 9:30',
    TIE_SKIP,
    'tiePolicy',
    [TIE_SKIP, TIE_DIRECTION],
    'OHLC cannot say which side filled first inside one bar.',
    G_SETUP
  );

  input.float('Tick size', 1, 'tickSize', 0.01, 100, 0.01, G_RISK);
  input.int('Stop loss (ticks)', 10, 'slTicks', 1, 500, 1, G_RISK);
  input.int('Take profit (ticks)', 30, 'tpTicks', 1, 500, 1, G_RISK);
  input.int('Order offset (ticks)', 1, 'offsetTicks', 0, 50, 1, G_RISK);

  input.bool('Enforce 1-minute chart', true, 'enforce1m', G_SAFETY);
  input.bool('Show levels before the open', true, 'showPre', G_SAFETY);
};

/* ------------------------------------------------------------------------ */
onTick = (length, moment, series, ta, inputs) => {
  const draw = (a, b, c, d, e) => {
    plot.line('Buy Stop', a, color.green);
    plot.line('Sell Stop', b, color.red);
    plot.line('Entry', c, color.blue);
    plot.line('Stop Loss', d, color.red);
    plot.line('Take Profit', e, color.green);
  };

  const t = toEpochMs(moment);
  if (!isSet(t) || !readBar(series)) return draw(NaN, NaN, NaN, NaN, NaN);

  /* new bar, or the current one updating? */
  if (lastBarTime === 0 || t > lastBarTime) {
    if (lastBarTime !== 0) barIntervalMs = t - lastBarTime;
    barH.push(inH);
    barL.push(inL);
    if (barH.length > MAX_BARS) {
      barH.shift();
      barL.shift();
    }
    lastBarTime = t;
    if (inputs.mode === MODE_PIVOT) updatePivots(inputs.pivotLen);
  } else if (barH.length > 0) {
    const j = barH.length - 1;
    if (inH > barH[j]) barH[j] = inH;
    if (inL < barL[j]) barL[j] = inL;
  } else {
    return draw(NaN, NaN, NaN, NaN, NaN);
  }

  if (inputs.enforce1m && barIntervalMs !== 0 && barIntervalMs !== 60000) {
    return draw(NaN, NaN, NaN, NaN, NaN);
  }

  const et = etParts(t);
  if (et.key !== dayKey) resetDay(et.key);

  const j = barH.length - 1;
  const curH = barH[j];
  const curL = barL[j];

  const tick = inputs.tickSize;
  const offset = inputs.offsetTicks * tick;

  /* STEP 2 — mark the levels on the 9:29 bar */
  if (et.hh === 9 && et.mm === 29) {
    if (inputs.mode === MODE_CANDLE) {
      buyStop = curH + offset;
      sellStop = curL - offset;
      levelsFrozen = true;
    } else if (!levelsFrozen && isSet(pivotHigh) && isSet(pivotLow)) {
      buyStop = pivotHigh + offset;
      sellStop = pivotLow - offset;
      levelsFrozen = true;
    }
    /* STEP 6a — a 9:29 break kills that side. Pivots only; a candle cannot
     * break its own extremes. */
    if (levelsFrozen && inputs.mode === MODE_PIVOT) {
      if (curH >= buyStop) upperDead = true;
      if (curL <= sellStop) lowerDead = true;
    }
  }

  /* STEPS 3, 6, 7 — the 9:30 candle is the only trigger window */
  if (et.hh === 9 && et.mm === 30 && levelsFrozen && !dayFinished && position === '') {
    const hitLong = !upperDead && curH >= buyStop;
    const hitShort = !lowerDead && curL <= sellStop;

    let side = '';
    if (hitLong && hitShort) {
      if (inputs.tiePolicy === TIE_DIRECTION) side = inC >= inO ? 'long' : 'short';
    } else if (hitLong) {
      side = 'long';
    } else if (hitShort) {
      side = 'short';
    }

    if (side === 'long') {
      position = 'long';
      entry = buyStop;
      stopPx = entry - inputs.slTicks * tick;
      targetPx = entry + inputs.tpTicks * tick;
    } else if (side === 'short') {
      position = 'short';
      entry = sellStop;
      stopPx = entry + inputs.slTicks * tick;
      targetPx = entry - inputs.tpTicks * tick;
    }
    if (side === '' && (hitLong || hitShort)) dayFinished = true;
  }

  /* no break on 9:30 at all = no trade day */
  if (levelsFrozen && position === '' && !dayFinished) {
    if (et.hh > 9 || (et.hh === 9 && et.mm > 30)) dayFinished = true;
  }

  /* STEPS 4 & 5 — resolve. A bar spanning both is scored as the stop. */
  if (position !== '' && outcome === '') {
    const stopHit = position === 'long' ? curL <= stopPx : curH >= stopPx;
    const targetHit = position === 'long' ? curH >= targetPx : curL <= targetPx;
    if (stopHit) outcome = 'SL';
    else if (targetHit) outcome = 'TP';
    if (outcome !== '') dayFinished = true;
  }

  const resting = levelsFrozen && outcome === '' && position === '';
  const afterOpen = et.hh > 9 || (et.hh === 9 && et.mm >= 30);
  const showLevels = resting && (inputs.showPre || afterOpen);
  const inTrade = position !== '' && outcome === '';

  draw(
    showLevels && !upperDead ? buyStop : NaN,
    showLevels && !lowerDead ? sellStop : NaN,
    inTrade ? entry : NaN,
    inTrade ? stopPx : NaN,
    inTrade ? targetPx : NaN
  );
};
