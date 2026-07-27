# Basic CRT — Step 2: Sweeps. Specification and evidence.

Written before any code. Every number below was measured on real
**OANDA:XAUUSD** data — the same feed the chart uses — pulled from the MT5/
TradingView mirror at `simom1/XAUUSD-history`, covering 2023-01 → 2026-05 on
4H/1H/30m/15m and back to 2010 on D/W.

Test scripts live in `/tmp/sweep/`: `sweep_lab.py` (reference implementation),
`measure*.py` (measurements), `pine_sim.py` (Pine execution-model simulation).

---

## 1. What a sweep is

Consensus across ICT / Smart-Money and Candle Range Theory sources:

> Price runs a level where stop orders sit, fails to hold beyond it, and closes
> back inside. The wick takes the liquidity; the body refuses the new price.

Candle Range Theory states it as a three-candle sequence:

- **Candle 1** sets the range — its high and low are the levels.
- **Candle 2** manipulates — it sweeps candle 1's high or low (the "turtle
  soup"). *For CRT to be valid, candle 2 must close back inside candle 1's
  range.*
- **Candle 3** distributes — price travels toward the opposite side of the
  range, first target 50% (equilibrium), final target the opposite extreme.

The opposite outcome has a different name and different meaning:

- **Sweep / purge** — took the level, closed back inside. Rejection.
- **Break / run** — took the level and closed beyond it. Acceptance.

That single distinction — where the candle **closed** relative to the level —
is the whole classification. Everything else is presentation.

