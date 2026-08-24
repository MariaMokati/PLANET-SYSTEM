# Prompt — reading price action and market structure with Heikin Ashi

A paste-ready prompt for **Claude on the web**. It asks for one thing: how to
*read* a chart using Heikin Ashi candles — every candle type by its wicks, every
transition sequence, basics through expert — written in the simplest English
possible and delivered as a white-background, vibrant PDF.

Instrument context: **XAUUSD**, the timeframes she actually trades (D1, 4H, 1H,
15m, 5m). Descriptive, not a strategy: it teaches reading, not entries.

---

## How to use it

1. Open the XAUUSD chart with Heikin Ashi switched on.
2. Optionally screenshot a stretch of trend and a stretch of chop and attach both.
3. Paste everything inside the fenced block into Claude on the web.

---

## The prompt

```text
Teach me to read price action and market structure using HEIKIN ASHI candles, on
XAUUSD (spot gold), across D1, 4H, 1H, 15m and 5m.

I am not a beginner at charts, but I am new to reading Heikin Ashi properly. I
already read structure with normal candles. I want Heikin Ashi as a second lens
that tells me what normal candles hide: who is actually in control, and when that
control is changing.

═══════════════════════════════════════════════════════════════════
SECTION 0 — HOW TO WRITE THIS
═══════════════════════════════════════════════════════════════════

W1. SIMPLEST ENGLISH POSSIBLE. Short sentences. No jargon unless you define it in
    the same sentence, in plain words. If a nine-year-old could not follow the
    sentence, rewrite it.

W2. Every concept gets three things, in this order:
      (a) what it IS — one or two plain sentences
      (b) what it MEANS about buyers and sellers — plain sentences
      (c) what I DO with it — a concrete instruction
    Never give (a) without (b) and (c).

W3. Show, do not just tell. Wherever a candle shape matters, draw it — an inline
    SVG or a simple ASCII sketch — so I can see the shape, not just read about it.

W4. No hype. Do not call anything a holy grail, a secret, or an edge. Heikin Ashi
    is a smoothing tool. Say plainly what it cannot do.

W5. Every claim about what a shape "usually means" must be labelled as either
    MECHANICAL (it follows from the maths of how the candle is built — always
    true) or CONVENTIONAL (it is how traders read it — a convention, not a law).
    This distinction matters more than anything else in the document. Do not blur
    it.

═══════════════════════════════════════════════════════════════════
SECTION 1 — LEVEL ONE: THE BASICS
═══════════════════════════════════════════════════════════════════

1.1 What a Heikin Ashi candle actually is. Give me the four formulas and then
    translate each one into a plain English sentence:
      HA close  = average of this bar's open, high, low and close
      HA open   = average of the PREVIOUS HA candle's open and close
      HA high   = the highest of: this bar's real high, HA open, HA close
      HA low    = the lowest of: this bar's real low, HA open, HA close
    Then explain, simply, WHY these formulas make the chart look smoother.

1.2 The single most important warning, stated early and stated hard: the HA close
    is NOT the real price. The HA candle's high and low are NOT the real high and
    low of that bar. Explain what this means for me practically:
      - I can never place an entry, a stop or a target off a Heikin Ashi level
      - the real candle is the only source of real price
      - anything I mark on the chart must come from the real candles
    Say exactly how to work with both: HA for reading, real candles for levels.

1.3 The lag. Because HA open uses the PREVIOUS candle, every HA candle is partly
    built from the past. Explain in plain English what that costs me: turns show
    up late. Quantify it as best you can and say on which timeframes the lag hurts
    most and least.

1.4 The colour rule, plainly: green/blue = HA close above HA open, red = below.
    Then say what colour alone does and does not tell me.

═══════════════════════════════════════════════════════════════════
SECTION 2 — THE CANDLE TYPES. THIS IS THE HEART OF THE DOCUMENT.
═══════════════════════════════════════════════════════════════════

Give me a COMPLETE catalogue of Heikin Ashi candle types, sorted by their wicks
and their bodies. For EACH type give me all of this:

    - a drawn picture of the shape
    - a plain-English name I can actually remember
    - the exact definition (what the wicks and body are doing)
    - MECHANICAL or CONVENTIONAL — what the maths forces versus what traders read
    - what it says about buyers and sellers
    - what it says about the TREND it appears in
    - what I do next
    - what would make me wrong about it

Cover at minimum every one of these, and add any I have missed:

  BODY-ONLY SHAPES
  2.1  Green candle with NO LOWER WICK (flat bottom) — strong up control
  2.2  Red candle with NO UPPER WICK (flat top) — strong down control
  2.3  Green with no lower wick AND a big body — the strongest up bar there is
  2.4  Red with no upper wick AND a big body — the strongest down bar there is

  WICK-ON-ONE-SIDE SHAPES
  2.5  Green candle that GROWS a lower wick — explain why this is the first crack
  2.6  Green candle with a long UPPER wick — sellers pushing back at the highs
  2.7  Red candle that grows an UPPER wick
  2.8  Red candle with a long LOWER wick — buyers stepping in underneath
  2.9  Wicks on BOTH sides — what two-sided wicks mean about control

  BODY-SIZE SHAPES
  2.10 Bodies getting BIGGER bar after bar — expansion
  2.11 Bodies getting SMALLER bar after bar — contraction, the classic warning
  2.12 Small body with long wicks both sides — the HA doji / spinning top
  2.13 A body so small it is nearly a line — full indecision

Then give me a SINGLE SUMMARY TABLE of every type: picture, name, wick rule,
what it means, what I do. One row per type. This table is the page I will
actually print and keep next to my screen.

Answer these two questions explicitly and separately, because they are the ones I
actually asked:
    Q1. What does a wick on the UPPER side mean — in an uptrend, and in a
        downtrend? Are those two different answers? Say so.
    Q2. What does a wick on the LOWER side mean — in an uptrend, and in a
        downtrend? Same treatment.

═══════════════════════════════════════════════════════════════════
SECTION 3 — THE SEQUENCES. THE PATTERNS I MUST WATCH FOR.
═══════════════════════════════════════════════════════════════════

This is the section I care most about. I want to know what it means when the
COLOURS CHANGE — not one candle, but the run of them.

For every sequence below give me: a drawn picture of the run, a plain name, what
is happening underneath, whether it usually continues or reverses, what confirms
it, what kills it, and what I do.

  3.1  A long run of greens, then ONE red, then greens again
       (the single-candle pullback inside a trend)
  3.2  A long run of greens, then TWO reds, then greens again
  3.3  Greens, then THREE OR MORE reds — where the line is between a pullback and
       a real change, and how I tell the difference EARLY
  3.4  Two greens, one red, then multiple reds — the rolling-over sequence
  3.5  Multiple reds, one single green, then a red again — the failed bounce
  3.6  Multiple reds, one green, then MULTIPLE greens — the real turn
  3.7  Alternating red-green-red-green with small bodies — chop, and how to
       recognise it fast enough to stop trading it
  3.8  A run of greens whose BODIES SHRINK while the colour stays green — the
       quiet warning that comes BEFORE the first red
  3.9  A run of greens where LOWER WICKS start appearing — the wick warning, and
       whether it comes before or after the shrinking bodies
  3.10 The first candle of a new colour that has NO wick on the trend side — the
       strong-flip signal
  3.11 A red candle inside an uptrend that is BIGGER than the greens around it
  3.12 Two-sided wicks appearing at the very top of a long green run

Then rank every sequence above from "most often continues the trend" to "most
often turns the trend", and be honest that this ranking is a reading convention
and not a measured statistic unless you have actually measured it.

═══════════════════════════════════════════════════════════════════
SECTION 4 — LEVEL TWO: HEIKIN ASHI AND MARKET STRUCTURE TOGETHER
═══════════════════════════════════════════════════════════════════

4.1 How do I read structure — higher highs, higher lows, break of structure,
    change of character — when the candles are Heikin Ashi? State clearly which
    parts of structure I must ALWAYS read from real candles instead, and why.

4.2 Give me a clean, simple rule for the split. Something like: HA tells me
    WHO IS IN CONTROL; real candles tell me WHERE THE LEVELS ARE. Expand that
    into a practical working method I can follow every session.

4.3 Multi-timeframe. Explain how to stack D1 → 4H → 15m with Heikin Ashi:
    what each timeframe's HA colour and shape tells me, what it means when they
    agree, and what it means when they disagree. Give me a simple table of the
    combinations and what each one means.

4.4 Heikin Ashi at a level. When price arrives at a level I have drawn from real
    candles (a previous high, a previous low, a session high or low), what HA
    shapes tell me the level is holding versus breaking? Be specific about the
    shapes.

4.5 Sessions. Does HA read differently during the Asian range, the London open
    and the New York open? Say what changes and what does not. If you cannot
    answer this from anything solid, say so rather than inventing a rule.

═══════════════════════════════════════════════════════════════════
SECTION 5 — LEVEL THREE: EXPERT
═══════════════════════════════════════════════════════════════════

5.1 Where Heikin Ashi FAILS. Give me the full honest list:
    - ranging and chopping markets
    - gaps and the Sunday open
    - thin liquidity hours
    - very fast news candles
    - the lag at a sharp V reversal
    For each, say what the chart will look like and how I avoid being fooled.

5.2 The stop-loss trap. Because HA wicks are not real wicks, spell out exactly how
    a trader gets hurt placing stops off an HA chart, with a worked example using
    made-up but realistic gold numbers. Label the numbers as illustrative.

5.3 How to compensate for the lag. Practical methods only.

5.4 What HA is genuinely good at, stated narrowly and honestly. And what it is not
    better at than plain candles.

5.5 A decision checklist. A short, ordered list of questions I ask myself in
    order, every time I look at the chart. It must be short enough to memorise.

═══════════════════════════════════════════════════════════════════
SECTION 6 — THE WORKED EXAMPLE
═══════════════════════════════════════════════════════════════════

I am attaching a screenshot of XAUUSD in an uptrend. In it, the red candles are
few and small, and the up moves arrive as runs of large green candles.

6.1 Read that chart back to me, candle group by candle group, in plain English,
    using the exact vocabulary you built in Sections 2 and 3. Name each shape and
    each sequence by the name you gave it.

6.2 Say what the chart was telling me BEFORE each push, and whether it was
    readable in advance or only obvious afterwards. Be honest when it was only
    obvious afterwards — that is the most useful thing you can tell me.

6.3 Point out anything in the picture that does NOT fit the tidy rules from
    earlier sections. The exceptions teach me more than the examples.

If the screenshot is not readable enough to do this properly, say so and tell me
exactly what to screenshot instead. Do not guess at candles you cannot see.

═══════════════════════════════════════════════════════════════════
SECTION 7 — WHAT IS NOT TRUE
═══════════════════════════════════════════════════════════════════

Mandatory section. List the things commonly claimed about Heikin Ashi that are
NOT true, or that are true only in a much narrower way than people say. Include
at minimum:
    - "Heikin Ashi removes noise" — what it actually does instead
    - "Green means buy, red means sell"
    - "HA candles filter out false signals"
    - "You can trade HA the same way you trade normal candles"
    - any claim that HA has a measured edge
State plainly that everything in this document is a way of READING a chart, not
evidence that any of it makes money, and that none of it has been backtested here.

═══════════════════════════════════════════════════════════════════
SECTION 8 — DELIVERABLE
═══════════════════════════════════════════════════════════════════

D1. Produce this as a PDF document. Not just chat text — an actual downloadable
    PDF.

D2. Design rules, no exceptions:
    - WHITE background (#FFFFFF). Never dark. Never grey-corporate.
    - Vibrant accent colours: deep purple, coral, teal, amber, emerald, magenta.
    - Semantic colour: green for bullish/confirmed, red for bearish/warning,
      amber for caution, purple for structure and brand.
    - Clean sans-serif type. Sentence case headings. Weights 400 and 500 only.
    - Emojis in section headers where they genuinely help me scan, never as
      decoration.
    - Every candle shape DRAWN, in colour, next to its explanation.

D3. Structure the PDF with a contents page, numbered sections matching the
    numbering above, and the Section 2 summary table on its own page so I can
    print that page alone.

D4. Put the biggest warning — HA price is not real price — on its own callout
    early in the document, in a coloured box I cannot miss.

D5. If the document is going to be long, that is fine. Do not compress it. If you
    must split the response, stop at a section boundary, say which sections are
    still pending, and continue when I say continue.

Start with Section 1. Do not summarise the whole thing first.
```

---

## Notes on choices made in this prompt

- **MECHANICAL versus CONVENTIONAL (W5)** is the spine of the document. Some
  Heikin Ashi facts follow from the formulas and are always true — a green candle
  with no lower wick means the HA open was the lowest of the three inputs, full
  stop. Most of what is written about Heikin Ashi online is instead a reading
  convention. Forcing the label on every claim is what stops the document turning
  into folklore.
- **The "not real price" warning is repeated three times** (1.2, 5.2, D4) because
  it is the one mistake that costs money rather than accuracy: an HA wick is not a
  place where price traded, so a stop placed off one sits at a price that never
  existed.
- **Section 3 is the sequences.** The transitions asked about — one red inside a
  green run, two greens then a red then multiple reds, multiple reds then a lone
  green — are each named separately so the answer cannot fold them into one
  generic "trend change" paragraph.
- **3.8 and 3.9 come before the colour flip.** Shrinking bodies and the first
  appearing lower wick both happen while the run is still green. If Heikin Ashi
  gives early warning anywhere, it is there, so they are asked for explicitly.
- **Section 7 is mandatory.** A long document about a smoothing tool will produce
  confident-sounding rules. Requiring the disconfirmed list in the same PDF is
  what keeps the rest honest.
