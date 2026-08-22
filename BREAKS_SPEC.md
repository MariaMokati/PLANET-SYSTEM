# Break ranking — spec (v2)

`BASIC_BREAKS.pine`. Numbers the breaks of the structure a move is retracing
into — **1st Break**, **2nd Break**, **3rd Break** — the way they get annotated
by hand.

`BASIC_BREAKS_ORIGINAL.pine` is the frozen v1 build, kept untouched as a
reference. v2 was rebuilt from scratch against a single worked example rather
than patched further.

---

## 1. The worked example

GC1! 1h, the decline into 14 Aug 04:00. Marked by hand:

| level | numbered |
|---|---|
| 4419 | **1st Break** |
| 4431 | **2nd Break** |
| 4443 | **3rd Break** |
| 4458 | 4th |
| 4487 | 5th |
| 4498 | 6th |

Made oldest-first 4498 → 4419, taken back newest-first. Everything below is
checked against this.

## 2. What a level is

A **lower high** left behind by the decline. The pool is a strictly
**descending** run: a high that a later, higher high supersedes was never a
lower high in that run and leaves the pool.

**Swing length 1** by default. Several of her zigzag legs are one or two bars
long; at swing length 2 the levels at 4431, 4443 and 4459 are not pivots at all,
never enter the pool, and can never be numbered — only 4420 survives. That was a
real failure, and it is why the default is 1.

### Levels closer than 0.8 × ATR are the same level

A small corner inside a leg *is* a genuine lower high, so no structural rule
removes it — but it takes a number belonging to the real level above it. On the
worked example a corner near 4450, between the 3rd (4443) and the 4th (4459),
put a wrong 4th on the chart and pushed every real level up one.

When a new high lands within the gap of the one on top, the **newer, lower one
supersedes it**. Rejecting the new one instead is backwards — the corner arrives
first and blocks the real level; at 0.8 that dropped 4443 and kept 4450.

The window is narrow and was measured, not guessed:

```
minGap 0.8   with the corner : 4420 · 4431 · 4443 · 4459 · 4490   ← corner gone, the marked five
             the clean five  : unchanged
             the marked six  : unchanged, 4487/4498 survive at 11 pts apart
minGap 1.0   with the corner : 4420 · 4431 · 4443 · 4490          ← too much, eats the 4th
```

## 3. Order

**Newest-made first.** The scan walks down from the newest for the first level
this bar's close is beyond.

## 4. The sequence

- A break is a **close beyond** the level. A wick through that closes back
  inside is a fake-out and breaks nothing.
- **The set closes on the 1st Break.** The pool is frozen as it stands; highs the
  rally makes on its own way up belong to the next leg.
- The count is **uncapped**. Only the first three are **drawn** by default.
- The sequence is **abandoned** if price closes back below the low the rally came
  off. The levels still in it were never broken, so they go **back into the
  pool**, oldest first.

Destroying them instead is what made the 3rd, 4th, 5th and 6th vanish: a
premature sequence earlier in the leg froze them, and abandoning it deleted them
permanently. Only two levels were left for the real rally, giving a 1st and a
2nd and nothing else.

## 5. Entry

On the **2nd break** by default. The signal is made the moment that candle
**closes**; the marker is drawn on the **next** candle — the one actually taken.
Triangle / Circle / Arrow / Diamond, sized, green with **E: Long** or pink with
**E: Short**. Separate `BUY entry` and `SELL entry` alertconditions.

## 6. Zone

Drawn at the **1st Break**, the moment both edges are known.

- **Demand**: top is the 1st Break level, bottom is the low the rally came off.
- **Supply**: the mirror.
- 50% marked through the middle and labelled.

A zone is **retired** the moment price closes through its far edge — the same
value the sequence is abandoned under, so the zone and its invalidation are one
line by construction. Only the last **2 live** zones stay on the chart.

Fixed forward width rather than an open-ended extend: a box running on
indefinitely is what produced `RE10026` in v1.

## 7. Sides

Buy side on, **sell side off by default**. Reading buy setups off a decline, the
mirror marks running down the leg are nothing but clutter.

## 8. Repainting

Pivots are **confirmed** — placed `length` bars back — and a break is judged
once, at bar close. The cost is the usual one: a confirmed pivot is only known
`length` bars after it printed.

## 9. What is and is not verified

Prototyped in Python against reconstructions of the worked example before each
commit. Confirmed: the six-level sequence; the swing-length reproduction; the
abandoned-levels reproduction; the minGap window in §2.

Two candidate fixes were **rejected by the reproduction before shipping** — a
minimum-levels gate (needed 4+ and then dragged a wrong level in as the 3rd) and
restricting the pool to strictly-descending lower highs alone (a no-op on all
three reconstructions).

**Not verified:** this environment cannot compile or run Pine. The port covers
the pool, the ordering, the freeze/abandon lifecycle and the gap rule — **not**
the drawing objects. Line, label and box geometry is inspection-only, which is
exactly how `RE10026` reached the chart in v1.

**Open item:** the minGap fix for the wrong 4th has not been confirmed on the
live chart.

Annotation tool. No claim that an Nth break is more or less likely to continue;
nothing Strategy-Tester validated.
