# Break ranking — spec

`BASIC_BREAKS.pine`. Numbers structural breaks in sequence — **1st Break**,
**2nd Break**, **3rd Break** — the way they get annotated by hand on a chart.

Nothing below is hard-coded. Every rule is a switch, so the layer, the
confirmation, the reset and the numbering can each be set independently.

---

## 1. What counts as a break

A level is *taken out* when the candle clears it by the chosen rule:

| Confirmation | Bearish (a low) | Bullish (a high) |
|---|---|---|
| Wick beyond | `low < level` | `high > level` |
| Close beyond | `close < level` | `close > level` |
| **Full body beyond** *(default)* | `close < level` **and** `open < level` | `close > level` **and** `open > level` |

*Full body* is the strictest: a candle whose body still straddles the level
gets no mark at all. *Close beyond* is the definition already used by the break
layer in `BASIC_CRT.pine` — there, a wick-only poke is a **sweep**, not a break.

Each level fires **once**. Once it has been taken out it is retired, and the
next break in the sequence needs a *new* level to form first. That is what
produces the 1 → 2 → 3 cascade rather than one level being counted repeatedly.

## 2. Which level gets broken — four independent layers

Each layer has its own on/off switch, its own pivot length, its own colours,
its own keep-N, and **its own pair of counters**. Run one or run all four.

| Layer | Level | Character |
|---|---|---|
| **SWING** *(on by default)* | confirmed `ta.pivothigh/low(len,len)`, default len 5 | the main structural layer; confirms `len` bars late |
| **FRACTAL** | the same, on a 2-bar pivot | responsive, many more marks |
| **CANDLE** | the previous candle's high / low | no lag at all, noisiest; long runs of consecutive numbers |
| **PROTECTED** | the last pivot low standing when a new pivot high confirms (and the mirror) | one break per structural leg — fewest, cleanest |

**Stale levels.** A pivot only confirms `len` bars after it printed, so price is
sometimes already past it by the time it exists. With *Ignore levels already
broken when they confirm* on (default) such a level is retired silently instead
of firing an instant break. This check runs **only on the bar the level is first
assigned** — running it every bar would retire every level the moment price
reached it and nothing would ever be marked. CANDLE levels are never stale by
definition and are exempt.

## 3. Direction

Breaks of **lows** and breaks of **highs** run on two **separate** counters,
each independently switchable. A bullish 2nd break has nothing to do with the
bearish count. Turn *Count breaks of HIGHS* off to reproduce a pure sell-off
annotation.

## 3a. The sequence anchor

The anchor is the level the sequence started from. It decides two things: whether
a counter-break is big enough to reset the count, and how far the zone on the 1st
break reaches.

| Anchor mode | Meaning |
|---|---|
| **Extreme of the last N bars** *(default, N=50)* | the highest high (bearish) or lowest low (bullish) of the leg the break came out of |
| The swing at the 1st break | the minor pivot standing at the moment of the break |

This matters more than it looks. With the anchor set to the minor swing, a shallow
pullback inside a sell-off clears it and the next break is numbered "1st" again —
which is what produced two consecutive *1st Break* labels on a 5m gold chart. With
the leg extreme as the anchor, the same sequence numbers 1st → 2nd → 3rd straight
down, which is how it gets marked by hand.

## 4. Resets — when the count goes back to 1st

All four are switchable and all four stack.

| Rule | Default | Behaviour |
|---|---|---|
| Direction flip | **on** | a break of a HIGH clears the bearish count, and vice versa. *Only past the sequence anchor* (default) makes the count survive small counter-moves inside a leg; *Any opposite break* is the blunt version |
| Restart after the Nth | off | count 1…N then start again at 1st; takes precedence over the numbering cap |
| New period | off | both counters clear at each new day / week / session |
| Return through the anchor | off | the anchor is the opposite level standing when the 1st break fired — the high the sell-off started from; closing back beyond it ends the sequence |

## 5. Numbering

Counting continues past the 3rd by default (4th, 5th, 6th…, with correct
ordinals through 11th/12th/13th). Switch *Stop numbering after N breaks* on and
everything past N either draws **without a number** (the line still appears, the
chart stays as clean as a hand annotation) or **stops drawing** entirely.

## 5a. The zone left by the 1st break

A bearish 1st break leaves a **supply zone** above it; a bullish 1st break leaves a
**demand zone** below it. Each is a box with a dashed 50% line and a label.

| Zone spans | Top / bottom |
|---|---|
| **Anchor to the break level** *(default)* | the sequence anchor across to the level that was broken — a leg-sized zone |
| Breaking candle | the high and low of the candle that made the break |
| Broken swing candle | the high and low of the swing candle itself |

The box extends right as price moves. When price closes back through it, it either
**freezes** (default — it stays as history but stops extending), is **deleted**, or
keeps extending. Each layer keeps its own last N zones.

## 6. Visuals

The mark is a horizontal line at the broken level running from the swing that
was broken across to the candle that broke it, with a text-only label
(`style_none`, matching the rest of the repo — no bubbles, no boxes).

Colouring is by **break number** (1st / 2nd / 3rd / 4th+ each their own colour),
by **direction**, or by **level source**. Line end, line style, label text,
label position, label side, ATR offset and size are all inputs. A dashboard
shows the live count per layer for both directions.

## 7. Alerts

`alert()` fires one combined message naming every layer that broke on that bar
and at what price. Five `alertcondition` entries are also exposed: bearish
break, bullish break, 3rd-or-later bearish, 3rd-or-later bullish, any break.

## 8. Repainting

Pivots are **confirmed** — placed `len` bars back — and with *Only evaluate
closed bars* on (default) a break is judged once, at bar close, and never
redrawn. The cost is the usual one: a confirmed pivot is only known `len` bars
after it printed, so the SWING mark arrives late. That lag is exactly why the
FRACTAL and CANDLE layers exist. Turning *Only evaluate closed bars* off flags
breaks mid-bar, and those marks can disappear again before the bar closes.

## 9. What was and was not verified

The counting rules were ported to Python and run against synthetic bar series
before each commit. Confirmed there:

- a staircase down-move produces exactly 1st → 2nd → 3rd on successive lower lows;
- the three confirmation modes fire in the expected order (wick earliest, then
  close, then full body);
- the bullish and bearish counters advance independently on a down-then-up move;
- **the anchor mode changes the outcome as claimed.** On a leg carrying a genuine
  mid-leg bullish break, the swing anchor gives `bear 1 · bull 1 · bear 1 · bear 2`
  — the count restarting — while the leg-extreme anchor gives
  `bear 1 · bull 1 · bear 2 · bear 3`. The first version of this change used the
  swing anchor and did **not** fix the restarting count; the simulation is what
  caught that.

Not verified: the script has **not been compiled in TradingView from this
environment** and has not been run against real market data here. The zone
drawing, mitigation and freezing behaviour is inspection-only — the Python port
covers the counting and the anchor, not the box objects. This is an annotation
tool; it makes no claim that an Nth break is more or less likely to continue. If
TradingView reports a compile error, paste it back and it gets fixed.
