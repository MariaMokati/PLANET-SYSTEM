# Break ranking — spec

`BASIC_BREAKS.pine`. Numbers the breaks of an impulse's structure — **1st
Break**, **2nd Break**, **3rd Break** — the way they get annotated by hand.

---

## 1. The model

An impulse **up** makes higher highs and **higher lows**. Those HLs are the
levels. When the pullback comes it breaks them in **reverse order of creation**:

| what the pullback breaks | number |
|---|---|
| the **last** HL made | 1st Break |
| the **second-last** HL made | 2nd Break |
| the **third-last** HL made | 3rd Break |

Mirror for an impulse **down**: it leaves **lower highs**, and the pullback up
takes them newest-first. Those are the buy setups — which is why "for buys look
at this side" boxes the *down* leg.

So the levels are a **stack, eaten from the top**. Not a list of whatever
happens to be nearest, and not one number per direction of travel.

A pullback commonly stops after the 1st or 2nd. That is not a miss — it is the
market failing to break the 3rd, and it is information worth seeing.

## 2. One alternating swing series first

`ta.pivothigh` and `ta.pivotlow` are **independent**. They do not alternate. A
strong leg happily prints two pivot highs with no pivot low between them, and
they confirm `length` bars late, so they do not even arrive in the order they
formed.

Pushing raw pivots into one list in confirmation order and connecting
consecutive entries is therefore wrong twice over: the zigzag draws as a
scribble (HH followed by HH, near-vertical segments), and every rule downstream
that assumes *a high is followed by a low* is reading garbage. Measured on a
synthetic 3,000-bar series: **10 same-side neighbours out of 79 swings.**

So the first thing built is one strictly alternating series — high, low, high,
low — with **same-side collapse**:

> two highs in a row collapse to the **higher**; two lows to the **lower**.

Same measurement after the fix: **0 faults out of 69 swings.**

Everything else — the zigzag drawing and both stacks — is driven off that one
series. Because it alternates, the previous same-side swing is always exactly
**two back**, which is what makes the structure rule a one-line comparison.

## 2a. What counts as impulse structure

A new swing tells you what the **previous** one was:

> a **higher high** means the low before it was a **higher low** of the impulse
> — that is a sell-side level.
> a **lower low** means the high before it was a **lower high** — a buy-side
> level.

A pullback high is *not* followed by a lower low, so it never enters the stack.
That is the rule that stopped a chart covered in spurious marks: on the
reproduction, a down impulse leaving LHs at 4453 / 4479 / 4499 followed by a
wiggly rally produced **six** events — 4441, 4453, **4467**, 4479, **4491**,
4499 — where three are the rally's own pullbacks. With the rule: exactly three,
at the right levels.

The comparison is against the **previous same-side swing**, not a running
all-time extreme. An all-time extreme goes stale in a drifting market and that
side stops confirming anything at all (3,000 bars produced three buy-side events
while the sell side ran to eleven unbroken).

A level also only enters the stack if it is genuinely higher (or lower) than the
one already on top — the stack has to be monotonic or the "eat from the top"
order is meaningless.

A stack has to be a real **sequence** before it counts — 2 levels by default. A
single stray pivot otherwise sits in the stack and every move the other way
prints a spurious "1st Break".

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
| **The zigzag drawn as a scribble** — HH straight into HH with no low between, near-vertical HH/HL segments, points apparently out of order | `ta.pivothigh` / `ta.pivotlow` are independent and confirm `length` bars late; raw pivots were pushed into one list in confirmation order and consecutive entries connected. 10 same-side neighbours per 79 swings | §2 — build one strictly alternating series with same-side collapse, then drive the zigzag *and* both stacks off it. 0 faults per 69 swings |

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

**The current rule has not yet been confirmed against the reference chart
annotations.** That is the open item.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
