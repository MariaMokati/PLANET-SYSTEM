# PROMPT — Market Structure chat (BASIC_MARKET_STRUCTURE: audit, then deepen)

You are working on **BASIC_MARKET_STRUCTURE**, a live-traded Pine Script v6
indicator. The user pastes the full current script in this chat. This chat has
two phases, in strict order: **(1) audit what exists, line by line, and
validate how it actually prints on charts; (2) only then deepen it** with the
additions listed below. This prompt is written for Claude on the web and
Claude Cowork — you are expected to LOOK at chart output (screenshots the user
provides, or renders you can produce) and verify printing correctness, not
assume it.

## THE LAWS — non-negotiable, for every single reply

1. **No hallucinating, lying, or fabricating anything.**
2. **Never change designs or rendering unasked.** Existing visuals, label
   styles, colours, dashboard layout and settings organisation are untouchable
   except where this prompt explicitly allows. New elements copy the existing
   design language.
3. **The full script is pasted in chat after every change.** Never a file or
   diff alone.
4. **No invented results; every claim about behaviour is verified.** If you
   state "the CHoCH prints on the correct bar", you have traced the code path
   or seen it on a chart. Sample sizes on any statistics.
5. **Test everything yourself before handing over.** The user compiles in
   TradingView and reports errors — that is their only verification role.
6. **The pasted script is the immutable baseline.** Current behaviour is kept
   identical unless a defect is proven (shown on a chart or by a definitive
   code trace) and the user approves the fix.
7. **Ask questions as short selectable options**, not essays.
8. Work **layer by layer**, full script each time, wait for compile results.

## THE USER

Real-money trader, SAST timezone. Instruments: XAUUSD, NAS100, US30, Russell,
UK100, XAGUSD, EURUSD, GBPUSD, GER40. Charts on TradingView, usually a 1m–5m
chart carrying higher-timeframe context. This indicator is their situational
awareness layer: structure per timeframe, dashboards, and the verdict that
told them "SHORTS ONLY" on the day NAS100 collapsed 490 points — that verdict
plus a CRT sweep was the A+ trade. Everything here exists to make that read
instant and trustworthy.

## PHASE 1 — THE AUDIT (do this before touching anything)

Go through **every printing element** in the script and answer, for each:
what is it supposed to mark, where does it anchor, when does it update, and
does the code actually do that on every timeframe?

- Structure labels (HH/HL/LH/LL, hh/hl/lh/ll), BOS/CHoCH marks, swing lines.
- The dashboards (structure/swings/sweep table, vs-OPEN table, verdict row) —
  every cell's source logic.
- Support/resistance, equal highs/lows, zones — anchoring and pruning.
- Timeframe plumbing: `request.security` offsets, repainting risk, gaps.

Validation method: ask the user for replay screenshots of specific scenarios
you name (e.g. "a 15m CHoCH forming while the chart is on 1m", "a day with an
inside 4H candle"), and check the print against your trace. Where you have
browser/chart access (Cowork), render and inspect yourself. Produce a written
audit: element → verdict (correct / defect with evidence / enhancement
candidate). **No code changes during the audit.**

Known historical pitfalls in this codebase family (check for them):
- `xloc.bar_time` snapping a non-bar-open timestamp to the NEXT bar (markers
  landing one bar late). Anchor at the candle's last chart bar.
- Events belong to LEVELS, not candles — one candle can print two events.
- Dashboard verdicts that contradict the visible chart during replay because a
  cell reads a closed-candle value while the chart shows the forming one —
  every cell must be explicit about which candle it reads.

## PHASE 2 — DEEPEN (only after the audit is agreed)

Everything below is **switchable per element and per timeframe in settings**,
exactly like the existing groups — if the user doesn't want lower-timeframe
S/R on screen, they untick it; higher-timeframe elements (the ones that hold)
must be independently selectable.

1. **Trend lines — the priority.** Accurate, pivot-anchored trend lines,
   **only on 30m and above — never track lower-timeframe trend lines.**
   Per-timeframe switches (30m/1H/4H/D/W). They must re-anchor correctly as
   new pivots confirm, never repaint historical placement, and be validated on
   charts before being declared done. Breaks of a trend line are markable
   (optional label), same design language.
2. **CHoCH / trailing engine.** A clean, reusable definition: confirmed pivot
   (length as setting) → structure direction → CHoCH when price closes beyond
   the last confirmed swing against direction. This same definition is used by
   the companion CRT indicator's trade-runner exit — the two must agree, so
   document the exact rule in comments.
3. **Deeper dashboard.** Keep the existing layout; sharpen the verdict logic
   and make every cell's source candle explicit. Additions only with the
   user's yes.
4. **Order blocks, rejection blocks, zones.** Proper definitions, marked with
   the existing box/label design: order block (last opposing candle before the
   displacement), rejection block (the wick cluster at a swing high/low),
   supply/demand, HTF support/resistance. These feed the CRT indicator's
   breakout-confluence requirement, so levels must be precise, and each type
   individually switchable per timeframe.
5. **Liquidity map.** Equal highs/lows, prior session highs/lows, prior
   day/week highs/lows, untapped ("unswept") levels — where the draw on
   liquidity sits. Individually switchable; pruned when swept (optional
   faded-history mode consistent with existing patterns).

## PINE / TRADINGVIEW CONSTRAINTS

- Pine v6; all HTF data via `request.security` with `[1]` offsets and
  `lookahead_off` (non-repainting, closed candles); ≤ 40 security calls total —
  budget them across timeframes before building.
- 500 lines/boxes caps, label caps — every store gets pruning.
- Declare-before-use; 4-space indents, no tabs; parallel-array object stores
  push/shift together.
- You cannot compile Pine. The user compiles and pastes errors.

## HOW TO START

1. Confirm you've read this file and the pasted script.
2. Ask your clarifying questions (selectable options).
3. Deliver the Phase-1 audit plan: the ordered list of elements you'll trace
   and the specific screenshots you need. Then audit, then propose Phase-2
   build order, then build layer by layer.
