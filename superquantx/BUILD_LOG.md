# SuperQuantX build log

One change per build. Each build names the counter that confirms it.

| Build | File | Change | Confirm by reading | Result |
|---|---|---|---|---|
| b0 | Synthetics | baseline, the file as received | | 2 % win rate reported, undiagnosed |
| **b1** | **Synthetics** | **Reward floor in `Raise()` compared with a 1e-9 tolerance** | **`Refused reward` falls sharply, `Drift rider` n roughly doubles, win rate barely moves** | **pasted and confirmed by the b1 header. NOT TESTABLE on either chart run: Drift rider fired 0 times on both. See b1 result below** |
| **b2** | **Synthetics** | **Repair build. SpikeFade built from the spike not the chart candle; drift pullback measured as a retracement; timeframe guard blocks instead of labelling; m1 history covers the window; per-engine refused counters; expired R out of the totals; sigma off by default** | **Drift rider takes a row with n above zero; Spike fade win rate; `WHY REFUSED` and `ENGINE GATES` blocks** | **shipped, not yet measured** |
| ~~b3~~ | Synthetics | m1 history to cover the analysis window | | **folded into b2** |
| b4 | Alpha | `PanelRows` 50 to 90 | the diagnostics block renders to the last row | queued |
| b5 | Alpha | `net += r.R` moved inside the outcome branches | `Total R` reconciles with `Won / Lost / BE` | queued |
| b6 | Alpha | out of sample, `Window Offset Days = 180` | two screenshots, offset 0 and offset 180 | queued |
| ~~b7~~ | Synthetics | `Refused` counters split per engine | | **folded into b2** |

**Process rule, added after b1.** Name the instrument and the timeframe the
build will be measured on *before* writing it, and check from the previous
panel that the engine being changed is firing on that chart. b1 was spent on an
engine that turned out to fire zero times on both charts that got tested.

---

## b1 result, measured 2026-09-05

Two panels, both reading `b1` in the header, so the paste landed.

### Volatility 10 Index, m5

```
History        54413 bars    189d loaded    180d asked
Window         09 Mar 26     05 Sept 26     180d
RESULTS                                     559 closed
Signals per day                             3,11
Won / Lost / BE     132         427          0
Win rate                        23,6 %
Average trade                               -0,31 R
Total                                       -175,6 R
Expired / open        0                      0
Refused         reward 1027   cap 3050      max 3/day
ENGINES               n         win%        net R
Sigma reversion     559         23,6        -175,6
```

### Boom 600 Index, m15

```
History        30972 bars    323d loaded    180d asked
SPIKES                                      500 seen
Mean interval                               13,5 min
TIMEFRAME TOO HIGH                          use m3
Mean size                                   6,717
Since last            0 bars                0% of mean
Clock                                       waiting
RESULTS                                     93 closed
Signals per day                             0,52
Won / Lost / BE       3          90          0
Win rate                        3,2 %
Average trade                               -1,02 R
Total                                       -94,55 R
Expired / open        0                      0
Refused         reward 1362    cap 70       max 3/day
ENGINES               n         win%        net R
Spike fade           93          3,2        -94,55
```

### What the reading actually says

**b1 is untestable on both charts. Drift rider fired zero times on each.** The
engine tables carry one row apiece and neither is Drift rider. The prediction
written into the b1 row and into PR #7 ("Drift rider n should roughly double")
had no population to act on. The fix is still correct in isolation, proven by
`check_reward_floor.py`, and it remains unverified on live data. Recording it
as unverified rather than as a win.

The process miss: the chart the build would be measured on was never agreed
before the build was spent. Fix for the rest of the queue is in the last
section of this file.

**Boom 600 m15 is the wrong timeframe and the panel says so in red.** Mean
spike interval is 13.5 min against 15 min bars, so roughly one spike sits
inside every bar. `_tfTooHigh` is therefore true, which disables Spike clock by
design (`SpikeOverdue` returns false), and `Since last 0 bars` shows
`_lastSpikeBar` tracking the current bar almost continuously. Every figure
under that red row was measured on a chart the file itself is refusing.

**Spike fade at 3.2 % over 93 trades is inverted, not unlucky.** At the ~2.6R
geometry the engine trades, a directionless entry would land near 25 wins in
93. It produced 3. That is far outside variance and matches the audit's B4
fault exactly: `SpikeFade` takes `sDir`, `sRange` and `_stopPx` from
`Bars[_lastSpikeBar]`, the chart candle, while the spike itself was detected on
m1. On Boom a candle can hold an up spike and still close down, and the engine
then buys a down-drifting instrument. b2 is promoted on this evidence.

