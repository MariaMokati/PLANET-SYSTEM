# Break ranking — spec

`BASIC_BREAKS.pine`. Numbers the breaks of an impulse's structure — **1st
Break**, **2nd Break**, **3rd Break** — the way they get annotated by hand.

---

## 1. The model — fractals, nothing else

Every confirmed fractal **high** is a level. Every confirmed fractal **low** is
a level. No classification, no HH/HL/LH/LL, no deciding which corners are
"allowed" to count.

When price closes **above** the nearest standing fractal high, that is the
**1st Break**. The next one up is the **2nd**. The one above that is the
**3rd**. Mirror downward through the fractal lows.

**Nearest-first**, not newest-first. Price rising physically reaches the
lowest standing high above it first. On a descending run of highs the two
orderings agree, but nearest-first is the one that is always right.

**Swing length is the only dial.** It decides which corners are fractals, and
therefore which levels get numbered. Nothing else filters.

### The two reference setups

From the annotated GC1! 30m chart, both reproduced exactly by the Python port:

| setup | levels | what happens |
|---|---|---|
| impulse up leaves lows 4404 / 4432 / 4443 | pullback down | **1st @ 4443**, **2nd @ 4432**, *"failed to break the 3rd"* at 4404 |
| down leg leaves highs 4430 / 4466 / 4494 | one big candle up | **1st @ 4430**, **2nd @ 4466**, **3rd @ 4494** — all three on that one bar |

A pullback commonly stops after the 1st or 2nd. That is not a miss — it is the
market failing to reach the 3rd, and it is information worth seeing.

## 1a. When does 1st start over

This is a real judgement call and it is a setting, not a silent choice —
*A sequence restarts*:

| option | behaviour |
|---|---|
| **When the move that made them turns** *(default)* | a new fractal forms in the direction of travel, so the pullback has topped. Tightest — a long chop between two breaks restarts the count |
| **When the other side breaks** | the count runs until price breaks the opposite way |
| **Only once the Nth is reached** | loosest — 1st, 2nd, 3rd, then over, however long it takes |

All three agree on both reference setups in §1. They only diverge when a level
is taken long after the one before it, with chop in between. On a reconstruction
of that case (2nd at 4458, a long chop, then 4435):

```
turn    1st 4467 · 2nd 4457 · [restart] 1st 4451 · 1st 4449 · 2nd 4434 · 3rd 4429
other   1st 4467 · 2nd 4457 · 3rd 4451
done    1st 4467 · 2nd 4457 · 3rd 4451 · 1st 4449 · 2nd 4434 · 3rd 4429
```

The annotated chart numbers that late level **3rd**, which is `other`. Left on
`turn` as the default until confirmed on the live chart. All three stay capped
at 3 over 3,000 bars on three regimes.

## 2. The two things that stop it numbering noise

Neither is a classification. Both fall out of the level list itself.

### 2a. Fractals within a few ticks are ONE level

A flat consolidation prints several fractals a few ticks apart. Without
merging they each eat a number, so the 1st and the 2nd land on what is
visually the same level and the real 3rd never gets counted. On the left
reference setup that produced `1st @ 4442, 2nd @ 4442, 3rd @ 4431` — the 4404
level never reached.

*Treat levels closer than (× ATR) as one*, default 0.25. The cluster keeps its
extreme price and the bar where it first formed. Set to 0 to number every
fractal separately.

### 2b. A sequence only STARTS where structure is stacked beyond it

In the annotated chart the impulse from 4365 → 4490 carries **no** break
labels — only "History hl for sell set ups". But the big candle on the right
*is* numbered 1st / 2nd / 3rd. The difference is not HH/HL:

> when the impulse breaks its own last minor high there is **clear air above
> it**; when the pullback breaks 4430 there are still 4466 and 4494 standing
> above.

So: *a sequence only starts where N levels stand beyond the one being taken*
(default 1). Once a sequence has started this no longer applies — the 3rd
still counts even when it is the last level there is.

Without this, the left setup produced three spurious upward "1st Break" marks
up the impulse itself. With it: none.

## 3. What breaks a level

| Confirmation | Break of a high | Break of a low |
|---|---|---|
| Wick beyond | `high > level` | `low < level` |
| **Close beyond** *(default)* | `close > level` | `close < level` |
| Full body beyond | `close >` and `open >` | `close <` and `open <` |

A wick through that closes back inside **is a fake-out** — it breaks nothing.
That falls out of the confirmation test; it is not a separate rule.

Each level is broken once and then leaves the stack.

## 4. The sequence

