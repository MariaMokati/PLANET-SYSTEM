# Break ranking — spec

`BASIC_BREAKS.pine`. Numbers the breaks of the structure a move is retracing
into — **1st Break**, **2nd Break**, **3rd Break** — the way they get annotated
by hand.

---

## 1. What is a level

**Every confirmed turning point.** A pivot low goes on the LOW stack, a pivot
high on the HIGH stack. No classification, no significance filter.

> A level leaves the stack for **exactly one reason: price broke it.**

**Swing length is 1 by default** — a turning point needs one clear bar either
side, so a two-bar bounce inside a decline is a level. Anything higher throws
small turning points away: at 5 a bounce needs five clear bars on each side to
exist at all, so it is never a level and never gets numbered. That was a real
failure — hand-marked levels were missing from the chart entirely, not
mis-numbered.

## 2. Which level this bar takes

**Newest-made first.** Levels are not guaranteed to be in price order, so the
top of the stack is not necessarily the one price reaches. The scan walks down
from the newest looking for the first one this bar actually breaks
(`f_nextDn` / `f_nextUp`).

## 3. What one sequence covers

A sequence numbers the turning points of the leg **immediately before** the
move — a rally numbers what the decline left behind. Because levels are eaten
newest-first, that has an exact form:

> Every level in a sequence must be **OLDER** than the one before it. The moment
> a break takes a level **newer** than the last one numbered, the move is taking
> out structure it built itself — a different leg — and the count restarts at
> 1st.

The count is **uncapped**: 4th, 5th, 8th are legitimate when the leg genuinely
left that many levels behind.

### What this replaced

A cross-reset: any break the other way zeroed the count. At swing length 1 a
single small counter-candle fires that and zeroes a sequence mid-move, so a
genuine 3rd prints as a 1st.

Reproduction — a decline leaving tops at 4443 / 4431 / 4419, a rally back
through all three, then the rally breaking its own top:

```
cross-reset   1st 4419 · 2nd 4431 · 3rd 4443 · 4th 4450   ← the rally's own top continues the sequence
leg rule      1st 4419 · 2nd 4431 · 3rd 4443 · 1st 4450   ← new leg, restarts
```

Over 3,000 bars on three regimes the highest number falls from **37th to 16th**
and 4th-or-higher events from **2,400 to 878**.

## 4. What breaks a level

| Confirmation | Break of a high | Break of a low |
|---|---|---|
| Wick beyond | `high > level` | `low < level` |
| **Close beyond** *(default)* | `close > level` | `close < level` |
| Full body beyond | `close >` and `open >` | `close <` and `open <` |

A wick through that closes back inside **is a fake-out** — it breaks nothing.
That falls out of the confirmation test; it is not a separate rule.

A stack also has to be a real sequence before it counts — `minLv`, 2 by default.

## 5. Drawing

Two controls, and only two:

- **Only show breaks from the last N bars** (300) — the clutter control.
  Everything inside the window is numbered and drawn; older marks are cleared.
- **Hide breaks past the Nth** (off) — draw-only. The count still runs.

The drawing cap is a **constant 500**, not a setting. TradingView cannot draw
more than 500 lines or labels anyway, and when it *was* a setting a low value
silently deleted marks that had been drawn correctly — which reads as the
indicator never marking them. That setting is gone.

The **standing-levels preview** (dotted lines for everything unbroken, numbered
outward from price) defaults **off**: at swing length 1 there can be fifty
standing at once.

## 6. Entry

Off by default. On the Nth break (`entAt`, 2), fired the moment that candle
**closes**; the marker sits `entShift` candles later — the candle actually
taken. Marker shape is Triangle / Circle / Arrow / Diamond, with its own size,
plus an editable **E: Long** / **E: Short** label in green / pink. Separate
`BUY entry` and `SELL entry` alertconditions.

## 7. Results (dashboard)

Tracks the same Nth-break signal on whatever timeframe the chart is on —
switching 1m → 30m recalculates from that timeframe's bars, because the
indicator only ever reads the chart series. Reports trades, wins, losses, win
rate, average R per trade, total R, profit factor, and the planned R:R and
stop/target of any open trade.

Scoring needs a stop and a target, so both are settings. Stop defaults to
**structure** — the newest turning point still standing behind the entry.
Target defaults to the **opposite extreme / draw on liquidity**, falling back to
an R multiple when that side is empty.

**This is not a Strategy.** No spread, slippage, commission or partial fills;
one position at a time; stop and target hit in the same bar counts as the loss.
It reports how the signal behaved on those bars and nothing more.

## 8. Repainting

Pivots are **confirmed** — placed `length` bars back — and with *Only judge
closed bars* on (default) a break is decided once, at bar close, and never
redrawn. The cost is the usual one: a confirmed pivot is only known `length`
bars after it printed.

## 9. Every bug this has hit, and what fixed it

Kept deliberately. Each was a real failure on a real chart.

| Symptom | Cause | Fix |
|---|---|---|
| Nothing marked at all | the stale-level guard ran every bar, retiring each level the instant price reached it | run it only on the bar a level is first assigned |
| `1st Break` twice in a row | the anchor was the minor pivot at the break | *(superseded)* |
| **`778th Break`** | the reset required reclaiming a 50-bar extreme, which never happens in a trend | bound the sequence |
| **`RE10026`** coordinate too far from the current bar | an unmitigated zone extended ~20,000 bars; its 50% label sits at the box midpoint, leaving the legal range at half the rate the box did | draw zones once at fixed width; clamp every bar index reaching a drawing |
| The zigzag drawn as a scribble | `ta.pivothigh` / `ta.pivotlow` are independent and confirm `length` bars late | *(reverted — the zigzag and the HH/HL labels were never asked for and are gone)* |
| `CE10088: Cannot modify global variable "hlOk" in function` ×4 | Pine v6 lets a function mutate a global **array** but not assign to a global **scalar** | run that logic at global scope |
| A redesign onto plain fractals with nearest-first ordering, a cluster merge and restart modes | I kept inventing rules instead of fixing the reported defect | rolled back wholesale on request |
| `LH (buys) 1`; no 2nd or 3rd break ever on the buy side | a contrary pivot wiped the entire stack, deleting levels still standing | §1 — a level leaves only when broken |
| Hand-marked levels **missing entirely** | swing length 5 meant small turning points were never turning points | §1 — swing length 1 |
| A deep move stops being numbered part-way | `stackMax` 12, and the OLDEST entry is dropped — in a decline that is the HIGHEST | raise to 50 (max 300) |
| Marks **completely absent** after being drawn correctly | `Keep last N break marks` = 10 deleted all but the ten most recent | §5 — the setting is gone; the cap is a constant 500 |
| Genuine 3rd printing as 1st | cross-reset fired by tiny counter-moves at swing length 1 | §3 — the leg rule |

## 10. What is and is not verified

Prototyped in Python and run against synthetic series before each commit.
Confirmed for the current model: the leg-rule reproduction in §3; the
swing-length and `stackMax` reproductions in §9; ordering is newest-made-first;
the count is uncapped.

**Not verified:** this environment cannot compile or run Pine. Every change is
checked by hand and by the Python port; the port covers the stacks, the level
rule, the ordering and the sequence grouping — **not** the drawing objects.
Line and label geometry is inspection-only, which is exactly how `RE10026`
reached the chart.

**Open item:** the leg rule has not been confirmed on the live chart.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
