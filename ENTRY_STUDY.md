# CRT entries: which entry model actually works?

Seven instruments. Gold on **16.5 years of 5-minute bars** (561,659 of them);
the rest on 15-minute bars, 2012–2026. Everything net of realistic costs.

## Method

A range candle **A**, then the next candle **B**. The moment price trades beyond
one of A's levels the setup **arms** — at a touch, not at a close. That matters:
at the instant of arming nobody knows whether the candle will close back inside
(a sweep) or beyond (a break). Every lower-timeframe model here enters blind to
that outcome, so the losers where the level simply broke are included. Only
`htf_close` waits for the close, and it is scored on the smaller population it
can actually see.

Higher-timeframe candles are built from the same lower-timeframe bars the
entries are measured on, so the two can never disagree about where a candle
starts. 4H and D hang off the chart's 17:00 New York anchor. A bar touching both
stop and target is scored a **loss**.

Six entry models: `htf_close` (enter at the sweep candle's close), `ltf_reclaim`
(first LTF bar closing back inside), `ltf_cisd` (close through the delivery
body), `ltf_msb` (close beyond the extreme bar's opposite side), `limit_level`
(retrace back to the swept level), `limit_eq` (retrace to the range midpoint).
Crossed with two targets (opposite extreme, equilibrium) and three stop widths
(the sweep wick, 25% of range beyond the level, 50% beyond).

## Finding 1 — the entry model barely matters. The stop width decides everything.

Cost in R is `2 × slippage ÷ stop distance`. A tight entry produces a tight stop,
which turns a fixed dollar spread into an enormous fraction of R. Gold, 4H
ranges, same model, only the stop changing:

| model / target | stop = wick | 25% of range | 50% of range |
|---|---|---|---|
| ltf_reclaim / opp | −0.650 R | −0.280 R | −0.196 R |
| limit_level / eq | −0.492 R | −0.278 R | −0.180 R |
| htf_close / opp | −0.237 R | −0.177 R | −0.135 R |
| ltf_msb / eq | −0.336 R | −0.249 R | −0.182 R |

Wider is better in every single row. `ltf_reclaim` gets in about three 5-minute
bars after the sweep with a mean R:R of **9.35** and is dead even gross
(−0.0008 R) — then loses **0.65 R** net. The confirmation is fine. The stop it
implies is unaffordable.

## Finding 2 — bigger ranges are better, monotonically

Gold, best model (`limit_eq` / opposite extreme / 50% stop):

| range timeframe | net R | 95% CI |
|---|---|---|
| 30m | −0.204 | — |
| 1H | −0.147 | — |
| **4H** | **−0.070** | [−0.087, −0.054] |
| **D** | **−0.007** | [−0.044, +0.029] |

Same reason: a bigger range means a bigger stop means costs are a smaller share
of R. Daily ranges are the first thing in this project to reach break-even.

## Finding 3 — execution cost, not the model, is the binding constraint

`limit_level` / equilibrium / wick stop, gold 4H, as cost per side changes:

| zero | $0.125 | $0.25 | $0.50 |
|---|---|---|---|
| **+0.579 R** | **+0.043 R** | −0.492 R | −1.563 R |

The same rule is one of the best and one of the worst in this study depending
only on the fill. NAS100 with the same config is **+0.123 R net at full cost**,
because an index spread is proportionally smaller. This is the number worth
measuring on a real account: what a fill genuinely costs decides which entry
model is available.

## Finding 4 — the one positive, cross-instrument result

**`limit_eq` — wait for price to retrace to the range's 50% equilibrium, enter
there, stop 50% of the range beyond the swept level, target the opposite
extreme — on 4H ranges whose candle opened in the 15:00/16:00 SAST bucket (the
New York open).**

| instrument | n | win% | R:R | gross | **net** | 95% CI net | 1st half | 2nd half |
|---|---|---|---|---|---|---|---|---|
| XAUUSD | 402 | 72.6 | 0.49 | +0.080 | **+0.026** | [−0.036, +0.092] | −0.052 | +0.103 |
| XAGUSD | 215 | 76.3 | 0.49 | +0.131 | **+0.069** | [−0.018, +0.149] | +0.134 | +0.004 |
| NAS100 | 188 | 78.2 | 0.48 | +0.150 | **+0.124** | [+0.030, +0.210] | +0.156 | +0.093 |
| US30 | 164 | 74.4 | 0.47 | +0.094 | **+0.061** | [−0.042, +0.157] | +0.101 | +0.021 |
| UK100 | 115 | 82.6 | 0.49 | +0.227 | **+0.148** | [+0.034, +0.252] | +0.139 | +0.156 |
| EURUSD | 185 | 75.1 | 0.49 | +0.118 | **+0.047** | [−0.046, +0.140] | +0.050 | +0.045 |
| GBPUSD | 199 | 72.9 | 0.49 | +0.082 | **−0.002** | [−0.106, +0.099] | +0.048 | −0.052 |

**Gross positive on 7 of 7. Net positive on 6 of 7. Mean +0.068 R.** The same
bucket also leads under `htf_close` / equilibrium / 50% stop, an unrelated entry
model — the only bucket positive on a majority there too.

### Why this should not be sized up yet

Six buckets were compared, so one leading bucket is expected by chance. What is
not expected by chance is the *same* bucket leading across seven instruments and
under two unrelated entry models. That is the strength of it.

The weakness is sample size. Cells are 115–402 trades, and only NAS100 and
UK100 have a confidence interval that excludes zero on their own. Split-half
signs hold on five of seven; XAUUSD and GBPUSD flip between halves. This is the
best-supported result in the project and it is still one forward-test away from
being trustworthy.

## What this changes for the indicator

The entry layer should default to: **retrace to equilibrium, stop half a range
beyond the swept level, target the opposite extreme, 4H or Daily ranges, NY-open
session.** Not because it prints money — mean +0.068 R on a few hundred trades
per instrument is thin — but because it is the only configuration that survived
costs on more than one instrument, and every alternative tested was worse.

The `limit_eq` model only fills about half the setups it arms: price has to come
back to the midpoint. That is a feature. The unfilled half are the setups that
ran away from the level, where the entry would have been chased.