Runs 1st, 2nd, 3rd and then **stops** (*Stop after the Nth break*, on, N=3).
Anything the pullback takes beyond that is not drawn — the sequence is finished.
Turn the cap off to keep counting 4th, 5th, …

The count restarts when the impulse resumes: a new higher low (or lower high)
is confirmed.

## 5. Seeing it before it happens

*Show the levels still standing* draws a dotted line for every HL / LH the
impulse has left that nothing has broken, numbered **1 · 2 · 3 downward from the
top of each stack**. Those numbers are a prediction: the line marked 1 is what
the next pullback will label 1st Break.

This is the fastest way to tell whether the structure being tracked is the right
one. If the standing lines sit where the levels would be drawn by hand,
everything downstream follows. If they don't, **swing length** is the dial.

## 6. Entry and zone

Both **off by default**, in their own settings group. Neither has been specified
yet; the breaks come first. The entry marks the Nth break at that candle's
close, and the zone spans from the level broken to the next one still standing
behind it.

## 7. Repainting

Pivots are **confirmed** — placed `length` bars back — and with *Only judge
closed bars* on (default) a break is decided once, at bar close, and never
redrawn. The cost is the usual one: a confirmed pivot is only known `length`
bars after it printed.

## 8. Every bug this has hit, and what fixed it

Kept deliberately. Each was a real failure on a real chart.

| Symptom | Cause | Fix |
|---|---|---|
| Nothing marked at all | the stale-level guard ran every bar, retiring each level the instant price reached it | run it only on the bar a level is first assigned |
| `1st Break` twice in a row | the anchor was the minor pivot at the break; a shallow pullback reset the count | *(superseded)* |
| **`778th Break`** | the reset required reclaiming a 50-bar extreme, which never happens in a trend, so nothing ever reset | bound the sequence |
| **`RE10026`** coordinate too far from the current bar | an unmitigated zone extended ~20,000 bars; its 50% label sits at the box midpoint, leaving the ~10,000-bar legal range at half the rate the box did | draw zones once at fixed width; clamp every bar index reaching a drawing to 4,000 back |
| Levels marked were not the hand-marked ones | only the most recent pivot per side was tracked | *(superseded)* |
| A chart covered in small `1st Break` marks; real levels **"not even marked"**; a genuine 1st labelled 2nd | every pivot high entered the stack, so a rally's own pullback highs were numbered as breaks | §2a — a swing only counts once the next swing confirms it |
| **The zigzag drawn as a scribble** — HH straight into HH with no low between, near-vertical HH/HL segments | `ta.pivothigh` / `ta.pivotlow` are independent and confirm `length` bars late; raw pivots were pushed into one list in confirmation order and consecutive entries connected | *(superseded — the zigzag and the HH/HL labels were never asked for and are gone)* |
| `CE10088: Cannot modify global variable "hlOk" in function` ×4 | Pine v6 lets a function mutate a global **array** but not assign to a global **scalar** | run that logic at global scope |
| **The whole HH/HL model was wrong** — marking things she never asked for, missing her real levels | I added a zigzag, HH/HL/LH/LL labels and a classification filter deciding which fractals were "allowed" to be levels. That filter dropped her levels and invented others | §1 — plain fractals, nearest-first, no classification at all. Both reference setups then reproduce exactly |

## 9. What is and is not verified

Prototyped in Python and run against synthetic series before each commit.

Confirmed for the current model:

- the hand-built reproduction of the annotated example gives **exactly**
  1st @ 4453, 2nd @ 4479, 3rd @ 4499 — and nothing else;
- over 3,000 bars on two seeds, both directions fire, counts stay bounded at 8,
  and the distribution is dominated by 1st / 2nd / 3rd.

Two candidate fixes were tried and **rejected by the simulation** before the
right one was found: clearing the stack on a contrary pivot changed nothing at
all, and re-anchoring on a new pivot re-admitted the pullback highs.

The alternating series is measured the same way — `zz.py` counts same-side
neighbours and backwards-in-time segments over the whole series, which is how
the scribble was quantified (10 / 79) and how the fix was confirmed (0 / 69).

**Not verified:** this environment cannot compile or run Pine. Every change is
checked by hand and by the Python port; the port covers the structure rule, the
stack and the ordering — **not** the drawing objects. Line and label geometry is
inspection-only, which is exactly how `RE10026` reached the chart.

**Both reference setups from the annotated chart now reproduce exactly** — see
the table in §1. Not yet confirmed live on the chart at her swing length; that
is the open item, and swing length is the dial for it.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
