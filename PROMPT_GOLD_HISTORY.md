# Prompt — XAUUSD candle history, Daily and 4H, Jan 2025 → today

A paste-ready prompt for **Claude on the web** with the XAUUSD chart open in a
browser tab. It asks for one thing only: **what the candles actually did.** No
strategy, no entries, no signals, no indicator. Description of behaviour, measured.

Scope of this prompt: **Daily first, then 4H.** Lower timeframes (1H, 30m, 15m, 5m)
are deliberately left for follow-up prompts so this one stays inside a single run.

Timestamps are reported in **SAST and New York, side by side**, on every table.

---

## How to use it

1. Open **XAUUSD** in a browser tab (TradingView, your broker, or wherever the
   data is) and leave that tab open.
2. Paste everything inside the fenced block below into Claude on the web.
3. If you also have CSVs, attach them — the prompt tells Claude which source wins.

---

## The prompt

```text
You are analysing the price history of XAUUSD (spot gold). I have the chart open
in a browser tab. Your job is to describe — in exhaustive, measured detail — how
this instrument has actually behaved from 1 January 2025 up to today.

This is a HISTORY AND BEHAVIOUR study. It is not a strategy request. Do not
propose entries, stops, targets, signals, indicators, or a system. Do not tell me
what to trade. Describe what the candles did.

═══════════════════════════════════════════════════════════════════
SECTION 0 — HARD RULES. READ THESE BEFORE YOU START.
═══════════════════════════════════════════════════════════════════

R1. EVERY number you state must come from data you actually read. If you did not
    read a bar, you do not have a number for it. Never estimate, never fill a gap
    from memory, never carry a figure over from general knowledge about gold.

R2. Before any analysis, state exactly what data you obtained:
      - the source (which tab, which feed/exchange, e.g. OANDA:XAUUSD, or which
        attached file)
      - the timeframe of each series
      - the FIRST bar timestamp and the LAST bar timestamp
      - the total bar count
      - the timezone the source reports in, and the daily-candle anchor it uses
    If you cannot read the tab, say so plainly and stop — ask me for a CSV export
    instead. Do NOT substitute recalled prices. A refusal is correct; invented
    data is not.

R3. Every claim gets its sample size. Write "37 of 412 days (9.0%)", never
    "often", "usually", "tends to". If n < 20 for a bucket, print the number and
    label it LOW SAMPLE — do not draw a conclusion from it.

R4. Separate FACT from READING. Facts are counts, percentages, dollar ranges,
    dates. Readings are your interpretation of them. Put readings in their own
    clearly-labelled paragraph, never inside a stats table.

R5. If two of your own figures disagree, say so and show both. Do not silently
    pick one. If something is not measurable from the data you have, write
    "NOT MEASURABLE FROM THIS DATA" and move on.

R6. Do not round away the truth. Gold moves in dollars — give me dollars to two
    decimals and percentages to one.

R7. Where a source is attached AND the tab is readable, the attached file wins
    for price; the tab is used only to confirm the feed matches. Report any
    disagreement between them explicitly, with the dates and the size of the
    difference.

═══════════════════════════════════════════════════════════════════
SECTION 1 — TIME CONVENTION
═══════════════════════════════════════════════════════════════════

T1. Report every timestamp TWICE, side by side, in this order:
    SAST (Africa/Johannesburg, UTC+2, no DST) | NY (America/New_York, DST-aware).
    Every table that carries a time carries both columns. No exceptions.

T2. State up front which daily-candle anchor the data uses:
      - 17:00 New York (the broker/TradingView convention), or
      - 00:00 UTC (the convention most raw CSV exports ship with)
    These produce DIFFERENT daily candles with different highs, lows and closes.
    Name the one you are using in the header of every daily table.

T3. New York observes DST; SAST does not. That means the NY session moves by one
    hour against SAST twice a year. List the exact changeover dates inside the
    study window and show how each affected session bucket boundaries. Do not
    average across the shift without saying you did.

T4. Session windows (state these back to me, in both clocks, before using them):
      Asia        — Tokyo open through the Asian range
      London open — the London opening hour and the two hours after
      NY open     — the New York opening hour and the two hours after
      NY PM       — after the NY AM window through the daily close
    If your data lets you compute the exact boundaries, use them and show them.
    If you must approximate, say which boundary you approximated and by how much.

═══════════════════════════════════════════════════════════════════
SECTION 2 — PERIOD
═══════════════════════════════════════════════════════════════════

P1. Full window: 1 January 2025 → today's date (state today's date explicitly).

P2. Analyse the full window first.

P3. Then repeat the headline measurements on the MOST RECENT TWO MONTHS on their
    own, as a separate section. I want to see whether what was true across the
    whole window is still true now, or whether the behaviour has changed. Show
    the two figures next to each other: full window vs last two months, with the
    difference. Say clearly whether each pattern held, weakened, or reversed.

P4. Also break the full window into calendar quarters and show the same headline
    figures per quarter, so drift over time is visible rather than averaged away.

═══════════════════════════════════════════════════════════════════
SECTION 3 — PHASE ONE: THE DAILY CANDLE
═══════════════════════════════════════════════════════════════════

Do this phase completely before you touch 4H.

3.1 ANATOMY — one table, one row per statistic, full window:
    - number of trading days
    - up days / down days / doji-close days (close within 0.05% of open), counts
      and percentages
    - mean, median, min, max of: daily range (H−L) in dollars and in %
    - mean, median of: body size, upper wick, lower wick — in dollars AND as a
      percentage of that day's range
    - mean and median absolute close-to-close move
    - the 10 largest-range days and the 10 smallest-range days, with dates,
      OHLC, and range

3.2 OPEN-TO-CLOSE STRUCTURE — for every day, classify and count:
    - close above open vs below open
    - where the close landed inside the day's range, bucketed by decile
      (0–10% = closed on the low, 90–100% = closed on the high)
    - days that closed in the top 25% of range; days that closed in the bottom 25%
    - days whose open was also that day's high (no upside continuation at all)
    - days whose open was also that day's low
    - days where the open sits within the middle 20% of the range

3.3 WICK AND SWEEP BEHAVIOUR — measured, not assumed:
    - how many days took out the PREVIOUS day's high, the previous day's low,
      BOTH, or NEITHER — counts and percentages
    - of the days that took out the previous day's high: how many CLOSED above it
      versus closed back below it. Same for the low. This is the whole
      sweep-versus-break distinction, and it must be reported as a count, not a
      characterisation.
    - of the days that took out BOTH previous high and previous low: which side
      was taken FIRST (use intraday data to establish order — if you cannot
      establish the order, say NOT MEASURABLE, do not guess), and where the day
      closed
    - the same four measurements against the previous WEEK's high and low
    - days that took out the previous day's high and then closed below the
      previous day's LOW (full reversal days) — list every one with its date

3.4 SEQUENCE AND STREAK BEHAVIOUR:
    - distribution of consecutive up-day and down-day runs (how many runs of 1,
      2, 3, 4, 5+, each direction)
    - after two consecutive up days, what happened on day three — counts
    - after a day that closed in the top 10% of its range, what did the next day
      do — counts, both directions
    - after a day whose range was in the top 10% of all ranges, what was the next
      day's range, relative to the median
    - inside days (high < previous high AND low > previous low): how many, and
      what the day after an inside day did
    - outside days (high > previous high AND low < previous low): how many, and
      what the day after did

3.5 CALENDAR BEHAVIOUR:
    - by weekday (Mon–Fri): count, mean range, up/down split, mean absolute move
    - by month: same figures, one row per calendar month in the window
    - first trading day of the month, last trading day of the month: behaviour
      versus the all-days baseline
    - Sunday/Monday opening gaps: how many, mean and max gap size in dollars,
      how many filled the gap the same day, how many the next day

3.6 VOLATILITY REGIME:
    - split the window into regimes by 20-day average true range (or 20-day mean
      daily range if ATR is not available — say which you used)
    - name the date boundaries of each regime you identify, and the mean daily
      range inside it
    - state which regime we are in NOW, and how the current 20-day figure
      compares to the window's own median

3.7 ANOMALIES — an explicit, itemised list. For each one give the date, the OHLC,
    what made it anomalous, and how far outside normal it sat (in multiples of
    the median, or in standard deviations — say which):
    - every day whose range exceeded 3× the median daily range
    - every day whose close-to-close move exceeded 3× the median
    - every gap larger than the 95th percentile gap
    - every cluster of 3+ consecutive days that all broke the same directional
      extreme
    - every day that broke a multi-month high or low
    - any day where the behaviour contradicts a pattern you reported in 3.3 or
      3.4 — I specifically want the exceptions listed, not smoothed over
    For each anomaly, if you can identify the macro event behind it (CPI, NFP,
    FOMC, geopolitical), name it and label it clearly as EXTERNAL CONTEXT, not as
    something derived from the price data. If you cannot, write "no identified
    catalyst" rather than inventing one.

3.8 WHERE INSIDE THE DAY THINGS HAPPENED — this needs intraday data; if you only
    have daily bars, write NOT MEASURABLE and say what data you would need:
    - in which session did the day's HIGH form: Asia / London open / London /
      NY open / NY PM — counts and percentages
    - in which session did the day's LOW form — same
    - how often the high AND the low both formed in the same session
    - how often the extreme formed in the first hour of London, and the first
      hour of New York, specifically
    - how much of the day's total range was delivered inside each session, as a
      percentage — mean and median
    - what percentage of days had already set their high or low before the
      London open, and before the New York open
    Report all of this with both SAST and NY time labels on the session columns.

═══════════════════════════════════════════════════════════════════
SECTION 4 — PHASE TWO: THE 4-HOUR CANDLE
═══════════════════════════════════════════════════════════════════

Only start this once Phase One is complete and written out.

4.1 State the 4H grid you are using. A 17:00 New York daily anchor produces six
    4H candles per day at fixed offsets; a UTC grid produces a different six.
    Print the six bucket start times in BOTH clocks, and note that US daylight
    saving moves each bucket by one hour against SAST — so each bucket appears
    under two adjacent SAST labels. Show both labels.

4.2 Per-bucket anatomy — one row per 4H bucket (six rows), full window:
    - count of candles
    - mean and median range in dollars
    - up/down split
    - mean body as a percentage of range
    - mean upper wick and lower wick as a percentage of range
    - percentage of the parent day's total range delivered by this bucket

4.3 Which bucket does the work:
    - which 4H bucket contained the DAY's high — counts by bucket
    - which contained the DAY's low — counts by bucket
    - which bucket had the largest range of its own day — counts by bucket
    - which bucket most often reversed the day's direction established up to
      that point

4.4 4H sweep-and-close behaviour, the same measurement as 3.3 but on 4H:
    - how many 4H candles took out the previous 4H candle's high / low / both /
      neither
    - of those, how many closed back inside versus closed beyond
    - which bucket produces the most take-outs, and which the most closed-back-
      inside outcomes — broken down per bucket, with counts
    - what the NEXT 4H candle did after each case

4.5 The two opens, in detail. Treat these as their own sections:
    LONDON OPEN
      - behaviour of the 4H candle covering the London open, versus all others
      - how often it takes out the Asian session high, the Asian session low,
        both, neither
      - how often it closes back inside the Asian range after doing so
      - mean range of that candle versus the all-bucket median
    NEW YORK OPEN
      - the same four measurements, against the London session's high and low
      - how often the NY-open candle extends the day's existing direction versus
        reverses it — counts
      - how often the day's high or low is set within that candle
    Both sections carry SAST and NY times in every table header.

4.6 4H sequences:
    - consecutive same-direction 4H runs — distribution of run lengths
    - what follows a 4H candle that closed in the top/bottom 10% of its range
    - inside and outside 4H candles: counts, and what followed

4.7 4H anomalies — same itemised treatment as 3.7, with dates and bucket labels.

═══════════════════════════════════════════════════════════════════
SECTION 5 — SYNTHESIS
═══════════════════════════════════════════════════════════════════

5.1 THE PATTERN LIST. A numbered list of every repeatable behaviour you measured.
    One line each, in this exact shape:
      [n] <the behaviour> — occurred X of Y times (Z%), full window; A of B (C%)
          in the last two months. STATUS: held / weakened / reversed.
    Order the list by how strongly the behaviour deviates from a coin flip, not
    by how interesting it sounds.

5.2 THE ANOMALY LIST. Every anomaly from 3.7 and 4.7 in one chronological table:
    date | SAST time | NY time | timeframe | what happened | size vs normal |
    catalyst or "none identified".

5.3 WHAT IS NOT TRUE. This section is mandatory and it is the point of the whole
    exercise. List every behaviour that is commonly assumed about gold — or that
    LOOKED true early in your own analysis — that the data does NOT support.
    Give the number that kills it. If a pattern is within noise of 50/50, say so
    and put it here rather than dressing it up in section 5.1.

5.4 SAMPLE-SIZE AND CONFIDENCE NOTE. State the total bar count behind each
    phase, name every bucket where n < 20, and state plainly that a study of one
    market over roughly twenty months is a description of THIS period and not
    proof of a persistent property.

5.5 WHAT YOU COULD NOT MEASURE. List every measurement above that you had to mark
    NOT MEASURABLE, and state exactly what data would be needed to complete it.

═══════════════════════════════════════════════════════════════════
SECTION 6 — OUTPUT FORMAT
═══════════════════════════════════════════════════════════════════

F1. Markdown. Tables for all figures. No prose paragraphs where a table works.

F2. Follow the section numbering above exactly, so I can find anything by number.

F3. Every table header states: timeframe, timezone convention, daily anchor,
    date range, and n.

F4. Do not summarise the study at the top. Lead with the data-provenance block
    from R2, then work through the sections in order. Any overall assessment goes
    at the very end, after 5.5.

F5. If the response is going to be truncated, STOP at the end of a numbered
    section, say "Section X complete, sections Y onward pending — say continue",
    and wait. Never compress a later section to fit. I would rather read it in
    three messages than get a thinned-out version in one.

F6. Do not congratulate the analysis, do not call anything institutional-grade,
    do not describe any finding as an edge. Report the numbers.

Begin with the Section 0 / R2 data-provenance block. Do not analyse anything
until you have told me exactly what data you are holding.
```

---

## Notes on choices made in this prompt

- **Provenance first (R2).** Web Claude reading a chart tab can silently fall
  back on recalled prices. Forcing it to declare feed, first/last bar and bar
  count before it analyses anything is the cheapest defence against that, and
  makes a bad run obvious in the first paragraph rather than the tenth table.
- **Sweep versus break stated as a count (3.3, 4.4).** Whether the candle *closed*
  back inside the level is the whole classification; asking for it as two counts
  rather than a description is what keeps the answer checkable.
- **Both clocks on every table (T1).** SAST has no DST and New York does, so the
  session buckets drift against each other twice a year. T3 forces those dates to
  be named instead of averaged through.
- **Last two months as its own section (P3).** Reported against the full-window
  figure with an explicit held / weakened / reversed verdict, so a pattern that
  has already stopped working cannot hide inside a twenty-month average.
- **Section 5.3 is mandatory.** Any long analysis will produce a list of things
  that look true. Requiring the disconfirmed list in the same output is what makes
  the confirmed list worth reading.
- **Stop-and-continue (F5).** A single web response will not hold all of this.
  Better a clean break at a section boundary than every section thinned to fit.
