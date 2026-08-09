//@version=1
/* ============================================================================
 * YM 9:30 OPENING BREAK — "The Boring Scalp"
 * FXR Script custom indicator for FX Replay.
 *
 * INSTRUMENT: YM / MYM ONLY. 1-minute chart ONLY.
 * Not tested on ES, NQ, or US30. The indicator refuses to draw on any other
 * timeframe (see "Enforce 1-minute chart").
 *
 * THE 7-STEP CHECKLIST THIS ENCODES
 *   1. YM only, 1-minute time frame.
 *   2. Mark the swings at 9:29 a.m. ET — wicks, not bodies.
 *   3. Stop orders at both levels: buy stop above the high, sell stop below
 *      the low.
 *   4. Stop loss: 10 ticks, fixed.
 *   5. Take profit: 30 ticks, fixed 3:1.
 *   6. Enter ONLY on the 9:30 candle. No break on 9:30 = no trade that day.
 *      Never on 9:31.
 *   7. One trade a day, first break only. Win or lose, you are done.
 *
 * PORTABILITY NOTES — this file deliberately avoids:
 *   Date / Date.UTC ...... all calendar work is integer math on epoch ms
 *   try / catch .......... no exception handling anywhere
 *   globalThis ........... nothing is read off the global object
 *   null reassignment .... every mutable holds ONE type for its whole life;
 *                          "unset" is NaN for numbers and '' for strings
 *   string slicing ....... the day key is a plain integer, e.g. 20260807
 *   arrays of objects .... bar history is five parallel number arrays
 * Every mutable is seeded with a value of its final type, so a type-checking
 * editor has nothing to complain about.
 * ========================================================================= */

/* ---------------------------------------------------------------------------
 * Input groups and option labels
 * ------------------------------------------------------------------------ */
const G_SETUP = 'Setup';
const G_RISK = 'Risk (ticks)';
const G_SAFETY = 'Safety';
const G_STYLE = 'Style';

const MODE_CANDLE = '9:29 Candle High/Low';
const MODE_PIVOT = 'Swing Pivots (frozen at 9:29)';

const TIE_SKIP = 'Skip the day (safest)';
const TIE_DIRECTION = 'Follow candle direction';

const DAY_MS = 86400000;
const MAX_BARS = 1500;

/* ---------------------------------------------------------------------------
 * Bar history — parallel number arrays, index 0 oldest, last = current bar
 * ------------------------------------------------------------------------ */
const barT = [];
const barO = [];
const barH = [];
const barL = [];
const barC = [];

let lastBarTime = 0; // 0 = nothing seen yet
let barIntervalMs = 0; // 0 = not yet inferred

let pivotHigh = NaN; // most recent CONFIRMED pivot high
let pivotLow = NaN;

let dayKey = 0; // ET calendar day as an integer, e.g. 20260807
let buyStop = NaN;
let sellStop = NaN;
let upperDead = false; // side killed before the open
let lowerDead = false;
let levelsFrozen = false;
let position = ''; // '' | 'long' | 'short'
let entry = NaN;
let stop = NaN;
let target = NaN;
let outcome = ''; // '' | 'TP' | 'SL'
let dayFinished = false; // one trade a day — first break only

/* NaN is the "unset" marker for every number above. */
const isSet = (x) => x === x;

/* ---------------------------------------------------------------------------
 * Calendar math on raw epoch milliseconds.
 *
 * Howard Hinnant's civil-date algorithms, which are exact for any proleptic
 * Gregorian date and need nothing but integer division.
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

/* Day 0 of the epoch is a Thursday, so shifting by 4 puts Sunday at 0. */
const nthSundayDays = (y, m, nth) => {
  const first = daysFromCivil(y, m, 1);
  const dow = ((first % 7) + 11) % 7;
  return first + ((7 - dow) % 7) + (nth - 1) * 7;
};

/* ---------------------------------------------------------------------------
 * Eastern Time without a timezone library.
 *
 * Chart data arrives in UTC. The strategy is defined on the New York wall
 * clock, so the 9:30 open sits on a different UTC hour in summer than in
 * winter. Rather than trust an ambient timezone, derive the offset from the
 * US DST rule in force since 2007:
 *   starts 2nd Sunday of March,    02:00 local standard  = 07:00 UTC
 *   ends   1st Sunday of November, 02:00 local daylight  = 06:00 UTC
 * ------------------------------------------------------------------------ */
