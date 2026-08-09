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
 * WHAT IT DRAWS (all via plot.line, so it reads cleanly in the legend):
 *   Buy Stop / Sell Stop  — the two orders, live from 9:29 until resolved
 *   Entry / Stop / Target — only after a break triggers
 *
 * HONESTY: this is a drawing + bookkeeping tool. It does not place orders and
 * it makes no profitability claim. Two intrabar ambiguities are unknowable
 * from OHLC alone and are resolved conservatively — both are documented at
 * their decision points below and one is exposed as an input.
 * ========================================================================= */

/* ---------------------------------------------------------------------------
 * Input groups
 * ------------------------------------------------------------------------ */
const G_SETUP = 'Setup';
const G_RISK = 'Risk (ticks)';
const G_SAFETY = 'Safety';
const G_STYLE = 'Style';

const MODE_CANDLE = '9:29 Candle High/Low';
const MODE_PIVOT = 'Swing Pivots (frozen at 9:29)';

const TIE_SKIP = 'Skip the day (safest)';
const TIE_DIRECTION = 'Follow candle direction';

/* Bars we keep in memory. Enough for any pivot lookback, bounded so a long
 * replay session cannot grow without limit. */
const MAX_BARS = 1500;

/* ---------------------------------------------------------------------------
 * Persistent state (survives across ticks)
 * ------------------------------------------------------------------------ */
const bars = []; // [{ t, o, h, l, c }] — index 0 oldest, last = current bar
let lastBarTime = null; // epoch ms of the bar currently being built
let barIntervalMs = null; // inferred from the two most recent bar opens

let pivotHigh = null; // most recent CONFIRMED pivot high (mode: pivots)
let pivotLow = null; // most recent CONFIRMED pivot low

let dayKey = null; // ET calendar day, e.g. "2026-08-07"
let buyStop = null; // resting buy-stop price
let sellStop = null; // resting sell-stop price
let upperDead = false; // that side was killed before the open
let lowerDead = false;
let levelsFrozen = false; // levels locked in for the day
let position = null; // 'long' | 'short' | null
let entry = null;
let stop = null;
let target = null;
let outcome = null; // 'TP' | 'SL' | null
let dayFinished = false; // one trade a day — first break only

/* ---------------------------------------------------------------------------
 * Eastern Time without a timezone library.
 *
 * Chart data arrives in UTC. The strategy is defined on the New York wall
 * clock, which means the 9:30 open lands on a different UTC hour in summer
 * than in winter. Rather than trust an ambient timezone, we derive the offset
 * from the US DST rule in force since 2007:
 *   starts 2nd Sunday of March,   02:00 local standard  = 07:00 UTC
 *   ends   1st Sunday of November, 02:00 local daylight = 06:00 UTC
 * ------------------------------------------------------------------------ */
const nthSundayUtc = (year, monthIdx, nth) => {
  const first = Date.UTC(year, monthIdx, 1);
  const dow = new Date(first).getUTCDay(); // 0 = Sunday
  const day = 1 + ((7 - dow) % 7) + (nth - 1) * 7;
  return Date.UTC(year, monthIdx, day);
};

const etOffsetMinutes = (utcMs) => {
  const y = new Date(utcMs).getUTCFullYear();
  const dstStart = nthSundayUtc(y, 2, 2) + 7 * 3600000; // March, 07:00 UTC
  const dstEnd = nthSundayUtc(y, 10, 1) + 6 * 3600000; // November, 06:00 UTC
  return utcMs >= dstStart && utcMs < dstEnd ? -240 : -300; // EDT : EST
};

const etParts = (utcMs) => {
  const d = new Date(utcMs + etOffsetMinutes(utcMs) * 60000);
  return {
    key:
      d.getUTCFullYear() +
      '-' +
      ('0' + (d.getUTCMonth() + 1)).slice(-2) +
      '-' +
      ('0' + d.getUTCDate()).slice(-2),
    hh: d.getUTCHours(),
    mm: d.getUTCMinutes(),
  };
};

/* ---------------------------------------------------------------------------
 * FXR host adapter.
 *
 * These two functions are the ONLY places this file touches the shape of the
 * data FXR hands us. They feature-detect rather than assume, so the indicator
 * works whether the host passes a candle object, a series object of
 * arrays/accessors, an array of candles, or exposes globals.
 * ------------------------------------------------------------------------ */
