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
takes them newest-first. Those are the buy setups.

So the levels are a **stack, eaten from the top** — newest-made first, oldest
last. Not nearest-to-price first, and not one counter per direction.

A pullback commonly stops after the 1st or 2nd. That is not a miss — it is the
market failing to break the 3rd, and it is information worth seeing.

## 2. What goes into a stack

A confirmed pivot low is pushed onto the HL stack **only if it is higher than
the one already on top** (or the stack is empty). A pivot that does not extend
the structure is **ignored**. Mirror for pivot highs and the LH stack.

> A level leaves the stack for **exactly one reason: price broke it.**

### The bug that made this rule explicit

A contrary pivot used to **wipe the whole stack** and restart it from that
pivot. Every level still standing was deleted the moment a higher pivot high
printed, so the dashboard read `LH (buys) 1` and the buy side could only ever
produce a 1st Break — never a 2nd, never a 3rd.

On the reported bar (GC1! 30m, 19 Aug 17:00) the three lower highs at 4462,
4428 and 4420 should have marked 3rd, 2nd and 1st. 4428 had already been wiped,
so 4420 marked **1st** and 4462 marked **2nd** instead of **3rd** — one missing
level shifting every number after it.

Reproduced in Python on a synthetic series with the same shape:

```
WIPE (old)    marks = [1st @4416]
NO WIPE (fix) marks = [1st @4416, 1st @4421, 2nd @4429, 3rd @4453, 4th @4463]
```

`LH (buys)` on the dashboard is the diagnostic. While it reads 1 there is
physically nothing for a 2nd break to land on.

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

**Uncapped.** It runs 1st, 2nd, 3rd, 4th, 5th … for as many levels as the move
takes. The count restarts when the impulse resumes — a new higher low (or lower
high) is pushed.

A stack also has to be a real **sequence** before it counts — `minLv`, 2 by
default. A single stray pivot otherwise sits in the stack and every move the
other way prints a spurious "1st Break".

## 5. Seeing it before it happens

*Show the levels still standing* draws a dotted line for every HL / LH nothing
has broken, numbered **1 · 2 · 3 downward from the top of each stack**. Those
numbers are a prediction: the line marked 1 is what the next pullback will
label 1st Break.

If the standing lines sit where the levels would be drawn by hand, everything
downstream follows. If they don't, **swing length** (`pvLen`, 5) is the dial.

## 6. Entry and zone

Both **off by default**, in their own settings group. Neither has been specified
yet; the breaks come first.

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
| **`778th Break`** | the reset required reclaiming a 50-bar extreme, which never happens in a trend | bound the sequence |
| **`RE10026`** coordinate too far from the current bar | an unmitigated zone extended ~20,000 bars; its 50% label sits at the box midpoint, leaving the ~10,000-bar legal range at half the rate the box did | draw zones once at fixed width; clamp every bar index reaching a drawing to 4,000 back |
| The zigzag drawn as a scribble | `ta.pivothigh` / `ta.pivotlow` are independent and confirm `length` bars late; raw pivots were pushed into one list in confirmation order | *(reverted — the zigzag and the HH/HL labels were never asked for and are gone)* |
| `CE10088: Cannot modify global variable "hlOk" in function` ×4 | Pine v6 lets a function mutate a global **array** but not assign to a global **scalar** | run that logic at global scope |
| A whole redesign onto plain fractals, nearest-first, with a cluster merge, a stacked-structure gate and sequence-restart modes | I kept inventing rules instead of fixing the reported defect | rolled back wholesale to the impulse-stack build on request |
| **`LH (buys) 1`; no 2nd or 3rd break ever on the buy side; a genuine 3rd labelled 2nd** | a contrary pivot wiped the entire stack, deleting levels that were still standing | §2 — ignore a pivot that does not extend the structure; a level leaves only when broken |

## 9. What is and is not verified

Prototyped in Python and run against synthetic series before each commit.

Confirmed for the current model:

- the reconstruction of the reported bar gives the full sequence rather than a
  single stray 1st Break (§2);
- ordering is newest-made-first, as specified;
- the count is uncapped, as specified.

**Not verified:** this environment cannot compile or run Pine. Every change is
checked by hand and by the Python port; the port covers the stack, the
structure rule and the ordering — **not** the drawing objects. Line and label
geometry is inspection-only, which is exactly how `RE10026` reached the chart.

**Open item:** the fix has not yet been confirmed on the live chart. The check
is the dashboard — `LH (buys)` should now read more than 1.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
