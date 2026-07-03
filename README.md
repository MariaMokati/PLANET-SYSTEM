# Apex Trend Continuation System (ATCS) v1

A complete, non-repainting **Pine Script v6 strategy** for TradingView. It trades
pullback continuations inside filtered trends and manages risk with an
ATR-based stop and a 1R / 2R / 3R take-profit ladder.

> **Honest disclaimer.** This is a research tool, not a money printer. No win
> rate is guaranteed. The 55–70% target is an *aspiration for the right
> instrument/timeframe after you backtest and tune it* — it is **not** a
> claimed or fabricated result. Nothing here is financial advice. Always
> forward-test on a demo account before risking real capital.

---

## 1. Strategy name
**Apex Trend Continuation System (ATCS) v1** — file: [`ATCS_strategy.pine`](./ATCS_strategy.pine)

## 2. Strategy thesis
The most durable, cross-asset edge is **trading *with* an established trend, but
only entering on a pullback that resets momentum, and only while the market is
genuinely trending rather than chopping.** Chop destroys trend systems, and
buying extended tops destroys pullback systems — so ATCS refuses both. It waits
for price to retrace to a dynamic level (EMA-pullback), reclaim it on a closed
bar, confirm with momentum and real swing structure, and only then fires — in
the direction of the dominant EMA stack, inside a trending ADX regime.

## 3. Why this model was selected

| Model considered | Verdict | Reason |
|---|---|---|
| Pure trend-following (MA cross) | ❌ | Whipsaws badly in chop; late entries; poor RR. |
| Mean reversion | ❌ | Works in ranges only; catastrophic in trends; regime-fragile. |
| Breakout | ⚠️ | Many false breaks; needs volume/structure confirmation; regime-specific. |
| Liquidity-sweep / SMC | ⚠️ | Powerful but discretionary and easy to overfit/repaint. |
| Volatility expansion | ⚠️ | Good triggers but no directional edge on its own. |
| Multi-timeframe confirmation | ✅ (as a *filter*) | Improves quality; used as an optional non-repainting HTF bias. |
| Volume confirmation | ⚠️ | Unreliable/unavailable on forex & many indices — not made mandatory. |
| **Hybrid: filtered trend + pullback + momentum + structure + ATR risk** | ✅ **Chosen** | Best balance of hit-rate, realistic RR, robustness, and cross-asset usability with the fewest overfit parameters. |

The chosen hybrid keeps each component doing one job it is individually good at,
and every filter removes a *specific, known* failure mode.

## 4. Full rule set

**Long entry — ALL must be true on the closed bar:**
1. **Trend:** `EMA50 > EMA200` **and** `close > EMA50` (bullish stack).
2. **Regime:** `ADX > threshold` (default 20) **and** `DI+ ≥ DI−` (buyers in control).
3. **Volatility:** `ATR% ≥ min` (skips dead markets).
4. **Session:** inside the allowed session (optional).
5. **HTF bias:** previous *closed* HTF bar above its EMA200 (optional, non-repainting).
6. **Structure:** most recent confirmed pivot low is **higher** than the prior one (HL).
7. **Pullback:** price dipped to/under the pullback-EMA within the lookback window.
8. **Trigger:** `close` **crosses above** the pullback-EMA (fresh reclaim).
9. **Momentum:** `RSI ≥ 50` **and** rising.

**Short entry** is the exact mirror (bearish stack, `DI− ≥ DI+`, LH structure,
rally to the pullback-EMA, cross-under reclaim, `RSI ≤ 50` and falling).

**Exits / risk (both directions):**
- **Stop** = `entry ∓ ATR × 1.5`.
- **TP1** = 1R → close 50%, then move stop to **breakeven**.
- **TP2** = 2R → honours the mandatory **minimum 1:2** reward-to-risk.
- **TP3** = 3R → optional runner for the remainder.
- One position at a time (`pyramiding = 0`).

## 5. Pine Script v6 code
See **[`ATCS_strategy.pine`](./ATCS_strategy.pine)** — fully commented, v6, strategy mode.

## 6. Alert setup instructions
1. Add the strategy to a chart, open **Create Alert**.
2. Under *Condition* pick **ATCS v1**, then one of the built-in `alertcondition`s:
   `Long Entry`, `Short Entry`, `TP1 Hit`, `Long Closed`, `Short Closed`, or `Any Entry`.
3. Set **"Once per bar close"** (never *once per bar*) so alerts match the
   non-repainting logic.
4. For fill-accurate automation, you may *also* create a second alert on the
   strategy itself set to **"Order fills only"**.