const asNumber = (v) => (typeof v === 'number' && isFinite(v) ? v : null);

/* Epoch milliseconds from whatever the host calls "now": a number (seconds or
 * milliseconds), a Date, or a moment-like object. */
const toEpochMs = (m) => {
  if (m == null) return null;
  const direct = asNumber(m);
  if (direct !== null) return direct < 1e12 ? direct * 1000 : direct;
  if (typeof m.valueOf === 'function') {
    const v = asNumber(m.valueOf());
    if (v !== null) return v < 1e12 ? v * 1000 : v;
  }
  if (typeof m.getTime === 'function') return asNumber(m.getTime());
  if (typeof m.unix === 'function') {
    const u = asNumber(m.unix());
    if (u !== null) return u * 1000;
  }
  return null;
};

/* Pull one OHLC field off a candidate source, trying the shapes the host
 * might use: plain number, zero-arg accessor, or array (newest last). */
const pluck = (src, names) => {
  if (!src || typeof src !== 'object') return null;
  for (let i = 0; i < names.length; i++) {
    const v = src[names[i]];
    if (v == null) continue;
    const plain = asNumber(v);
    if (plain !== null) return plain;
    if (typeof v === 'function') {
      try {
        const r = asNumber(v.call(src, 0));
        if (r !== null) return r;
      } catch (e) {
        /* accessor needs different args — fall through */
      }
    }
    if (Array.isArray(v) && v.length) {
      const a = asNumber(v[v.length - 1]);
      if (a !== null) return a;
    }
  }
  return null;
};

