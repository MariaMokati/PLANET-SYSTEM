# Basic Breaks — spec

Numbers the breaks of the structure a move is retracing into — "1st Break",
"2nd Break", "3rd Break" — the way they get annotated by hand.

Two Pine files:

| file | what it is |
| --- | --- |
| `BASIC_BREAKS.pine` | the live build |
| `BASIC_BREAKS_ORIGINAL.pine` | **frozen.** The v1 build, byte-identical apart from two `\n` escapes that were lost when it was pasted through chat and had to be restored for it to compile. Never edit it. |

## History of the build, so the dead ends stay dead

1. **v1** — every `ta.pivothigh` / `ta.pivotlow` becomes a level; break newest-first.
   Frozen as `BASIC_BREAKS_ORIGINAL.pine`.
2. **v2** — rewritten from scratch against one worked example: a strictly
   descending pool of lower highs, an ATR gap rule, a frozen set, demand zones.
   Then extended with body-based levels (a wick is not a high; a turn needs two
   opposing candles). **Rolled back at her request** — v2 is in this branch's
   history only. Two things worth keeping from it:
   - a wick on its own is not a high, and a break is a body closure beyond
   - `hiRight[pvLen + 2]` — indexing a **user-defined** variable by an
     input-derived offset — is a Pine **runtime** error. The script compiles,
     the settings show in the status line, and every line, label and box is
     silently dropped. `max_bars_back` on `indicator()` covers built-in series
     only and does not help. **Never index a user-defined variable by an input.**
3. **v3, current** — an accurate alternating swing structure, plus a filter that
   decides which sequences are actually setups.

## v3 — the structure

`ta.pivothigh` and `ta.pivotlow` are two independent series. **They do not
alternate.** Inside one leg three pivot highs can print with no pivot low
between them, and v1 put all three on the stack as separate levels where
structure has one. That is what made a genuine 2nd print as a 3rd.

The structure now alternates:

- a swing **high** only follows a swing **low**, and vice versa
- a more extreme point arriving while the leg is still running **replaces** the
  swing standing — a higher high inside an up leg is the same swing, moved
- a swing already broken is spent; a new pivot after it starts a new swing

Each swing is classified against the previous swing **on its own side**:

| | above the last one on its side | below it |
| --- | --- | --- |
| swing high | `HH` | `LH` |
| swing low | `HL` | `LL` |

`lhOnly` restricts buys to `LH` and sells to `HL`. **Off by default** — the `HH`
that topped the decline is normally the *last* number in the sequence (the 6th,
in the worked example), so dropping it loses the far end of the leg.

## v3 — the breaks

Unchanged from v1 in spirit:

- the move takes structure back **newest first** — last swing high = 1st Break
- a break is a **close beyond** (configurable: wick / close / full body)
- a level leaves for one reason only: **price broke it**
- the count **restarts at 1st** when a break takes a swing *newer* than the last
  one numbered — that is the move eating structure it built itself
- the count is uncapped; **only the first three are drawn** by default

## v3 — which sequences are setups

Measured on her 5m XAUUSD chart, v3 without a filter produced **786 trades,
37.8% wins, profit factor 0.9**. Two causes:

**Every two-bar wiggle was a setup.** A sequence is now judged once, at its 1st
Break, on the size of the leg it is taking back — the distance from that level to
the extreme the move came off, in ATRs (`minLegAtr`, default 2.5). Below the bar
it still eats its levels, because they *were* broken and leaving them standing
forever would be wrong; it is simply not drawn, not entered and not counted.
That is also what takes the mess off the chart. `qTrend` is a second, optional
gate requiring structure to have turned first (`HL` for buys, `LH` for sells).

**The target was the furthest level away.** `tgtMode` defaulted to the full draw
on liquidity on *every* sequence, including two-bar ones. A target several times
further away than the stop cannot win half the time. Default is now the
**nearest** unbroken level the other side — the next thing actually in the way.
The old behaviour is still selectable.

Structure detection is untouched by all of this. Every turning point, however
small, is still read. The filter decides what is *tradable*, not what *exists*.

### Tune on profit factor, not win rate

They pull against each other: moving the target closer raises the win rate and
shrinks each win. Profit factor above 1.0 makes money at any win rate; tuning on
win rate alone can push it past 50% and still lose. The dashboard shows both,
plus a skipped-sequences count so the filter's effect is visible.

## Defaults that exist because something broke

| default | why |
| --- | --- |
| `pvLen = 1` | every turning point matters no matter how small. At 2 the small legs are not pivots at all and can never be numbered. |
| drawings capped at 500, not a setting | TradingView cannot draw more than 500 lines or labels. When this *was* a setting, a low value silently deleted marks that had been drawn correctly, which read as the indicator failing to mark them. |
| `hidePast = true` | 1st / 2nd / 3rd on the chart; 4th+ still counted, alerted and in the dashboard, just not drawn. |
| structure labels and zigzag **off** | nothing gets drawn that was not asked for. |
| sell side on, buy side on | both, but `lhOnly` off so neither side is over-filtered by default. |
| `f_xc()` clamps to `bar_index - 4000` | a drawing coordinate more than ~10,000 bars back is `RE10026`. |
| constant history offsets only | see the v2 runtime error above. |

## Verification limits

This environment cannot compile or run Pine, and has no price data. Rules are
prototyped in Python against reconstructions before shipping — that is how the
swing-length failure, the abandoned-levels failure and two wrong candidate fixes
were caught. It does **not** cover drawing-object geometry, and it cannot measure
a win rate: the dashboard on the chart is the only measurement.

Annotation tool. No claim that an Nth break is more or less likely to continue;
the results are indicator-side, with no spread, slippage, commission or partial
fills, one position at a time.
