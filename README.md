# Liquidity Market-State Engine (LMX) v3

A price-action / liquidity / market-structure **Pine Script v6 strategy** for
TradingView. **No** EMA/RSI/MACD/ADX/Supertrend as the entry engine. ATR is used
only for volatility normalisation and stop padding — never for direction.

> **HONESTY (this is not optional).** This code is **manually reviewed, NOT
> machine-compiled here, and NOT Strategy-Tester validated.** It describes
> *expected behaviour only*. I make **no** claim that it is profitable, no win-rate
> claim, nothing "institutional-grade." SMC/liquidity systems frequently still fail
> to beat costs. You must compile it and validate it in the Strategy Tester before
> trusting it. If TradingView shows an error, paste it back and it gets fixed.

File: [`ATCS_strategy.pine`](./ATCS_strategy.pine)

---

## What was removed from the failed v2 (and why)
v2 (profit factor 0.537, 20% win rate on XAU 1H) took **every** sell-side sweep as
a long and every buy-side sweep as a short — **regardless of premium/discount
location or market structure.** On trending days that is literally *fading the
trend*: a 20% win rate is the signature of that mistake. It was removed entirely.

## Why v3 is different (the one change that matters)
Direction is now **gated by structure and location**:
- **Longs only** when: structure is **bullish** (a BOS/CHoCH up has occurred) **and**
  price is in **discount** (below equilibrium) **and** sell-side liquidity was just
  **swept & rejected** **and** there is unswept **buy-side** liquidity to target.
- **Shorts** are the exact mirror (bearish structure, premium, buy-side swept).

Trade **with** the draw, from the **correct half** of the range. Everything else is
a filter on top of that.

## The 18 modules (best Pine approximation of each; limitations marked)
1. **Dealing range** — highest/lowest over lookback → premium / discount / equilibrium + % position.
2. **Liquidity map** — PDH/PDL, PWH/PWL, Asia/London/NY frozen H/L, recent swings, equal H/L; nearest BSL/SSL. *(Approximation: "nearest 3 pools" is rendered as the key named pools + nearest swing rather than an arbitrary top-3 list, to keep the chart clean.)*
3. **Draw on liquidity** — structure-first, then proximity → buy-side / sell-side / neutral, with level, distance, RR, internal/external.
4. **Sweep engine** — wick beyond a pool + close back inside + rejection candle. External raids (PD/PW/session) graded higher.
5. **Displacement engine** — body ≥ 60% of range, close in top/bottom 25%, body > 1.3× avg(10) body, **and** breaks an internal swing (this doubles as the MSS).
6. **Structure engine** — swings, **BOS**, **CHoCH**, protected high/low, structure invalidation when the protected level breaks. *(Approximation: BOS/CHoCH from confirmed pivots; internal vs external is length-based.)*
7. **Entry engine** — full sequence enforced; entry = **50% of the displacement leg** (a valid ICT retrace entry), placed as a limit with a validity window.
8. **Target engine** — nearest **unswept liquidity pool** within [minRR, maxRR]; if none is ≥ 2R and reachable, **the trade is rejected**.
9. **Stop engine** — structural: beyond the **sweep wick** + a small ATR pad (not an arbitrary ATR stop).
10. **Quality score 0–100** — sweep 20 / draw 20 / displacement 20 / MSS 15 / P-D location 10 / session 10 / RR 5. Tiers: **A+ ≥85, A ≥75, B ≥65**, below 65 = no trade.
11. **Frequency control** — tier selector (`A+ only` / `A and above` / `B and above`). Loosen to trade more.
12. **Duplicate control** — a raid is **consumed** when it fires; one position at a time; state resets only when flat.
13. **Session engine** — Asia/London/NY frozen H/L, killzone-only option, session shown on dashboard. *(Approximation: London-sweep-of-Asia and NY-continuation logic is expressed through the liquidity pools + premium/discount rather than as hard-coded narratives.)*
14. **Prop-firm protection** — max trades/day, max/session, daily loss %, daily target lockout, max consecutive losses, cooldown after a loss, restricted window, Friday cutoff, skip-Monday.
15. **Dashboard** — structure, draw (+ level/distance), nearest BSL/SSL, swept-today, range, premium/discount %, session, setup tier+score, expected RR, position, trades D/S, day PnL % (+lock), win% / PF, consecutive losses.
16. **Visuals** — key pools (hidden once swept), sweep + displacement + CHoCH markers, entry-zone box, SL/TP for the active trade only, arrows + one reason label per signal.
17. **Alerts** — long/short entry, SSL/BSL swept, bull/bear displacement, long/short closed, daily lockout, any entry.
18. **Backtest honesty** — see the disclaimer at the top; nothing is claimed as verified.