const etOffsetMinutes = (utcMs) => {
  const y = civilFromDays(floorDiv(utcMs, DAY_MS)).y;
  const dstStart = nthSundayDays(y, 3, 2) * DAY_MS + 7 * 3600000;
  const dstEnd = nthSundayDays(y, 11, 1) * DAY_MS + 6 * 3600000;
  return utcMs >= dstStart && utcMs < dstEnd ? -240 : -300; // EDT : EST
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
 * FXR host adapter.
 *
 * The only two places this file touches the shape of the data FXR hands us.
 * They feature-detect rather than assume, so the indicator works whether the
 * host passes a candle object, an array of candles, or accessor functions.
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

/* Epoch milliseconds from whatever the host calls "now": a number in seconds
 * or milliseconds, a Date, or a moment-like object. */
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

/* Writes into the four scratch values below rather than allocating, so no
 * object shape has to be inferred anywhere. */
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
 * Pivots (mode: Swing Pivots). A pivot at index i needs `len` bars either
 * side, so it is only CONFIRMED `len` bars later — which is exactly why this
 * never repaints.
 *
 * The comparison is STRICT on both sides: a neighbour that merely equals the
 * candidate disqualifies it. Without that, a flat stretch of bars registers
 * every one of its members as a pivot and the real swing gets overwritten by
 * the most recent piece of chop.
 * ------------------------------------------------------------------------ */
const updatePivots = (len) => {
  const i = barH.length - 1 - len; // the bar that just became confirmable
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

/* ---------------------------------------------------------------------------
 * Day state
 * ------------------------------------------------------------------------ */
const resetDay = (key) => {
  dayKey = key;
  buyStop = NaN;
  sellStop = NaN;
  upperDead = false;
  lowerDead = false;
  levelsFrozen = false;
  position = '';
  entry = NaN;
  stop = NaN;
  target = NaN;
  outcome = '';
  dayFinished = false;
};

/* ---------------------------------------------------------------------------
 * init — inputs and panel placement
 * ------------------------------------------------------------------------ */
init = () => {
  indicator({ onMainPanel: true, format: 'inherit' });

  input.str(
    'Level source',
    MODE_CANDLE,
    'mode',
    [MODE_CANDLE, MODE_PIVOT],
    'Candle: the 9:29 bar own wick high and wick low. ' +
      'Pivots: the last confirmed swing high/low before 9:29, where a break ' +
      'during the 9:29 candle kills that side.',
    G_SETUP
  );

  input.int(
    'Pivot lookback',
    5,
    'pivotLen',
    2,
    50,
    1,
    'Bars either side of a swing point. Only used in Swing Pivots mode.',
    G_SETUP
  );

  input.str(
    'If both levels break on the 9:30 candle',
    TIE_SKIP,
    'tiePolicy',
    [TIE_SKIP, TIE_DIRECTION],
    'OHLC cannot tell us which side was hit first inside a single bar. ' +
      'Skip is honest. Direction assumes the close tells you where price went last.',
    G_SETUP
  );

  input.float(
    'Tick size',
    1,
    'tickSize',
    0.01,
    100,
    0.01,
    'YM and MYM tick 1.00 index point. Leave at 1 unless you know otherwise.',
    G_RISK
  );

  input.int('Stop loss (ticks)', 10, 'slTicks', 1, 500, 1, 'Fixed, non-negotiable.', G_RISK);
  input.int('Take profit (ticks)', 30, 'tpTicks', 1, 500, 1, 'Fixed 3:1 against a 10 tick stop.', G_RISK);
  input.int(
    'Order offset (ticks)',
    1,
    'offsetTicks',
    0,
    50,
    1,
    'How far ABOVE the high the buy stop sits, and below the low for the sell stop. Set 0 to rest exactly on the level.',
    G_RISK
  );

  input.bool('Enforce 1-minute chart', true, 'enforce1m', G_SAFETY);
  input.bool('Show levels before the open', true, 'showPre', G_SAFETY);

  input.color('Buy stop', '#26A69A', 'cBuy', G_STYLE);
  input.color('Sell stop', '#EF5350', 'cSell', G_STYLE);
  input.color('Entry', '#B39DDB', 'cEntry', G_STYLE);
  input.color('Stop loss', '#E53935', 'cStop', G_STYLE);
  input.color('Take profit', '#43A047', 'cTarget', G_STYLE);
};

/* ---------------------------------------------------------------------------
 * onTick — runs on every price update
 * ------------------------------------------------------------------------ */
onTick = (length, moment, series, ta, inputs) => {
  const draw = (a, b, c, d, e) => {
    plot.line('Buy Stop', a, inputs.cBuy);
    plot.line('Sell Stop', b, inputs.cSell);
    plot.line('Entry', c, inputs.cEntry);
    plot.line('Stop Loss', d, inputs.cStop);
    plot.line('Take Profit', e, inputs.cTarget);
  };

  const t = toEpochMs(moment);
  if (!isSet(t) || !readBar(series)) return draw(NaN, NaN, NaN, NaN, NaN);

  /* ---- bar bookkeeping: a new bar, or the current one updating? ---------- */
  if (lastBarTime === 0 || t > lastBarTime) {
    if (lastBarTime !== 0) barIntervalMs = t - lastBarTime;
    barT.push(t);
    barO.push(inO);
    barH.push(inH);
    barL.push(inL);
    barC.push(inC);
    if (barT.length > MAX_BARS) {
      barT.shift();
      barO.shift();
      barH.shift();
      barL.shift();
      barC.shift();
    }
    lastBarTime = t;
    if (inputs.mode === MODE_PIVOT) updatePivots(inputs.pivotLen);
  } else if (barT.length > 0) {
    const j = barT.length - 1;
    if (inH > barH[j]) barH[j] = inH;
    if (inL < barL[j]) barL[j] = inL;
    barC[j] = inC;
  } else {
    return draw(NaN, NaN, NaN, NaN, NaN);
  }

  /* ---- refuse to signal on the wrong timeframe -------------------------- */
  if (inputs.enforce1m && barIntervalMs !== 0 && barIntervalMs !== 60000) {
    return draw(NaN, NaN, NaN, NaN, NaN);
  }

  const et = etParts(t);
  if (et.key !== dayKey) resetDay(et.key);

  const j = barT.length - 1;
  const curO = barO[j];
  const curH = barH[j];
  const curL = barL[j];
  const curC = barC[j];

  const tick = inputs.tickSize;
  const offset = inputs.offsetTicks * tick;
  const isSetupBar = et.hh === 9 && et.mm === 29;
  const isTriggerBar = et.hh === 9 && et.mm === 30;

  /* ---- STEP 2: mark the levels ------------------------------------------ */
  if (isSetupBar) {
    if (inputs.mode === MODE_CANDLE) {
      /* The 9:29 candle's own wicks. Updates live as that bar forms and is
       * final the moment it closes — which is when the orders go in. */
      buyStop = curH + offset;
      sellStop = curL - offset;
      levelsFrozen = true;
    } else if (!levelsFrozen && isSet(pivotHigh) && isSet(pivotLow)) {
      /* Freeze the last swing pivots CONFIRMED before 9:29 opened. */
      buyStop = pivotHigh + offset;
      sellStop = pivotLow - offset;
      levelsFrozen = true;
    }

    /* STEP 6, first half: a break during the 9:29 candle kills that side.
     * Only meaningful for pivots — a candle cannot break its own extremes. */
    if (levelsFrozen && inputs.mode === MODE_PIVOT) {
      if (curH >= buyStop) upperDead = true;
      if (curL <= sellStop) lowerDead = true;
    }
  }

  /* ---- STEPS 3, 6, 7: the 9:30 candle is the only trigger window -------- */
  if (isTriggerBar && levelsFrozen && !dayFinished && position === '') {
    const hitLong = !upperDead && curH >= buyStop;
    const hitShort = !lowerDead && curL <= sellStop;

    let side = '';
    if (hitLong && hitShort) {
      /* Both stops filled inside one bar. OHLC does not record the order of
       * events, so we either stand down or infer from where it closed. */
      if (inputs.tiePolicy === TIE_DIRECTION) side = curC >= curO ? 'long' : 'short';
    } else if (hitLong) {
      side = 'long';
    } else if (hitShort) {
      side = 'short';
    }

    if (side === 'long') {
      position = 'long';
      entry = buyStop;
      stop = entry - inputs.slTicks * tick;
      target = entry + inputs.tpTicks * tick;
    } else if (side === 'short') {
      position = 'short';
      entry = sellStop;
      stop = entry + inputs.slTicks * tick;
      target = entry - inputs.tpTicks * tick;
    }

    /* Whatever happened, the window is now shut. Never enter on 9:31. */
    if (side === '' && (hitLong || hitShort)) dayFinished = true;
  }

  /* No break on the 9:30 candle at all = no trade day. */
  if (levelsFrozen && position === '' && !dayFinished) {
    if (et.hh > 9 || (et.hh === 9 && et.mm > 30)) dayFinished = true;
  }

  /* ---- STEPS 4 & 5: resolve the trade ----------------------------------- */
  if (position !== '' && outcome === '') {
    const stopHit = position === 'long' ? curL <= stop : curH >= stop;
    const targetHit = position === 'long' ? curH >= target : curL <= target;
    /* If a single bar spans both, we assume the stop went first. Fixed 3:1
     * systems flatter themselves when scored the other way. */
    if (stopHit) outcome = 'SL';
    else if (targetHit) outcome = 'TP';
    if (outcome !== '') dayFinished = true;
  }

  /* ---- render ----------------------------------------------------------- */
  const resting = levelsFrozen && outcome === '' && position === '';
  const afterOpen = et.hh > 9 || (et.hh === 9 && et.mm >= 30);
  const showLevels = resting && (inputs.showPre || afterOpen);
  const inTrade = position !== '' && outcome === '';

  draw(
    showLevels && !upperDead ? buyStop : NaN,
    showLevels && !lowerDead ? sellStop : NaN,
    inTrade ? entry : NaN,
    inTrade ? stop : NaN,
    inTrade ? target : NaN
  );
};
