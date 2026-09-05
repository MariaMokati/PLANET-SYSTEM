# SuperQuantX build log

One change per build. Each build names the counter that confirms it.

| Build | File | Change | Confirm by reading | Result |
|---|---|---|---|---|
| b0 | Synthetics | baseline, the file as received | | 2 % win rate reported, undiagnosed |
| **b1** | **Synthetics** | **Reward floor in `Raise()` compared with a 1e-9 tolerance** | **`Refused reward` falls sharply, `Drift rider` n roughly doubles, win rate barely moves** | **not yet measured** |
| b2 | Synthetics | `SpikeFade` to build direction, stop and target from the m1 spike, not the chart bar | `Spike fade` win rate | queued |
| b3 | Synthetics | m1 history to cover the analysis window | `SPIKES seen`, and Drift rider n falling as the overdue block starts working | queued |
| b4 | Alpha | `PanelRows` 50 to 90 | the diagnostics block renders to the last row | queued |
| b5 | Alpha | `net += r.R` moved inside the outcome branches | `Total R` reconciles with `Won / Lost / BE` | queued |
| b6 | Alpha | out of sample, `Window Offset Days = 180` | two screenshots, offset 0 and offset 180 | queued |

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