## Signal labels explain themselves
> `LONG A: SSL swept + bullish displacement + MSS + discount → draw BSL @ 4512.50`

## Inputs & how to tune them
- **Frequency too low?** → set *Trade which tiers* = `B and above`, lower *Body vs avg body ×* (1.3→1.15), lower *External swing length* (8→6), raise *Setup window*.
- **Too many weak trades?** → `A+ only`, raise *Body ×*, raise *Min body/range*, keep *Only trade in London/NY* = on.
- **Getting wicked out?** → raise *Stop pad*. **Targets never hit?** → lower *Maximum RR*.
- **Prop account?** → set *Max daily loss %*, *Daily target %*, *Max consecutive losses*, *Max trades/day & /session* to your firm's rules.
- **Sessions wrong?** → set *Timezone* to your instrument's exchange tz and adjust the three session windows.

## What each visual means
Red/green dashed = nearest buy/sell liquidity & PDH/PDL; thick maroon/teal = weekly
H/L; grey = equilibrium. Blue ▲ / orange ▼ = a liquidity sweep. Green ▲ / red ▼ =
displacement. Aqua/fuchsia **C** = CHoCH. Blue box = the entry zone (50% of the
displacement leg). Solid red/teal/green/lime lines appear only while a trade is
live = SL / TP1 / TP2 / TP3.

## Alert setup
Create Alert → condition **LMX v3** → pick the event → **"Once per bar close"**. Add
a second alert on the strategy set to **"Order fills only"** for exact fills.

## Notion trade journal (optional)
Pine cannot call an API, so journalling is a relay: the strategy emits a JSON
`alert()` → TradingView webhook → a Cloudflare Worker → a Notion database row.
Turn it on with *Emit JSON alert() payloads* in the **Webhook** input group (off
by default; it adds nothing to the trading logic) and create the alert with
condition **"Any alert() function call"**. Setup, schema and troubleshooting:
[`integrations/notion/README.md`](./integrations/notion/README.md).
**Same honesty rule as above:** the Worker is **unit-tested locally only** — it
has never been deployed, nor run against a live Notion workspace or a live
TradingView alert from this repo.

## Backtest setup & validation
1. Add to chart → **Strategy Tester**. Keep the built-in commission (0.02%) & slippage (1 tick).
2. Need **100+ trades** before any opinion; ignore < 30.
3. **In-sample vs out-of-sample** — tune on old data, confirm on untouched recent data.
4. Test on **several instruments** (XAU, NAS100, US30, US500, EURUSD, GBPUSD, BTC, ETH) — a real edge generalises; a curve-fit one doesn't.
5. Treat tester stats as an **upper bound** (intrabar stop-vs-target order is assumed), then **forward-test on demo**.

## Best markets & timeframes
Indices (US500, NAS100, US30) and gold on **15m–1H**; FX majors 15m–1H with
killzones on; crypto 15m–1H with sessions off.

## Known weaknesses
- **50% limit entries don't always fill** — clean one-way moves get skipped.
- **Pivot-confirmed structure lags** by `swLen`/`intLen` bars (the price of no repaint).
- **`nearest-3 pools` and `London-sweep/NY-continuation` are approximated**, not literal.
- **Consecutive-loss counter** is measured entry→flat equity, so a scratch after TP1 counts as a small win.
- **Backtest fill realism** (intrabar ordering) still applies.
- **May be too strict OR still unprofitable** — only the Strategy Tester will tell you.

## Version 4 ideas
True top-3 pool ranking with per-pool line objects; HTF (4H/D) liquidity & FVG
arrays; order-block / breaker entries alongside the 50% entry; SMT divergence
between correlated pairs; explicit London-sweep→NY-continuation state machine;
fixed-fractional risk-per-trade sizing.

## Final self-review
- Direction is now structure- and location-gated (the v2 killer is gone).
- Sequence sweep→displacement→MSS→retrace→RR-gate is enforced before any signal.
- Non-repainting: bar-close logic, confirmed pivots, previous-period security reads, later-bar fills.
- **Not compiled or tester-validated here** — hand-reviewed against the v6 spec; fixed comma-chained statements, empty-array loop bounds, unused vars, and per-setup loss tracking during the build. Compile it in TradingView and send any error back.