## 7. Backtesting instructions
1. Paste `ATCS_strategy.pine` into the TradingView Pine Editor → **Add to chart**.
2. Open the **Strategy Tester** tab. Review Net Profit, **% Profitable**,
   **Profit Factor**, Max Drawdown, and average trade.
3. Set realistic **commission** and **slippage** in the settings (defaults:
   0.02% commission, 1 tick slippage) — a strategy that only works at zero cost
   is not real.
4. Judge on a **large sample** (aim for 100+ trades). Ignore anything with <30 trades.
5. Compare in-sample vs a held-out **out-of-sample** period; if edge collapses
   out-of-sample, it is overfit — loosen parameters, don't tighten them.

## 8. Recommended markets and timeframes
- **Indices** (US500, NAS100, GER40): 15m–4H. Strong trends, respects ADX filter well.
- **Forex majors** (EURUSD, GBPUSD, USDJPY): 1H–4H. Enable the session filter.
- **Metals** (XAUUSD): 15m–1H. High ATR — the ATR stop adapts automatically.
- **Crypto** (BTC, ETH): 1H–4H, 24/7 so leave the session filter **off**.

Below 15m, noise and costs dominate — not recommended.

## 9. Optimization parameters
Tune **a few at a time**, on out-of-sample data, and prefer wide plateaus over
sharp peaks:

| Input | Suggested range | Notes |
|---|---|---|
| Slow Trend EMA | 150 – 250 | Higher = fewer, cleaner trends. |
| Pullback EMA | 10 – 34 | Lower = more/earlier entries. |
| ADX Threshold | 18 – 28 | Higher = stricter trend filter, fewer trades. |
| RSI Pivot | 45 – 55 | Loosen to 45 for more longs / tighten for quality. |
| ATR × (stop) | 1.2 – 2.5 | Wider = fewer stop-outs, worse RR per trade. |
| TP2 (R) | ≥ 2.0 | Keep ≥ 2.0 to preserve the 1:2 minimum. |
| Pivot Left/Right | 3–8 / 2–5 | Larger = stronger but slower structure. |

## 10. Known weaknesses
- **Lag:** confirmation-based entries miss the first leg of a move by design.
- **Sharp V-reversals:** pullback logic can be late when trends flip violently.
- **Pivot delay:** structure confirms `pivotRight` bars late (the price of no repaint).
- **News spikes:** ATR stops can be gapped through; size accordingly.
- **Ranging drift:** in weak, sub-threshold trends the ADX filter may sit you out
  for long stretches (this is intended, but feels slow).
- **Default sizing** uses 100% equity for clean % returns; replace with fixed
  fractional risk for live use.

## 11. What to improve in Version 2
- Fixed-fractional / ATR-based **position sizing** (risk a set % per trade).
- **ATR trailing stop** on the runner instead of a fixed 3R target.
- **Volume/OBV confirmation** where the instrument provides real volume.
- **Multi-TF structure** (HTF pivots) rather than only an HTF EMA bias.
- Time-of-day / day-of-week **performance analytics** in the dashboard.
- **Partial re-entries** on continuation after TP1 in strong trends.

## 12. Final quality checklist
- [x] Pine Script **v6**, strategy mode (backtesting-first).
- [x] Clear **long & short** entries with buy/sell arrows.
- [x] **Stop loss + TP1 + TP2** plotted; optional **TP3 runner**.
- [x] **Minimum 1:2 RR** enforced by the TP2 default.
- [x] Trend, **volatility**, **momentum**, **market-structure**, **session**, and **chop/ADX** filters.
- [x] **Risk dashboard** + **win/loss statistics** panel.
- [x] **Alert conditions** for entries, TP1, and exits.
- [x] **User inputs** for every major setting, grouped and tooltipped.
- [x] **Non-repainting**: bar-close logic, confirmed pivots, `lookahead_off` HTF, next-bar fills.
- [x] No future-looking data; no "after-the-move" signals.
- [x] Clean visuals; comments explaining the important logic.
- [x] **No fabricated results** and **no guaranteed-profit** claims.

---

### How to use it (quick start)
1. Open TradingView → **Pine Editor** → paste `ATCS_strategy.pine` → **Add to chart**.
2. Pick a recommended market/timeframe (e.g. **XAUUSD 1H** or **US500 1H**).
3. Read the **dashboard** (top-right): trend, ADX, RSI, ATR%, position, live SL/TP, and stats.
4. Wait for a **LONG/SHORT arrow** — it prints only on a *closed* bar, so it will not repaint.
5. Let the strategy manage the ATR stop, breakeven-after-TP1, and the TP ladder.
6. **Backtest first, then demo-trade, then** consider going live with proper risk sizing.
