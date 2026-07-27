# Which time slots produce the best CRT setups?

Measured, not assumed. Eight instruments, five timeframes, 2010–2026.

## Method

For every candle A, if the next candle B sweeps one of A's levels and closes back
inside, that is a setup — exactly the event the indicator now marks. Entry is B's
close (when the marker appears), stop is B's sweep wick, target is A's opposite
extreme. Price then races: target before stop, within 6 candles (8 on 15m/30m,
5 on D). **A bar touching both is scored a loss** — intrabar order is unknowable
and flattering it would inflate everything.

Each result is attributed to the **range candle's SAST open time**, because that
is what the filter would switch on and off. Double sweeps are excluded (already
measured negative on every timeframe).

Reported in **expectancy per trade, in R, net of costs.** Win rate alone is
meaningless when R:R varies between setups — that trap already produced one false
result in this project.

Two cost levels per instrument, per side: tight (good broker, liquid hour) and
realistic. Gold $0.10 / $0.25. Silver $0.004 / $0.010. NAS100 0.5 / 1.5.
US30 1.0 / 3.0. UK100 0.5 / 1.5. EURUSD 0.4 / 1.0 pip. GBPUSD 0.6 / 1.5 pip.

**4H is rebuilt on the chart's own grid** — a 17:00 New York daily anchor, not the
UTC-aligned grid the raw files use. US daylight saving moves each bucket by an
hour, so every bucket lands on two adjacent SAST labels; both are shown.

**TVC_RUT (Russell) is excluded.** It is a cash index with exchange hours;
overnight gaps make the sweep wick microscopic and the opposite extreme far away,
which inflates R:R and expectancy to untradeable numbers (pooled +0.80 R on 4H
against roughly −0.1 R everywhere else). GER40/DAX was not available in any
accessible dataset.

## Headline

**No slot, on any timeframe, is profitable net of realistic costs on a majority
of instruments. Zero out of 6 on 4H, zero out of 24 on 1H, zero out of 6 by
weekday.**

Pooled net expectancy per instrument, 4H, all slots:

| | XAUUSD | XAGUSD | NAS100 | US30 | UK100 | EURUSD | GBPUSD |
|---|---|---|---|---|---|---|---|
| net R | −0.199 | −0.285 | −0.082 | −0.148 | −0.190 | −0.235 | −0.285 |

This is the same verdict the earlier 6,240-configuration study reached, now
confirmed across seven instruments and sixteen years: **taken mechanically, the
sweep-to-opposite-extreme trade loses after costs.**

## But the ranking is real, and it is large

Mean net R across the seven instruments, 4H, best to worst:

| bucket (SAST, winter/summer) | mean net R | instruments positive |
|---|---|---|
| **11:00 / 12:00 — London PM** | **−0.081** | 1 of 7 |
| 15:00 / 16:00 — NY open | −0.174 | 1 of 7 |
| 23:00 / 00:00 — daily open | −0.178 | 0 of 7 |
| 07:00 / 08:00 — London open | −0.212 | 1 of 7 |
| 19:00 / 20:00 — NY afternoon | −0.286 | 0 of 7 |
| **03:00 / 04:00 — Asia** | **−0.395** | 0 of 7 |

The spread between the best and worst bucket is **0.31 R per trade**, and the
ordering is consistent across seven instruments that share nothing but a clock.
Asia and NY-afternoon ranges are negative on **7 of 7**. That is a far stronger
result than any single cell surviving a significance test, and it is the part
worth acting on.

1H, mean net R across instruments — best and worst five of 24:

| best | net R | | worst | net R |
|---|---|---|---|---|
| 17:00 | −0.161 | | 07:00 | −0.763 |
| 18:00 | −0.232 | | 08:00 | −0.749 |
| 11:00 | −0.247 | | 03:00 | −0.691 |
| 02:00 | −0.267 | | 00:00 | −0.621 |
| 16:00 | −0.290 | | 06:00 | −0.607 |

1H is worse than 4H everywhere: smaller ranges mean smaller stops, and cost
measured in R is `2 × slippage ÷ stop distance`.

Daily, by weekday: Wednesday −0.045 (positive on 3 of 7), Monday −0.055,
Friday −0.103, Tuesday −0.126, Thursday −0.130.

## Selectivity makes it worse, not better

Filtering to setups with R:R ≥ 1.5 — the indicator's old default — lowers
expectancy on **every** instrument:

| | XAUUSD | XAGUSD | NAS100 | US30 | UK100 | EURUSD | GBPUSD |
|---|---|---|---|---|---|---|---|
| all setups | −0.199 | −0.285 | −0.082 | −0.148 | −0.190 | −0.235 | −0.285 |
| R:R ≥ 1.5 | −0.402 | −0.529 | −0.084 | −0.233 | −0.314 | −0.399 | −0.466 |

High-R:R setups have proportionally lower win rates *and* tighter stops, so costs
in R terms are larger. The R:R filter selects the most fragile trades.

## What this means for the feature

Build the slot filter — but as a tool for **muting the ranges that are
consistently worst**, not for finding ones that print money. The honest framing:

- Asia (03:00/04:00) and NY afternoon (19:00/20:00) 4H ranges are the worst on
  every instrument tested, by a wide margin. Turning them off removes the
  weakest third of the population.
- London PM (11:00/12:00) is the least-bad bucket on 4H, on nearly every
  instrument.
- On 1H, the 06:00–09:00 SAST block is consistently the worst of the day.

## What this does not measure

The mechanical rule: every sweep, stop at the wick, target the opposite extreme,
no confluence, no discretion, fixed horizon. Real trading adds a directional
view, structure, and setup selection. The absolute numbers say "do not trade this
blind" — which was already established. **The relative ranking between slots is
the transferable part**, and it is what the filter is built on.