**Sources:** [TradingFinder — ICT Candle Range Theory](https://tradingfinder.com/education/forex/ict-candle-range-theory/) ·
[Trading Wyckoff — CRT explained](https://tradingwyckoff.com/en/crt/) ·
[Writofinance — Candle Range Theory](https://www.writofinance.com/candle-range-theory-crt/) ·
[LiquidityScan — the ICT stop hunt](https://liquidityscan.io/blog/liquidity-sweep-explained-the-ict-stop-hunt) ·
[BH Terminal — sweep vs breakout](https://www.bhterminal.com/en/insights/liquidity-sweep-vs-breakout-how-to-tell-the-difference) ·
[Alchemy Markets — liquidity sweep](https://alchemymarkets.com/education/strategies/liquidity-sweep/) ·
[Equiti — spotting liquidity sweeps](https://www.equiti.com/sc-en/news/trading-ideas/liquidity-sweeps-explained-how-to-identify-and-trade-them/)

---

## 2. Four defects measured in the previous sweep layer

### D1 — "full body outside" made breaks almost impossible, and hid 37% of candles

The old default required a break to have **open AND close** beyond the level.
In a 24-hour market the open of the next candle equals the close of the last, so
that condition needs a gap. Measured on 4H XAUUSD:

| break rule | breaks found | sweeps found |
|---|---|---|
| body fully outside (old default) | **60** of 5,269 candles (1.1%) | 2,219 |
| close beyond the level | 2,532 (48%) | 1,780 |

Worse than being rare, it left a hole. A candle that wicks above the level and
closes above it, but opened below it, was **neither** a break (body not fully
out) **nor** a sweep (close not back inside). It got no tag at all:

| timeframe | candles touching a level but silently untagged |
|---|---|
| 15m | 2,292 — 39.4% of all candles |
| 1H | 3,351 — 40.3% |
| 4H | 2,034 — 38.6% |
| D | 4,077 — 27.6% |

Confirmed again through the Pine execution simulation: of 381 4H candles,
strict tagged 175, close-based tagged 317 — 142 candles (37%) silently
reclassified as "inside" when they had in fact taken a level.

**Fix: break = the candle CLOSES beyond the level.** Keep "require the open
beyond too" as an off-by-default option for anyone who wants gap-only breaks.

With that change the classification becomes a complete partition — measured
holes on 15m/1H/4H: **2, 3 and 1 candle** out of thousands, all exact-equality
ties (close landing precisely on the level).

### D2 — the double sweep is the losing configuration and was treated as normal

10–14% of candles take **both** the prior high and the prior low and close back
inside. The old code treated these as a valid setup, picking direction from
which side of equilibrium the close landed.

Tested as a trade — target the opposite extreme, stop at the sweep wick:

| timeframe | n | win rate | mean R:R | expectancy |
|---|---|---|---|---|
| 15m | 265 | 73.2% | 0.27 | **−0.079 R** |
| 1H | 413 | 65.6% | 0.28 | **−0.175 R** |
| 4H | 288 | 66.3% | 0.28 | **−0.175 R** |

High win rate, terrible geometry: after taking both sides the candle already sits
near the opposite extreme, so there is almost nothing left to win and a long way
to lose. Negative on every timeframe.

**Fix: mark double sweeps as their own event, in their own colour, with their
own switch, defaulted OFF for setups.**

### D3 — "one tag per candle" hides the strongest CRT pattern

A candle can sweep one side and break the other: it dips under the low, rejects
it, and closes clean above the high. Manipulation and distribution inside one
candle. Frequency, as a share of all breaks: **15m 12.2%, 30m 12.6%, 1H 13.2%,
4H 17.4%, D 9.2%.** Under one-tag-per-candle these print as a plain break and
the sweep half disappears.

**Fix: keep one tag per candle by default, add an optional "sweep + break"
combined marker for exactly this case.**

### D4 — markers anchored at the candle's open floated left

Confirmed in simulation: anchoring a 4H marker at the source candle's open time
places it **240 to 4,620 minutes** — up to 77 hours — to the left of the bar
that detected it on a 15m chart. Anchoring at `open + timeframe − 1ms` lands it
on the far right edge of the candle it describes, 100% of the time, on every
chart timeframe tested.

**Fix: keep close-anchoring as the default (already correct in the last build).**

---

## 3. What the sweep marker is worth — measured, not claimed

The CRT claim is testable exactly: after candle B sweeps candle A's high, does
price reach A's low (the opposite extreme) before trading back above B's high
(the sweep wick)? Both levels are known at B's close. Bars touching both levels
are scored as losses, since intrabar order is unknowable.

First pass looked excellent — win rate beating the break-even implied by mean
R:R by +8 to +11 percentage points on every timeframe, stable across horizons of
2–12 candles and across both halves of the sample.

**That edge does not survive decomposition.** Break-even computed from the *mean*
R:R is only valid if win rate is independent of each trade's own R:R. It is not.
Bucketing 4H sweeps by their own R:R:

| R:R bucket | n | win% | break-even% | edge | expectancy |
|---|---|---|---|---|---|
| <0.5 | 198 | 60.6% | 76.2% | −15.6pp | −0.214 R |
| 0.5–1 | 317 | 56.8% | 57.3% | −0.5pp | −0.020 R |
| 1–1.5 | 222 | 40.5% | 44.6% | −4.1pp | −0.097 R |
| 1.5–2 | 196 | 41.3% | 36.7% | +4.6pp | +0.119 R |
| 2–3 | 218 | 31.2% | 29.2% | +2.0pp | +0.043 R |
| 3–5 | 165 | 19.4% | 20.9% | −1.5pp | −0.084 R |
| >5 | 135 | 11.9% | 9.5% | +2.3pp | +0.122 R |

Signs scatter. The headline "+10pp" was an averaging artifact.

True expectancy per trade, gross of costs, with bootstrap 95% confidence
intervals:

| timeframe | n | expectancy | 95% CI | net @ $0.10/side |
|---|---|---|---|---|
| 15m | 1,963 | −0.016 R | [−0.079, +0.050] | −0.074 R |
| 1H | 2,524 | −0.054 R | [−0.108, +0.003] | −0.108 R |
| 4H | 1,451 | −0.024 R | [−0.109, +0.056] | −0.072 R |

Every confidence interval spans zero. A minimum-R:R filter does not rescue it —
the best net cell found was 4H with R:R ≥ 1.5 at −0.019 R, still negative.

**Conclusion: a sweep, taken mechanically as a trade to the opposite extreme, is
break-even at best gross and loses after costs.** This matches the earlier
6,240-configuration study. The sweep marker is a **location marker** — it shows
where liquidity was taken so a decision can be made there. It is not a signal to
be traded on its own, and the indicator will not present it as one.

---

## 4. Mechanical proofs run on the algorithm

| test | result |
|---|---|
| No lookahead — labels identical when history is truncated at 4 points | PASS |
| Sweep and break never both set on one candle | 0 violations across 4 timeframes |
| Break-up and break-down never both set | 0 violations |
| Resampling equivalence: 1H → 4H reproduces broker 4H candles | 100.00% exact, 810 candles |
| Marker anchor lands inside its own candle | 100% on 15m/1H/4H |
| **Identical marks on 15m, 30m and 1H charts** (the bug that broke the last build) | **identical, 379 / 2,172 / 2,172 candles compared** |
| One mark per source candle, no repeats | 0 duplicates |
| Anchor precedes the bar that detected it (causal) | PASS |

---

## 5. Build plan — step by step

**Step 2.1 — Classification core.** One function, run once per closed candle of
the sweep timeframe, against the candle before it.

```
took_hi = B.high > A.high
took_lo = B.low  < A.low
brk_up  = took_hi and B.close > A.high      (+ optional: and B.open > A.high)
brk_dn  = took_lo and B.close < A.low       (+ optional: and B.open < A.low)
is_break = brk_up or brk_dn
swp_hi  = took_hi and not is_break and B.close < A.high
swp_lo  = took_lo and not is_break and B.close > A.low
```
Order of precedence: break outranks sweep. Classification happens once, before
any drawing, so which layers are switched on can never change what a candle *is*.

**Step 2.2 — Six distinct events, each with its own switch and colour.**
`SWEEP HIGH` · `SWEEP LOW` · `DOUBLE SWEEP` · `BREAK UP` · `BREAK DOWN` ·
optional `SWEEP+BREAK` combined. Double sweep is visually distinct because it
measured negative on every timeframe.

**Step 2.3 — Data access.** `request.security(sym, tf, [low[2], high[2], low[1],
high[1], open[1], close[1], time[1]], lookahead_off)`. Both candles always
closed. De-duplicate on the source candle's own open time so each candle is
marked exactly once regardless of chart timeframe.

**Step 2.4 — Anchoring.** `open_time + tf_length − 1ms`, `xloc.bar_time`.
Proven to land inside the correct candle on every chart timeframe.

**Step 2.5 — Timeframe selection.** Follows the Range group's dropdown by
default, with an override so sweeps can be watched on a different timeframe from
the one drawing ranges.

**Step 2.6 — Presentation.** Marker design (label / triangle / circle), size,
per-event colours, optional timeframe title, keep-last-N pruning. At ~34% of 4H
candles carrying a mark, pruning is required, not optional.

**Step 2.7 — Verification before delivery.** Re-run `pine_sim.py` against the
final rule set and confirm: identical marks on 15m/30m/1H charts, one mark per
candle, no untagged level-touching candles, anchors inside their own candle.

---

## 6. Defaults, and why

| setting | default | reason |
|---|---|---|
| Break rule | close beyond the level | body-fully-outside finds 60 breaks in 5,269 candles and hides 37% of the chart |
| Sweep rule | close back inside the range | classic CRT; the alternative produces stops so tight costs eat 0.16 R |
| Double sweep | shown, own colour, excluded from setups | negative expectancy on 15m, 1H and 4H |
| Sweep + break combined | off | one tag per candle stays the default; available for the 12–17% case |
| Anchor | candle close | open-anchoring floats markers up to 77 hours left |
| Sweep timeframe | follows the range dropdown | one range, one sweep set, nothing to reconcile |
