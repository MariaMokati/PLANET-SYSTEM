# Basic Sessions — exact session open / close marks

A Pine Script **v6 indicator** that marks where every trading session **starts**
and **ends**, at the true minute, in the timezone the session is actually
defined in. Hand-drawn vertical lines drift: they land on whatever bar edge the
mouse was nearest, they ignore daylight saving, and they have to be redrawn by
eye every week. This does not.

File: [`BASIC_SESSIONS.pine`](./BASIC_SESSIONS.pine)

> **HONESTY.** Hand-reviewed against the v6 spec, **not machine-compiled here.**
> If TradingView reports anything on paste, send the message back and it gets
> fixed.

---

## The accuracy claim, stated exactly

Three things are usually wrong with a session marker. All three are handled:

1. **Daylight saving.** Each open is built with `timestamp(tz, y, m, d, hh, mm)`
   — a wall-clock construction — so the UTC offset **for that specific date** is
   resolved by TradingView's timezone database. A 02:00 New York open is 06:00
   UTC in July and 07:00 UTC in January, automatically, with no offset typed in
   anywhere.
2. **Midnight and the two odd nights.** The close is built the same way on its
   own calendar date, derived from a noon probe rather than by adding 24 hours,
   so a window that crosses midnight still ends at the right wall-clock minute
   on the two nights of the year that are 23 and 25 hours long.
3. **Bar edges.** The mark is drawn with `xloc.bar_time` **at that epoch**. A
   09:03 open on a 5-minute chart is drawn three minutes *into* the 09:00 bar,
   not at its edge. A 05:00 close on a 4H chart lands inside the 04:00 candle,
   where it belongs. Nothing snaps to a bar unless you set
   **Anchor = "Bar open"**.

A bar belongs to a session when the bar's own span overlaps the session's span
(`bar_open < session_close and bar_close > session_open`), which is what catches
the sessions that begin or end *inside* a candle.

## When a mark appears vs where it sits

**Where** is always the true epoch. **When** differs slightly: the open mark
appears on the bar that contains it, and the close mark appears as the next bar
opens, because the session's high, low and box are only final then. On a 4H
chart the close mark can land on the chart up to four hours after the coordinate
it is drawn at.

**The alerts do not wait.** Both fire on the tick that passes the true minute, so
a 09:03 open does not alert at 09:00, and a 17:00 close on a 4H chart alerts at
17:00 rather than at 20:00.

## What it draws

| Element | Default | Notes |
|---|---|---|
| Open mark + label | on, solid, width 2 | one per session per day |
| Close mark + label | on, dashed, faded 35% | opens read louder than closes |
| Session box | off | tracks the range live, final at the close |
| Session high / low | off | optional N-bar extension to the right |
| Session midline (EQ) | off | the 50% of the session range |
| Next-transition projection | on, dotted | the close of what is running, or the next open |
| Status table | on, bottom right | open / closed, exact times, live countdown |

The projection sits at the **true future timestamp**, so it is a countdown you
can see on the chart. TradingView only allows drawings up to 500 bars ahead, so
it hides itself when the next event is further away than that.

## The status table is the proof

It prints, for each enabled session: whether it is **OPEN** or closed right now,
the exact open and close of the instance running (or the next one due), in your
chosen clock, and a live `T-minus` countdown to the next transition. If a mark
ever looks wrong, the table says what the script actually thinks the time is —
and the footer names both timezones in play.

## Presets

Three presets fill the six slots. Their windows are written in **New York time**,
which is why `America/New_York` is the default session timezone:

| Preset | Windows |
|---|---|
| ICT killzones | Asia `2000-0000` · London `0200-0500` · NY AM `0700-1000` · NY PM `1300-1600` |
| Classic FX sessions | Sydney `1700-0200` · Tokyo `1900-0400` · London `0300-1130` · New York `0800-1700` |
| CRT models | 1AM `0100-0200` · 9AM `0900-1000` · 5PM `1700-1800` |
| Custom | your own six slots, in any timezone |

The presets overwrite the name, window and on/off of the six slots. Switch to
**Custom** to run your own — colours and day masks stay yours either way.

## Two clocks, on purpose

- **Session timezone** — the clock the windows are *written* in.
- **Label clock** — the clock the times are *read* in.

Define London in `Europe/London`, read it in `Africa/Johannesburg`, and both stay
right through every DST change on both sides. Either can also be typed in as any
IANA zone if it is not in the dropdown.

## Days

Each session has a day mask: `1=Sun 2=Mon 3=Tue 4=Wed 5=Thu 6=Fri 7=Sat`, applied
to the day the session **opens** on — so a window across midnight belongs to its
opening day. A `:days` suffix typed inside the window box itself
(`0200-0500:23456`) overrides the mask.

## Alerts

Create **one** alert on the script with condition **"any alert() function call"**.
Messages name the session, the event, the exact time and the symbol:

```
London OPEN 02:00 · XAUUSD 5
```

Turn *Alert on session open* / *close* off to mute either side.

## Settings worth knowing

- **Keep last N of each session** (default 8) — old sessions are pruned whole,
  oldest first, so the script never quietly overflows TradingView's limit of 500
  lines / 500 labels / 500 boxes. Six sessions with open + close marks cost about
  12 drawings per instance.
- **Hide above this chart timeframe** (default 240 minutes) — session marks on a
  daily chart are noise, so above 4H they disappear and the table says so. Set 0
  to never hide.
- **Anchor** — `Exact session time` (the point of the script) or `Bar open` for
  the old, snapped-to-a-candle-edge look.

## What it will not do

- **It does not invent bars.** A session with no bars inside it — a holiday, a
  Friday-night Asia window, an instrument that simply is not trading — is not
  marked at all, because nothing happened in it.
- **It does not repaint.** Every mark is a pure function of the clock: no price
  input, no `request.security`, no lookahead. A mark on a historical bar sits at
  the same epoch it was drawn at in real time.
- **It does not do range analytics.** Box, high/low and midline are there as
  optional context, not as a study. Range work lives in `BASIC_CRT.pine`.

## Cross-platform status

- **v1.0.0 — Pine Script v6.** This file. Shipping now.
- **v1.1.0 — MQL5 port.** Not written yet. The port is not a copy-paste: MQL5 has
  no IANA timezone database, so DST has to be reconstructed from the US and EU
  changeover rules against the broker's server offset, and the two changeover
  weekends have to be tested explicitly. That is a build of its own — say the
  word and it gets built and matched mark-for-mark against this file.

## If TradingView complains

Paste the exact message back. The file is hand-reviewed, not compiler-verified
here, and the fastest fix is the literal error text.