**Volatility 10 sigma reversion is a premise error, not a tuning error.** The
shipped geometry is entry at 3.0 sigma, stop at 4.2, target at 0.4 from the
mean, which is 2.6 / 1.2 = 2.17R and a breakeven of 31.5 %. Measured 23.6 %.
A Volatility index is a driftless random walk by construction: no news, no
gaps, no trend. Mean reversion on a driftless random walk has an expectancy of
exactly zero before costs and negative after. `SuperQuantX_Synthetics_Notes.md`
calls reversion "the only edge" on that family. That premise is wrong, and no
parameter change repairs it.

**The daily cap is doing the selecting on Volatility.** `Refused cap 3050`
against 559 taken means sigma generates roughly 26 signals a day and
`MaxPerDay = 3` keeps the first three by clock order, not by quality.

**One thing the reading does confirm.** `Expired / open` is `0 / 0` on both
panels, so the audit's expired-R contamination (Part B, `net += r.R` outside
the outcome branches) has no effect on either total. The -175.6 R and the
-94.55 R are clean figures.

**One thing the panel cannot answer.** `Refused` is a single pair of counters
for the whole file, so the 1362 reward refusals on Boom 600 cannot be
attributed to an engine. Making `Refused` per-engine is now queued as b7,
because without it the counter named as b1's confirmation cannot confirm
anything.

### Next test, no paste required

Isolate the only engine that ever measured positive, on the timeframe where it
measured: Boom 600, h1, Drift rider alone. On h1 every bar swallows several
spikes, so `SpikeFade` cannot fire (`i != _lastSpikeBar + 1` is never true) and
Spike clock stays disabled, which means the engine runs clean without any code
change. Turn off Spike Clock Signals, Spike Fade Signals, Sigma Reversion
Signals and Range Break Signals, leave Drift Rider on, screenshot.

Reference to beat: 43.2 % over 1195 trades, which at 2R is +0.296 R per trade.
If it does not reproduce, this file has nothing and the honest call is to say
so rather than ship b2.

---

## b1, the detail

### What was wrong

`Raise()` refused any signal whose reward multiple fell below `MinRewardR`:

```csharp
if (Math.Abs(target - entry) / risk < MinRewardR) { _refusedR++; Clear(); return; }
```

Drift rider sets `_targetR = DriftR`, and `DriftR` ships at 2.00, the same value as
`MinRewardR`. `Raise()` then builds the target as:

```csharp
target = isLong ? entry + _targetR * risk : entry - _targetR * risk;
```

So every drift signal lands exactly on the floor rather than clear of it.

`target` is `entry + 2.00 * risk` rounded once to a double. Subtracting `entry` back
out returns `2.00 * risk` plus or minus half an ulp of `entry`, and the sign of that
last bit is a coin flip. A bare `<` refused whichever half rounded down.

### Measured

200,000 samples at three instrument scales, in `check_reward_floor.py`:

| Instrument scale | Drift signals silently refused |
|---|---|
| Boom 900, price ~9000, risk 2 to 20 | 49.69 % |
| Boom 600, price ~14000, risk 5 to 60 | 49.67 % |
| Crash 500, price ~4000, risk 1 to 15 | 49.72 % |

Drift rider produced the entire +79.53 R on the Boom 600 h1 reading. Half of its
signals were being discarded for no reason.

### What this does and does not explain

It halves the trade count and the net R of the only engine that measured
profitable. It does **not** explain a 2 % win rate. The discarded half is random
with respect to outcome, so the win rate of the survivors should be unchanged.
b2 and b3 are the candidates for the win rate itself.

### The change

```csharp
double rewardR = Math.Abs(target - entry) / risk;
if (rewardR < MinRewardR - 1e-9) { _refusedR++; Clear(); return; }
```

One part in a billion of a reward multiple, far below any reward difference that
could matter. Nothing that genuinely falls short of the floor survives it.

No trade geometry changes. Every signal that already passed keeps the same entry,
the same stop and the same target.

### Declared, not silent

Two display-only additions alongside the fix:

- the panel header now reads `SUPERQUANTX SYN  b1`, so a screenshot proves the
  paste landed
