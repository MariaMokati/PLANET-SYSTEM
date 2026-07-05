# Liquidity Draw & Displacement System (LDX) v2

A serious, price-action / order-flow **Pine Script v6 strategy** for TradingView.
The edge is **not** an indicator — it is the institutional footprint:

> **liquidity rests → it gets raided (stop hunt) → price displaces away leaving an
> imbalance (FVG) → market structure shifts (MSS) → you enter on the retrace into
> the FVG → stop beyond the raid → target the next draw on liquidity.**

Moving averages appear **only** as an optional higher-timeframe background bias.
They never generate a signal.

> **Honest disclaimer.** No win rate is promised or fabricated. The numbers you
> care about (win %, profit factor, drawdown) only become real when *you* run the
> Strategy Tester and forward-test on demo. This is a research tool, not financial
> advice. **I could not compile it in a live TradingView environment from here — it
> is hand-audited against the v6 spec. Paste it into the Pine Editor; if the
> compiler flags anything, send me the exact text and I'll fix it immediately.**

File: [`ATCS_strategy.pine`](./ATCS_strategy.pine) *(kept as the repo's script filename)*

---

## 1. The core question it answers
*"Where are stops resting, which pool is price drawing toward, and where is the
highest-probability entry **after** liquidity is taken?"*

## 2. The Draw-on-Liquidity engine
**Buy-side liquidity (above):** previous day high (PDH), previous week high (PWH),
frozen Asia-session high, latest major swing high, and **equal highs** (a swing
within a tolerance of the prior swing = a clean stop pool).
**Sell-side liquidity (below):** PDL, PWL, Asia low, latest major swing low, **equal
lows**.
**Nearest BSL / SSL** and the implied **draw direction** (which pool is closer) are
computed every bar and shown on the dashboard. Levels are **plotted only until they
are taken**, then they disappear — keeping the chart clean and showing exactly what
liquidity has already been consumed today.

**Liquidity taken** is classified as: *wick sweep + close back inside* (a failed
sweep / raid — the tradable event) vs *full-body break* (continuation, marks the
pool "taken").

## 3. The exact trade sequence (built into the state machine)
1. **Dealing range** = highest high / lowest low over the lookback.
2. **Premium / discount / equilibrium** = position vs the range midpoint.
3. **External liquidity targets** = PDH/PDL, PWH/PWL, Asia H/L, major swings.
4. **Internal liquidity** = minor swing highs/lows (short-term stop pools).
5. **Raid** — price sweeps a pool and closes back inside (`longRaidActive`).
6. **Displacement** — a Fair Value Gap larger than `minFvg × ATR` forms in the
   reversal direction (`longDispSeen`, stores the FVG for entry).
7. **MSS** — price breaks the most recent internal swing (the trigger).
8. **Entry** — limit order into the FVG (better RR) or market on the MSS close.
9. **Stop** — beyond the raid extreme + an ATR buffer.
10. **Target** — the **next draw on liquidity** in the trade direction.
11. **RR gate** — the signal is rejected unless that draw is **≥ 2R** away.

## 4. No duplicate signals / only A+ (and optional B)
- A raid is **consumed** the moment it produces a signal, so the same move cannot
  fire twice.
- MSS is a one-bar cross, and a **cooldown** plus **one-position** rule prevent
  clustering.
- Every candidate gets a **0–100 quality score**: MSS (20) + external-vs-internal
  raid (10/20) + displacement strength (≤25) + premium/discount alignment (5/15) +
  HTF bias (0/10) + RR bonus (5/7/10).
- **A+ ≥ 75**, **B ≥ 55** (both configurable). Turn B off to trade only A+.

## 5. Signal labels explain themselves
Example printed on the chart and sent to alerts:
> `A+ 82/100 — LONG: sell-side liquidity swept + bullish displacement + MSS → draw
> = buy-side @ 4512.50`

## 6. Sessions
Asia range (the London draw), London killzone, New York killzone, PDH/PDL, PWH/PWL,
frozen session highs/lows. Optional **"only trade inside killzones"** switch. The
dashboard shows the live session.

## 7. Risk controls
- **Min 1:2 RR** enforced *before* a signal is allowed.
- **Max trades per day** (default 3).
- **Daily loss cap** (% of day-start equity) → blocks new entries once hit.
- **Cooldown** after each trade.
- **TP1 (1R, 50% off) → stop to breakeven → TP2 = the liquidity draw → optional TP3
  runner (3R).**
- Invalidation: unfilled limit entries are cancelled after N bars or if the raid
  low/high is violated.

## 8. Dashboard (top-right)
Structure (bull/bear/ranging) · draw on liquidity · nearest BSL · nearest SSL ·
liquidity taken today · dealing range · premium/discount · session · **setup
quality score & tier** · **expected RR to draw** · position · **trades today /
max (+ loss-cap flag)** · win rate.

---

## How to use it
1. Pine Editor → paste `ATCS_strategy.pine` → **Add to chart**.
2. Use **15m or 1H** on indices or gold (see below).
3. Read the dashboard. Wait for a **LONG/SHORT arrow + label** — it prints only on a
   closed bar and is never redrawn.
4. The strategy places the entry, stop, TP1/TP2/TP3 and manages breakeven for you.
5. Alerts: **Create Alert → LDX v2 → LDX Long/Short/Any Setup → "Once per bar
   close"**. Add a second **"Order fills only"** alert for exact fills.

## How to backtest it
1. Open **Strategy Tester**. Keep realistic **commission (0.02%) and slippage (1
   tick)** — already set.
2. Judge on **100+ trades**: % profitable, **profit factor**, max drawdown, avg
   trade. Ignore samples < 30 trades.
3. **In-sample vs out-of-sample:** tune on older data, confirm on untouched recent
   data. If the edge dies out-of-sample, it was curve-fit.
4. **Reality check:** TradingView fills limits/stops against OHLC assumptions and is
   optimistic on gaps and on *which* touched first (stop vs target) within a bar.
   Treat backtest stats as an **upper bound**, then **forward-test on demo**.
5. **Multi-instrument robustness:** a real liquidity edge should show *positive
   expectancy on several* correlated instruments (e.g. US500 **and** NAS100), not
   one cherry-picked symbol.

## How to optimize it
Tune a few at a time, out-of-sample, preferring **wide plateaus** over sharp peaks:

| Input | Range | Effect |
|---|---|---|
| External swing length | 6–20 | Larger = higher-grade liquidity/structure, fewer setups |
| Internal swing length | 3–7 | Smaller = more MSS triggers (more frequency) |
| Dealing-range lookback | 60–200 | Defines premium/discount context |
| Min FVG size (× ATR) | 0.3–1.0 | Higher = only strong displacement counts |
| Setup window after raid | 6–20 | Longer = catches slower reversals |
| Stop buffer (× ATR) | 0.1–0.5 | Wider = fewer wick-outs, worse RR |
| A+ / B thresholds | 70–85 / 50–60 | Raise to cut frequency, lower to add it |
| Killzone-only | on/off | On = fewer, cleaner; Off = more frequency |

**Frequency tuning:** if you get too few signals, lower `Internal swing length`,
lower `Min FVG size`, enable **B setups**, and widen `Setup window`. If too many,
do the opposite or set **Killzone-only = on**.

## Best markets & timeframes
- **Indices** (US500, NAS100, GER40): **15m–1H** — cleanest liquidity runs.
- **Gold** (XAUUSD): **15m–1H** — textbook London/NY raids; ATR stop self-adapts.
- **Forex majors**: **15m–1H** with killzones on.
- **Crypto** (BTC/ETH): **15m–1H**, killzones off (24/7), sessions less meaningful.

## Weaknesses (be honest)
- **FVG-limit entries don't always fill** — a clean move with no retrace is skipped
  (switch to "market on MSS close" for more fills at worse RR).
- **Pivots confirm late** (`right` bars), so structure/MSS is slightly delayed — the
  price of not repainting.
- **Backtest fill realism**: intrabar stop-vs-target ordering is assumed, not known.
- **Equal-H/L detection is tolerance-based** — noisy instruments create loose pools.
- **Sessions are timezone-sensitive**: set the correct `tz` for your instrument.
- **Not every raid reverses** — continuation through liquidity will stop you out;
  that's why the RR gate and daily loss cap exist.

## Version 3 upgrade ideas
- **HTF liquidity & HTF FVG** (draw from 4H/Daily arrays, not just an EMA bias).
- **Order-block entries** (last down-candle before displacement) as an alternative
  to raw FVG.
- **Breaker blocks & inversion FVGs** for continuation setups.
- **Partial-fill-aware position sizing** (fixed-fractional risk per trade for prop
  rules) + per-session trade caps.
- **SMT divergence** between correlated pairs (ES/NQ, EURUSD/DXY) as a confluence.
- **Adaptive `minFvg`** scaled to realized volatility regime.

## Final audit — non-repainting / no future leakage / compilation
**Non-repainting:** `calc_on_every_tick=false` (bar-close logic); pivots consumed
only after they confirm; PDH/PDL/PWH/PWL and HTF bias read the **previous closed**
period via `request.security(..., [1], lookahead_off)`; signal flags (`sigLong`/
`sigShort`) are set on the confirmed trigger bar and never rewritten; entries are
limit/market orders that fill on **later** bars.
**No future data:** no negative-offset reads, no `lookahead_on`, no forming-bar
security pulls.
**No after-the-move signals:** the trigger is the *first* MSS immediately after a
fresh raid + displacement — the start of the leg, not a lagging confirmation.
**Compilation (hand-reviewed):** fixed undefined-function, comma-`var`, float→int,
and day-rollover signal-misfire bugs during the build; removed unused variables to
avoid warnings. Scale-out math nets the position flat in both target-ladder and
stop scenarios. ⚠️ Not machine-compiled here — please verify in the Pine Editor.
