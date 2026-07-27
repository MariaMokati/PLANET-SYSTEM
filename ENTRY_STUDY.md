# CRT entries — measured against the rule as actually traded

**The rule.** A full bar closes back inside the range. That is the setup. Target
the opposite extreme. Stop beyond the wick that did the sweeping.

Seven instruments. Gold on **16.5 years of 5-minute bars** (561,659 of them);
the rest on 15-minute bars, 2012–2026. All figures net of realistic costs.

## A correction to an earlier version of this study

An earlier pass ranked entry models purely on expectancy after costs and put a
**retrace-to-equilibrium entry** on top. That was wrong on the trading, and the
numbers should have made it obvious:

- Entering at the 50% mark means entering **into the leg**. Price has already
  travelled from the sweep back to the middle, so the trade joins a move rather
  than taking it at its origin.
- It keeps the risk and gives up half the reward. Measured R:R was **0.49** —
  risking two to make one — with a 72–82% win rate. That win rate is not a sign
  of quality; it is the signature of a bad payoff structure.

It also produced a "New York open is positive on 6 of 7 instruments" result.
**That result does not survive** once the target goes back to the opposite
extreme. It was an artifact of the entry structure, not a property of the
session. Everything below uses the correct rule.

## Result 1 — nothing is positive, in any configuration

Mean net R at realistic cost, 4H ranges, across seven instruments. Every cell in
the "+" column is the number of instruments that were positive:

| entry model / stop | XAUUSD | XAGUSD | NAS100 | US30 | UK100 | EURUSD | GBPUSD | + | mean |
|---|---|---|---|---|---|---|---|---|---|
| close / 50% stop | −0.135 | −0.094 | −0.094 | −0.067 | −0.089 | −0.123 | −0.131 | **0** | **−0.105** |
| close / 25% stop | −0.177 | −0.148 | −0.146 | −0.124 | −0.153 | −0.211 | −0.215 | 0 | −0.168 |
| reclaim / 50% stop | −0.195 | −0.180 | −0.142 | −0.135 | −0.159 | −0.203 | −0.217 | 0 | −0.176 |
| limit at level / 50% | −0.239 | −0.213 | −0.176 | −0.162 | −0.221 | −0.250 | −0.260 | 0 | −0.217 |
| close / wick stop | −0.237 | −0.243 | −0.191 | −0.181 | −0.206 | −0.274 | −0.310 | 0 | −0.235 |
| limit at level / wick | −0.570 | −0.368 | −0.056 | −0.167 | −0.476 | −0.530 | −0.587 | 0 | −0.393 |
| reclaim / wick stop | −0.650 | −0.642 | −0.370 | −0.429 | −0.513 | −0.659 | −0.846 | 0 | −0.587 |

**Zero of seven, in every row.**

## Result 2 — two things help, consistently, on every instrument

**Widen the stop.** Cost in R is `2 × slippage ÷ stop distance`, so a tight stop
turns a fixed spread into a large fraction of R. Going from a stop at the sweep
wick to one 50% of the range beyond the level improves the mean from −0.235 to
−0.105 R, and improves every single instrument.

**Bank half at equilibrium, run the rest with the stop at breakeven.** This is
not entering at EQ — it is taking profit there, which is the standard CRT
management. Entry stays at the confirming close.

| | XAUUSD | XAGUSD | NAS100 | US30 | UK100 | EURUSD | GBPUSD | mean |
|---|---|---|---|---|---|---|---|---|
| hold to the extreme (net) | −0.135 | −0.094 | −0.094 | −0.067 | −0.090 | −0.123 | −0.131 | −0.105 |
| bank half at EQ (net) | −0.071 | −0.052 | −0.062 | −0.038 | −0.073 | −0.088 | −0.082 | **−0.067** |
| improvement | +0.064 | +0.043 | +0.032 | +0.029 | +0.016 | +0.036 | +0.049 | **+0.038** |

Better on **7 of 7**, and it turns the trade **gross positive on 6 of 7**
(gold +0.014, silver +0.058, US30 +0.019, UK100 +0.021, EURUSD +0.043,
GBPUSD +0.069; NAS100 −0.006). Costs are what keep it under water.

## Result 3 — sessions, under the correct rule

Entry at the confirming close, 50% stop, by the range candle's SAST slot:

| bucket | mean net R | best instrument |
|---|---|---|
| **11:00/12:00 London PM** | **−0.067** | UK100 −0.045 |
| 15:00/16:00 NY open | −0.083 | NAS100 −0.013 |
| 07:00/08:00 London open | −0.087 | XAGUSD +0.001 |
| 23:00/00:00 daily open | −0.109 | US30 −0.066 |
| 03:00/04:00 Asia | −0.122 | NAS100 −0.014 |
| 19:00/20:00 NY afternoon | −0.144 | XAGUSD −0.102 |

This **matches the range-only study exactly**: London PM best, Asia and NY
afternoon worst. Two independent measurements, same ordering. The session
ranking is the most reproducible thing found in this project.

## Where that leaves it

Best honest configuration: **confirming close entry, stop 50% of the range
beyond the swept level, half off at equilibrium with the stop to breakeven, the
rest to the opposite extreme, 4H ranges, London PM or NY open, avoiding Asia and
NY afternoon.** Mean **−0.067 R** per trade across seven instruments.

That is gross positive on six of seven and loses to costs. Three things could
close a gap that size, in order of how much they are worth checking:

1. **Fills.** Every number here assumes $0.25/side on gold and equivalents
   elsewhere. Halving that roughly halves the deficit. This is measurable on a
   real account and is the single highest-value unknown.
2. **A validated directional filter.** Taking only setups that agree with
   higher-timeframe structure is untested — the earlier attempt at one was never
   validated and performed badly in review.
3. **Selection.** Everything here takes *every* qualifying setup. Discretion
   applied on top is not measured and cannot be measured from price data alone.

What this does not support is trading the mechanical rule set as-is and
expecting it to pay.
