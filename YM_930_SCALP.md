# YM 9:30 Opening Break — "The Boring Scalp"

An FXR Script custom indicator for **FX Replay**. Marks two levels before the
New York open, arms both sides, takes the first break on the 9:30 candle, and
stops for the day.

File: [`YM_930_SCALP.fxr.js`](./YM_930_SCALP.fxr.js)

> **HONESTY.** This is a drawing and bookkeeping tool. It places no orders and
> makes no profitability claim. It has been driven against synthetic bars, not
> against live FX Replay data — the strategy logic is verified, the host API
> bindings are reconstructed (see *Known unknowns*). Two intrabar situations
> are genuinely unknowable from OHLC and are resolved conservatively; both are
> called out below rather than buried.

---

## The 7-step checklist, and where each step lives

| # | Rule | Encoded as |
|---|------|-----------|
| 1 | YM only, 1-minute time frame | `Enforce 1-minute chart` — the indicator draws nothing on any other interval |
| 2 | Mark the swings at 9:29 a.m. ET, wicks not bodies | `Level source` (two modes, below) |
| 3 | Stop orders at both levels | `Buy Stop` / `Sell Stop` plots, live from 9:29 |
| 4 | Stop loss 10 ticks, fixed | `Stop loss (ticks)` |
| 5 | Take profit 30 ticks, fixed 3:1 | `Take profit (ticks)` |
| 6 | Enter ONLY on the 9:30 candle, never 9:31 | trigger window is the 9:30 bar and nothing else |
| 7 | One trade a day, first break only | `dayFinished` latches the moment anything happens |

---

## The one ambiguity in the source material

Step 2 says *mark the swings at 9:29*. Step 6 says *a 9:29 break kills that
side*. Those two only both make sense under one reading, so the indicator ships
both and you pick:

**`9:29 Candle High/Low`** — the levels are the 9:29 bar's own wick high and
wick low. Simplest, zero judgement, matches "mark two levels, set your orders,
the market either triggers you or it doesn't." Under this reading the
"9:29 break" clause is a no-op, because a candle cannot break its own extremes.

**`Swing Pivots (frozen at 9:29)`** — the levels are the most recent *confirmed*
swing high and swing low before 9:29 opened, using `Pivot lookback` bars either
side. Here the 9:29 clause bites: if price takes out a level during the 9:29
candle, that side is dead and only the other side can trigger at 9:30.

Pivots are confirmed `lookback` bars after the fact, so they **never repaint**.
Pivot comparison is strict on both sides — a neighbour that merely *equals* the
candidate disqualifies it. Without that, a flat stretch of bars registers every
member as a pivot and the real swing gets overwritten by the newest chop.

---

## The two conservative calls

**Both stops fill inside the 9:30 candle.** OHLC records no ordering, so we
cannot know which side was hit first. `If both levels break on the 9:30 candle`
defaults to **skip the day** — the honest answer. Set it to *Follow candle
direction* to infer from the close instead, and know that you are guessing.

**Stop and target both sit inside one bar.** Assumed to be the **stop**.
Fixed-R systems flatter themselves when scored the other way, and a backtest
that reads better than reality is worse than useless. This one is deliberately
not an input.

---

## Timezone

The 9:30 open is a New York wall-clock event, so it lands on a different UTC
hour in summer than in winter. Rather than trust an ambient timezone, the
indicator derives the Eastern offset directly from the US DST rule in force
since 2007 — second Sunday of March, first Sunday of November. No library, no
host dependency.

That conversion was checked against the real IANA `America/New_York` database
at ~57,000 sample points spanning 2024–2028, plus every DST switchover instant
at 5-minute resolution across a ±3 hour window. Zero mismatches.

---

## Inputs

**Setup** — `Level source`, `Pivot lookback` (5), `If both levels break on the 9:30 candle`

**Risk (ticks)** — `Tick size` (1.00 for YM and MYM), `Stop loss` (10),
`Take profit` (30), `Order offset` (1)

`Order offset` is how far *above* the high the buy stop rests, and below the low
for the sell stop, since a stop order has to sit beyond the level to be a stop
order. Set it to 0 to rest exactly on the level.

**Safety** — `Enforce 1-minute chart` (on), `Show levels before the open` (on)

**Style** — one colour per plot.

---

## What it draws

Everything renders through `plot.line`, so values read straight off the legend
and there is nothing to clean up between sessions.

- `Buy Stop` / `Sell Stop` — the resting orders, from 9:29 until one triggers
- `Entry` / `Stop Loss` / `Take Profit` — only once a break has triggered

All five go blank the moment the trade resolves. One trade a day means the
chart should be quiet by 9:32.

---

## Known unknowns

FX Replay's FXR Script reference is at
`custom-indicators.gitbook.io/custom-indicators-docs`, which was unreachable
from the build environment. The API surface here was reconstructed from the
starter template and public examples: `init`/`onTick`, `indicator({onMainPanel,
format})`, `input.int|float|bool|str|color`, and `plot.line(title, value,
color)`.

Two consequences worth knowing:

1. **The OHLC and timestamp bindings are feature-detected, not assumed.**
   `readBar` and `toEpochMs` probe for candle objects, arrays of candles,
   accessor functions, and globals; time is accepted as epoch seconds, epoch
   milliseconds, a `Date`, or a moment-like object. All six shapes are covered
   by tests. If the host uses a seventh, that is the one place to patch.
2. **Input parameter order mirrors the starter template exactly** — `input.bool`
   and `input.color` are called with the group in slot 4, `input.int`/`float`
   with tooltip then group in slots 7 and 8, `input.str` with options, tooltip,
   group. If a label lands in the wrong place it is cosmetic, not functional.

Zone rectangles were left out on purpose. `rectangle()` exists but its exact
signature could not be confirmed, and a guessed drawing call that throws would
take the whole indicator down with it. Lines carry the whole strategy anyway.

---

## Not tested on ES, NQ, or US30

The source material is explicit about this. `Tick size` defaults to 1.00, which
is correct for YM and MYM and wrong for most other things.
