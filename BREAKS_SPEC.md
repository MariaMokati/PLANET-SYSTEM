# Break ranking — spec

`BASIC_BREAKS.pine`. Numbers structural breaks in sequence — **1st Break**,
**2nd Break**, **3rd Break** — the way they get annotated by hand on a chart,
leaves a supply/demand zone behind the 1st, and marks the entry on the 2nd.

---

## 1. The model: a pool of live levels

Every confirmed swing becomes a **live level** and stays live until a candle
closes through it.

There is **one** sequence, not one per direction. Whichever live level is taken
out next is the next number — a high or a low, whichever comes first.

This is the part that took three rewrites to get right. Earlier versions tracked
only the *most recent* pivot on each side. That cannot mark a low made yesterday
which nothing has touched since: by the time price finally reaches it, newer
swings have replaced it as "the current level", so it gets no number at all.
That is why the marked levels did not match the ones marked by hand, and why a
break that was never part of the sequence got numbered.

A pool fixes it directly. On synthetic bars the pool takes out levels up to
**436 bars old** (median 16) — exactly the behaviour a hand annotation shows when
a plunge finally clears a level that had been standing for a day.

### One candle, several levels

A single candle can close through more than one standing level. They are
numbered in the order **price reached them** — nearest the candle's open first —
so a drop through two old lows prints *2nd* and *3rd* on the same bar.

### What feeds the pool

Three switchable pivot sources, each with its own length:

| Source | Default | Character |
|---|---|---|
| **Swings** | on, length 5 | the main source |
| Fractals | off, length 2 | many more levels, many more breaks |
| Major swings | off, length 15 | only the big turning points |

The pool holds at most N levels (default 60); the oldest drops out when full.
Raising it keeps older levels eligible for longer. *Show the live levels* draws
a faint dotted line for everything still standing, so the next possible break is
visible before it happens.

## 2. What takes a level out

| Confirmation | Break of a high | Break of a low |
|---|---|---|
| Wick beyond | `high > level` | `low < level` |
| Close beyond | `close > level` | `close < level` |
| **Full body beyond** *(default)* | `close >` and `open >` | `close <` and `open <` |

A wick through that closes back inside **is a fake-out** — it takes nothing out,
so it neither gets a number nor triggers an entry. That rule is not bolted on;
it falls out of the confirmation test.

Each level is taken out **once** and then leaves the pool.

## 3. The sequence

The count runs 1st, 2nd, … up to **Restart at 1st after the** (default 3), then
starts again at 1st. Optional extras, both off by default because the restart
rule already bounds the count: restart after N quiet bars, and restart on a new
day / week / session.

## 4. Entry

**Entry is the Nth break** (default the 2nd), taken at **that candle's close** —
the close of the candle that takes out the second level. Marked with a label at
the close plus a dotted price line running N bars right.

Long when the level taken out was a high, short when it was a low.

## 5. The zone left by the 1st break

The 1st break leaves the range price just vacated: from the level that was taken
out across to the **nearest level still standing on the other side**. Drawn as a
box with a dashed 50% line and a label — *Supply Zone 50%* when a low was taken
out, *Demand Zone 50%* when a high was.

The zone is drawn **once**, at a fixed width, and never repositioned. That is
deliberate: repositioning a drawing every bar is what produced `RE10026` (see §7).

## 6. Repainting

Pivots are **confirmed** — placed `length` bars back — and with *Only judge
closed bars* on (default) a break is decided once, at bar close, and never
redrawn. The cost is the usual one: a confirmed pivot is only known `length`
bars after it printed. Shorter pivot sources exist for that reason.

## 7. Bugs this has already hit, and what fixed them

Worth keeping, because each one was a real failure on a real chart.

| Symptom | Cause | Fix |
|---|---|---|
| Nothing marked at all | the stale-level guard ran every bar, retiring each level the moment price reached it | run it only on the bar a level is first assigned |
| `1st Break` twice in a row | the anchor was the minor pivot at the break, so a shallow pullback reset the count | *(superseded — the anchor no longer drives the reset)* |
| **`778th Break`** | the reset required reclaiming a 50-bar extreme, which never happens in a trend, so nothing ever reset | bound the sequence: restart after N |
| **`RE10026`** — coordinate too far from the current bar | an unmitigated zone extended for ~20,000 bars; its 50% label sat at the box's midpoint, leaving the ~10,000-bar legal range at half the rate the box did | draw zones once at fixed width; clamp every bar index that reaches a drawing to 4,000 bars back |
| Levels marked were not the ones marked by hand | only the most recent pivot per side was tracked, so an old standing level was never a candidate | the level pool (§1) |

## 8. What is and is not verified

The engine is prototyped in Python and run against synthetic bar series before
each commit. Confirmed there for the current pool model: levels up to 436 bars
old get taken out (median 16); the shared counter cycles across both sides; and
a candle that clears two levels numbers them consecutively.

**Not verified:** this environment cannot compile or run Pine, so every change is
checked by hand and by the Python port before it goes out. The port covers the
counting, the pool and the ordering — **not** the drawing objects. Box
placement, the entry marker and the label geometry are inspection-only, which is
exactly how `RE10026` reached the chart. The pool rewrite has **not yet been
confirmed against the reference chart annotations** — that is the next thing to
check.

This is an annotation tool. It marks what happened. It makes no claim that an
Nth break is more or less likely to continue, and nothing here has been
Strategy-Tester validated.
