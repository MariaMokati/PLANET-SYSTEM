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

## 2. What counts as impulse structure

This is the rule everything else depends on, and getting it wrong is what
produced a chart covered in spurious marks.

> A pivot high becomes an impulse **lower high** only once the **next pivot low
> comes in below the previous pivot low**. A lower low is what confirms it.

A pullback high is followed by a **higher** low instead, so it is discarded and
never enters the stack. Mirror for higher lows.

Without that rule, a rally's own pullback highs land in the stack and get
numbered the moment the rally continues. On the reproduction, a down impulse
leaving LHs at 4453 / 4479 / 4499 followed by a wiggly rally produced **six**
events — 4441, 4453, **4467**, 4479, **4491**, 4499 — where three of them are
the rally's own pullbacks. With the rule: exactly three, at the right levels.

The comparison is against the **previous pivot**, not a running all-time
extreme. An all-time extreme goes stale in a drifting market and that side stops
confirming anything at all (3,000 bars produced three buy-side events while the
sell side ran to eleven unbroken).

A stack also has to be a real **sequence** before it counts — 2 levels by
default. A single stray pivot otherwise sits in the stack and every move the
other way prints a spurious "1st Break".

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
| A chart covered in small `1st Break` marks; real levels **"not even marked"**; a genuine 1st labelled 2nd | every pivot high entered the stack, so a rally's own pullback highs were numbered as breaks | §2 — a swing only counts once the next swing confirms it |

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

**Not verified:** this environment cannot compile or run Pine. Every change is
checked by hand and by the Python port; the port covers the structure rule, the
stack and the ordering — **not** the drawing objects. Line and label geometry is
inspection-only, which is exactly how `RE10026` reached the chart.

**The current rule has not yet been confirmed against the reference chart
annotations.** That is the open item.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
