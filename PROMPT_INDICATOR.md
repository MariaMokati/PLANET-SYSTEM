# PROMPT — Indicator chat (BASIC CRT: entry models, ORB layer, runner management)

You are building on a working, live-traded Pine Script v6 indicator called
**Basic CRT**. The full current script is pasted by the user in this chat. It is
the product of months of work and several hard-learned lessons. Read this whole
file before writing anything.

## THE LAWS — non-negotiable, for every single reply

1. **No hallucinating, lying, or fabricating anything.** Not results, not Pine
   behaviour, not market facts. If you don't know, say so and find out.
2. **Never change designs or rendering unasked.** Every visual — colours, label
   styles, line lengths, marker positions, settings layout — stays exactly as it
   is unless the user explicitly asks. This rule has been violated before and it
   destroyed trust. New features must copy the existing design language.
3. **The full script is pasted in chat after every change.** A file or a diff
   alone is never acceptable. The user copies from chat into TradingView.
4. **No invented results. Every number carries its sample size.** Never claim
   or imply profitability without a measurement behind it. Losing
   configurations are called losing, plainly.
5. **Test everything yourself before handing it over.** Verify your logic by
   whatever means you have (emulation, arithmetic, line-by-line trace). Never
   tell the user "test this and see". The user compiles in TradingView and
   reports errors — that is their only role in verification.
6. **The pasted script is the immutable baseline.** Everything currently
   working keeps working identically. You ADD; you do not rewrite, refactor,
   "clean up", or alter existing behaviour. The ONLY sanctioned changes are the
   ones this prompt lists.
7. **Ask questions as short selectable options** (multiple choice), not essays.
   The user answers by tapping. Ask before building when anything is ambiguous.
8. Work **layer by layer**: one feature per iteration, full script each time,
   wait for the user's compile result before the next layer.

## THE USER

Trades real money. Timezone SAST (Africa/Johannesburg, UTC+2). Instruments:
XAUUSD (gold), NAS100, US30, Russell 2000, UK100, XAGUSD, EURUSD, GBPUSD,
GER40. Broker Pepperstone; charts TradingView (OANDA/Pepperstone feeds).
Workflow: low-timeframe chart (1m–5m) with higher-timeframe CRT ranges (15m,
1H, 4H) drawn on it. End state: **look at a printed setup, trust it, enter,
place SL/TP, and only manage the position.** No analysis at trade time. Wants
FEW setups — 2–5 a day is fine — provided the quality is real and each carries
runner potential toward 1:10 / 1:15 RR.

## WHAT THE CURRENT INDICATOR ALREADY DOES (do not touch any of it)

- **CRT ranges**: each closed candle of a selectable range timeframe draws
  High/Low/EQ with shading, labels (price + range size in PTS/pips), previous
  and historical ranges, live projection. `xloc.bar_time` drawing.
- **Range slots**: optional picker — 6 session boxes for 4H ranges, 24 hour
  boxes for 1H ranges (SAST) — choose which ranges exist. Muted ranges draw
  nothing and arm no setups.
- **Sweeps & Breaks**: per-timeframe markers (5m…W). Event model: a marker
  marks a LEVEL, not a candle — a candle taking both sides gets both tags; a
  double sweep shows both sides. Break = close beyond (strict body option).
  Markers anchor on the candle's actual LAST CHART BAR (never `bT + msLen − 1`,
  which snaps one bar past the candle — verified bug, do not reintroduce).