- a build note at the top of the file

Neither touches a number.

### Deliberately not changed

`SpikeFade` has its own floor at line 706, `if (move / risk < FadeMinR) return;`.
Same class of comparison, but `move` is `FadeTargetShare * sRange`, which is not
built from `risk`, so fade signals do not systematically sit on the boundary.
Left alone to keep b1 to one mechanism.

---

## b2, the repair build

b1 was one change on an engine that turned out never to fire. b2 is the
opposite: it fixes everything the b1 panels actually proved was broken, in one
file, because leaving her pasting one-line fixes into a system where three of
five engines cannot work was the wrong call.

### What each change is, and what confirms it

| # | Change | Kind | Confirmed by |
|---|---|---|---|
| 1 | `SpikeFade` builds direction, stop and target from the **spike**, not the chart candle that contained it | **Confirmed fault.** Audit B4, and the measured 3.2 % over 93 trades | `Spike fade` win rate. Anything near 25 % is neutral, 3 % was inverted |
| 2 | Drift pullback measured as an ATR **retracement** from the drift extreme, not N consecutive counter closes | **Reasoned, but the failure is reproduced.** See sweep below | `Drift rider` n above zero, and the `Drift no pullback / no turn` gate row |
| 3 | Timeframe guard **blocks** both spike engines instead of printing red and trading anyway | Confirmed fault | `Blocked by timeframe guard` count, and the spike engines showing n = 0 on a wrong timeframe |
| 4 | m1 history loads what the window needs, was a flat 60000 bars = 41.7 days | Confirmed fault. Audit B6 | `m1 history  Nd covered` row, red when short |
| 5 | `Refused` counters split per engine | Confirmed gap. Raised by the b1 reading | The `WHY REFUSED` block |
| 6 | Expired trades no longer counted in `Total R` or `Average trade` | Confirmed fault. Audit B2 | `Expired (excluded)` row now carries its own R |
| 7 | `Since last` reports minutes against a minutes mean, was bars divided by minutes | Confirmed fault. Audit B5 | The `% of mean` row now agrees with `ARMED` |
| 8 | `_tfTooHigh` computed always, not only inside `RecordSpike` | Confirmed fault. Audit B9 | The red row appears with zero spikes found |
| 9 | Sigma reversion ships **off**, property renamed so the default reaches a saved chart | **Reasoned from the instrument's construction.** Measured 23.6 % against a 31.5 % breakeven over 559 trades | Turn it back on and it will reproduce the 23.6 % |
| 10 | Every enabled engine takes a panel row even at n = 0 | Usability. This is why b1's zero was invisible | Drift rider visible at zero rather than absent |
| 11 | Drift rider runs first on Boom and Crash | Reasoned. Under a 3/day cap the first engine takes the slots | `Refused cap` per engine |

### Why the old drift rule read zero

Reproduced in `check_drift_rule.py`. Boom drifts down in small uniform steps,
so the grind dominates the noise. The old rule needed three consecutive
counter-drift closes, and that probability collapses as the grind becomes more
uniform. 180 days of m15, one seed:

| drift / noise | up bars | old rule | b2 retracement |
|---|---|---|---|
| 0.3 | 39.8 % | 679 | 9875 |
| 0.6 | 29.1 % | 313 | 11909 |
| 1.0 | 17.7 % | 81 | 14009 |
| 1.5 | 8.4 % | 4 | 15673 |
| 2.5 | 2.4 % | **0** | 16775 |
| 4.0 | 1.8 % | **0** | 16937 |

The zero is not a coincidence and it is not timeframe-specific bad luck. The
old rule is a probability that goes to zero on exactly the instrument property
Boom is built around, which is why it produced 1195 trades on h1 and none on
m15.

### Honest caveat

Change 2 is a rule change, not a parameter tweak, and the win rate of the new
population is **unknown**. The old rule's 43.2 % was measured on h1 on a
different selection. b2 makes the engine fire; it does not promise the
survivors win at the same rate. `Drift Pullback Mode` is switchable back to
`ConsecutiveCloses` so the two can be compared on one chart.

### Checker

```
CHECK  SuperQuantX_Synthetics.cs  1568 lines    0 fail, 0 warn
```

Panel budget: `Rows = 64` against a worst case of about 39, so nothing is
silently truncated the way `PanelRows = 50` truncates Alpha.