const readBar = (series) => {
  const candidates = [series];
  if (Array.isArray(series) && series.length) {
    candidates.unshift(series[series.length - 1]);
  }
  if (typeof globalThis !== 'undefined') candidates.push(globalThis);

  for (let i = 0; i < candidates.length; i++) {
    const s = candidates[i];
    const h = pluck(s, ['high', 'h', 'High']);
    const l = pluck(s, ['low', 'l', 'Low']);
    if (h === null || l === null) continue;
    const c = pluck(s, ['close', 'c', 'Close', 'closeC']);
    const o = pluck(s, ['open', 'o', 'Open']);
    return { o: o !== null ? o : c !== null ? c : h, h: h, l: l, c: c !== null ? c : h };
  }
  return null;
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
const isPivotHigh = (i, len) => {
  const h = bars[i].h;
  for (let k = i - len; k <= i + len; k++) {
    if (k === i || k < 0 || k >= bars.length) continue;
    if (bars[k].h >= h) return false;
  }
  return true;
};

const isPivotLow = (i, len) => {
  const l = bars[i].l;
  for (let k = i - len; k <= i + len; k++) {
    if (k === i || k < 0 || k >= bars.length) continue;
    if (bars[k].l <= l) return false;
  }
  return true;
};

const updatePivots = (len) => {
  const i = bars.length - 1 - len; // the bar that just became confirmable
  if (i < len) return;
  if (isPivotHigh(i, len)) pivotHigh = bars[i].h;
  if (isPivotLow(i, len)) pivotLow = bars[i].l;
};

/* ---------------------------------------------------------------------------
 * Day state
 * ------------------------------------------------------------------------ */
const resetDay = (key) => {
  dayKey = key;
  buyStop = null;
  sellStop = null;
  upperDead = false;
  lowerDead = false;
  levelsFrozen = false;
  position = null;
  entry = null;
  stop = null;
  target = null;
  outcome = null;
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
    'Candle: the 9:29 bar\'s own wick high and wick low. ' +
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
  const blank = () => {
    plot.line('Buy Stop', null, inputs.cBuy);
    plot.line('Sell Stop', null, inputs.cSell);
    plot.line('Entry', null, inputs.cEntry);
    plot.line('Stop Loss', null, inputs.cStop);
    plot.line('Take Profit', null, inputs.cTarget);
  };

  const t = toEpochMs(moment);
  const bar = readBar(series);
  if (t === null || !bar) return blank();

  /* ---- bar bookkeeping: is this a new bar, or the current one updating? -- */
  if (lastBarTime === null || t > lastBarTime) {
    if (lastBarTime !== null) barIntervalMs = t - lastBarTime;
    bars.push({ t: t, o: bar.o, h: bar.h, l: bar.l, c: bar.c });
    if (bars.length > MAX_BARS) bars.shift();
    lastBarTime = t;
    if (inputs.mode === MODE_PIVOT) updatePivots(inputs.pivotLen);
  } else if (bars.length) {
    const cur = bars[bars.length - 1];
    cur.h = Math.max(cur.h, bar.h);
    cur.l = Math.min(cur.l, bar.l);
    cur.c = bar.c;
  } else {
    return blank();
  }

  /* ---- refuse to signal on the wrong timeframe -------------------------- */
  if (inputs.enforce1m && barIntervalMs !== null && barIntervalMs !== 60000) {
    return blank();
  }

  const et = etParts(t);
  if (et.key !== dayKey) resetDay(et.key);

  const cur = bars[bars.length - 1];
  const tick = inputs.tickSize;
  const offset = inputs.offsetTicks * tick;
  const isSetupBar = et.hh === 9 && et.mm === 29;
  const isTriggerBar = et.hh === 9 && et.mm === 30;

  /* ---- STEP 2: mark the levels ------------------------------------------ */
  if (isSetupBar) {
    if (inputs.mode === MODE_CANDLE) {
      /* The 9:29 candle's own wicks. It updates live as that bar forms and is
       * final the moment the bar closes — which is when the orders go in. */
      buyStop = cur.h + offset;
      sellStop = cur.l - offset;
      levelsFrozen = true;
    } else if (!levelsFrozen) {
      /* Freeze the last swing pivots CONFIRMED before 9:29 opened. */
      if (pivotHigh !== null && pivotLow !== null) {
        buyStop = pivotHigh + offset;
        sellStop = pivotLow - offset;
        levelsFrozen = true;
      }
    }

    /* STEP 6, first half: a break during the 9:29 candle kills that side.
     * Only meaningful for pivots — a candle cannot break its own extremes. */
    if (levelsFrozen && inputs.mode === MODE_PIVOT) {
      if (cur.h >= buyStop) upperDead = true;
      if (cur.l <= sellStop) lowerDead = true;
    }
  }

  /* ---- STEPS 3, 6, 7: the 9:30 candle is the only trigger window -------- */
  if (isTriggerBar && levelsFrozen && !dayFinished && position === null) {
    const hitLong = !upperDead && cur.h >= buyStop;
    const hitShort = !lowerDead && cur.l <= sellStop;

    let side = null;
    if (hitLong && hitShort) {
      /* Both stops filled inside one bar. OHLC does not record the order of
       * events, so we either stand down or infer from where it closed. */
      if (inputs.tiePolicy === TIE_DIRECTION) side = cur.c >= cur.o ? 'long' : 'short';
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
    if (side === null && (hitLong || hitShort)) dayFinished = true;
  }

  /* No break on the 9:30 candle at all = no trade day. */
  if (levelsFrozen && position === null && !dayFinished) {
    if (et.hh > 9 || (et.hh === 9 && et.mm > 30)) dayFinished = true;
  }

  /* ---- STEPS 4 & 5: resolve the trade ----------------------------------- */
  if (position !== null && outcome === null) {
    const stopHit = position === 'long' ? cur.l <= stop : cur.h >= stop;
    const targetHit = position === 'long' ? cur.h >= target : cur.l <= target;
    /* If a single bar spans both, we assume the stop went first. Fixed 3:1
     * systems flatter themselves when scored the other way. */
    if (stopHit) outcome = 'SL';
    else if (targetHit) outcome = 'TP';
    if (outcome !== null) dayFinished = true;
  }

  /* ---- render ----------------------------------------------------------- */
  const live = levelsFrozen && outcome === null;
  const showLevels =
    live && (inputs.showPre || et.hh > 9 || (et.hh === 9 && et.mm >= 30));

  plot.line('Buy Stop', showLevels && !upperDead && position === null ? buyStop : null, inputs.cBuy);
  plot.line('Sell Stop', showLevels && !lowerDead && position === null ? sellStop : null, inputs.cSell);
  plot.line('Entry', position !== null && outcome === null ? entry : null, inputs.cEntry);
  plot.line('Stop Loss', position !== null && outcome === null ? stop : null, inputs.cStop);
  plot.line('Take Profit', position !== null && outcome === null ? target : null, inputs.cTarget);
};