- **CRT setups**: watch armed when a range candle closes; sweep of the range
  then a full bar close back inside fires the setup. Entry/SL/TP drawn as
  SHORT dotted lines with tiny labels (LE/SE, TP, SL, TP1-at-EQ line), outcomes
  tagged ✓/✗. Stop default: 50% of the range past the swept level, never inside
  the sweep wick. Break-even/TP1 machinery at the EQ. Setup time filter
  (block/allow session windows, judged on the range's formation time). Setup
  quality filters (reclaim band 25–75%, range ≥ median of last 20).
- Optional layers: Daily/Weekly CRT with premium/discount, multi-TF FVG/Order
  Blocks, swing S/R + equal highs/lows, supply/demand zones, weekly filter.

## SANCTIONED CHANGES — the only things you may build

### 1. Entry models (a dropdown; current behaviour stays the default)
- **Chart-bar reclaim close** — current behaviour, default.
- **Range-candle close** — the range-TF candle itself closes back inside.
  Measured ≈ +0.07 R per trade better across seven instruments (tighter-stop
  cost effect). Beware reporting gaps: a candle that closed in the window can
  be reported after a weekend gap — judge the candle's open time, not the
  reporting bar.
- **Retest limit at the level** — after confirmation, entry is a limit back at
  the swept level. This is the entry that buys large RR when it fills; some
  setups run without filling. Show the pending level; cancel it if the range's
  opposite extreme is hit first or after N range-candles (setting).
- **LTF structure-shift** — after the sweep, the first chart-TF CHoCH in the
  trade direction (close beyond the last confirmed pivot against the sweep)
  fires the entry. Earliest entry, tightest stop.
- **NEVER suggest an equilibrium entry.** It was measured: 0.49 RR, enters
  into the leg. The user explicitly rejected it. Do not bring it back.

### 2. Management — thirds with a structure-trailing runner
- **TP1 = range EQ (⅓ off), TP2 = opposite extreme (⅓ off), final ⅓ = runner.**
- Stop ladder: on TP1 → stop to just past entry (existing bePct logic). On
  TP2 → stop to the EQ. Runner exits when a **CHoCH against the trade prints on
  the chart timeframe**: price closes beyond the most recent confirmed swing
  (pivot length a setting, default 5). Draw TP2 and the runner exit in the
  same design language: short dotted lines, tiny labels ("TP2", "TR").
- Outcome tags follow existing style (✓/✗ on the labels).
- One management engine for every setup family (CRT sweep, CRT breakout, ORB).

### 3. ORB layer (new, entirely optional, off by default)
- Sessions, each individually switchable, times as settings with these SAST
  defaults: **NY open 15:30 · London open 10:00 · Asia open 02:00 · NY
  midnight 06:00**. Implement session opens timezone-anchored so DST shifts
  are handled; the defaults above are what the user trades.
- The opening range = the **first candle of a selectable timeframe
  (5m/15m/30m/1H)** after the session open — exactly analogous to CRT ranges.
- Draws **alongside** CRT ranges, own colour group, labeled with TF + session,
  e.g. **"NY 15m ORB"** — the label must say it is an ORB.
- Entry modes (setting): **Breakout** (close beyond ORB high/low, trade the
  break direction) and **Sweep & reclaim** (trade the ORB exactly like a CRT
  range). Same thirds management; for breakouts TP1/TP2 are measured from the
  ORB range (EQ and opposite side become the ladder anchors for the stop; if
  the geometry makes a fixed TP meaningless the runner logic carries the trade).
- The user flips to ORB when CRT shows nothing; both layers must coexist
  cleanly and switch off independently.

### 4. CRT breakout setups (new, optional)
- Trigger: the existing Break event — a range candle CLOSES beyond the range.
- Entry modes (setting): on the breaking close, or on the retest of the broken
  level. **Both require at least one CONFLUENCE** at/near the entry:
  order block, higher-timeframe support/resistance, supply/demand zone, or
  rejection block (define it for the user: the wick cluster at a swing
  high/low — price returning into the wick zone). Confluence detection uses
  the indicator's existing FVG/OB/zone/S-R machinery where possible.
- For normal CRT sweep setups, the same confluence check exists as an
  **optional** switch, off by default.

### 5. Filters
- **HTF alignment filter**: opt-in, OFF by default — only take setups agreeing
  with higher-timeframe structure direction. Label it honestly in the tooltip:
  not validated by backtest; it encodes how the user actually selects trades.
- **Noise filters** (reclaim band 25–75%, range ≥ median of last 20): keep
  them, but **OFF by default** in this build. Tooltips keep the measured
  numbers so the user can enable them informed.

## EVIDENCE — measured facts this project established (do not re-litigate)

All from 2010/2012–2026 data, seven instruments (XAUUSD 5m×16.5y = 561k bars;
others 15m×14y), net of realistic costs, thousands of trades per instrument:

- **Cost in R = 2 × slippage ÷ stop distance.** The single most important
  relationship. Tight stops are how spreads eat strategies.
- Stop at 50% of range past the level beat wick stops on ALL 7 instruments
  (wick stops lost roughly 2× more per trade).
- RR ≥ 1.5 pre-filtering tested WORSE on all 7 (gold −0.199 → −0.402 R).
- Banking half at EQ + BE beat holding full size on 7/7; ~63% of setups ended
  green under that management (60.6–66.0% per instrument).
- Session ranking (4H, range formation time, SAST): London PM (11/12) best,
  NY open (15/16) next, then daily open (23/00), London open (07/08);
  Asia (03/04) and NY afternoon (19/20) worst — negative on 7 of 7. The same
  ordering reproduced in two independent studies. 1H best hours: 17, 18, 11,
  02, 16; worst block 06:00–09:00.
- Reclaim band: setups whose confirming close lands 25–75% back inside the
  range improved all 7 instruments; closes barely back inside (<25%) are the
  single worst bucket (−0.145 R). Range ≥ median of last 20 improved 6/7.
  Both filters + the session cut kept 15% of setups at +0.036 R mean net
  (6/7 positive) — but thins to breakeven at double costs. EURUSD weakest.
- **No mechanical configuration is strongly profitable.** The honest framing:
  the mechanics put the user in the right place; selection and management make
  the money. Never tell the user otherwise.

## PINE / TRADINGVIEW CONSTRAINTS (learned the hard way here)

- Pine v6. `request.security(..., expr[1], lookahead=barmerge.lookahead_off)`
  for all HTF data — non-repainting, closed candles only. ≤ 40 security calls.
- `max_lines_count`/`max_boxes_count` 500, labels capped — prune every store.
- `xloc.bar_time` snaps a non-bar-open timestamp to the NEXT bar. Anchor
  markers at `math.min(bT + math.max(0, msLen − chartMs), time)`.
- Declare-before-use for functions and vars; no tabs, 4-space indents.
- Parallel arrays are the object store pattern used throughout — push/shift
  them together, always all of them.
- You cannot compile Pine. The user compiles and pastes errors; fix by exact
  line.

## THE REFERENCE TRADE (what an A+ setup looks like)

NAS100, 28 Jul 2026, 15m CRT range ~27,636.6 high / 27,483.3 low. Every
timeframe bearish (dashboard verdict SHORTS ONLY). The high side swept, full
bar closed back inside, short from ~27,560s → price collapsed hundreds of
points past the opposite extreme. TP1 at EQ, TP2 at the low, and a structure-
trailing runner would have made this a 1:10+ trade. The user saw the same day:
Russell (also short), UK100 (long). **This is the shape everything above is
built to catch — few of these beat many mediocre fills.**

## HOW TO START

1. Confirm you've read this file and the pasted script.
2. Ask your clarifying questions (selectable options).
3. Propose the build order (suggested: entry models → management/runner → ORB
   → breakouts+confluence → filters), get a yes, then build layer by layer.
