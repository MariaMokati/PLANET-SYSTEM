# SuperQuantX audit

Required first output per `SuperQuantX_HANDOVER_BRIEF.md` section 10. Both files
read line by line. Static checker rebuilt at `check.py` and run on both. Nothing
was changed while producing this.

```
CHECK  SuperQuantX_Alpha.cs       5162 lines    1 fail, 11 warn
CHECK  SuperQuantX_Synthetics.cs  1345 lines    0 fail,  0 warn
```

Every row names a specific method or field. Anything that could not be evidenced
is in Part C as unverified.

---

## Part A: working correctly

| Component | File | Evidence it is correct |
|---|---|---|
| `Calculate` / `_lastClosed` walker | Both | Walks to `index - 1` only. A bar is processed once, after close. No repainting path exists |
| Zero lookahead | Both | Checker found 10 forward-offset reads in Alpha, all of form `Bars.X[k + d]` where `k = i - N` and `d <= N`, so `k + d <= i`. Four carry an explicit `k + d > i` guard (`EqualLevels` 2516, `SwingLow` 3785, `SwingHigh` 3802, `RawSwing*` 4016 and 4033). Synthetics has zero forward-offset reads anywhere |
| History loader | Both | `Initialize` computes `DaysToBars(AnalyseDays + WindowOffsetDays) + 1200` and loops to 400 attempts. The v45 fix is intact in both files |
| Stop tested before target | Both | `UpdateTrades`: `if (hitStop) ... else if (hitTgt)`. Alpha 3900, Synthetics 949 |
| Dealing cost on every exit | Both | `RAt` subtracts `t.Cost`. Break-even exits return `-t.Cost / t.Risk`, not zero |
| Engine independence | Alpha | `_conts` built by `IsBigBar` with its own params, separate from `_elephants`. `_capUsed[src]`, `_lastSrcBar[src]`, `_srcStreakCount[src]` all per-source. `UseGlobalCap` and `GlobalSpacing` both default false |
| HTF bias reads closed bars only | Alpha | `BiasOf` finds the first `OpenTimes[k] < now` then uses `idx = k - 1` |
| Live preview is inert for numbers | Alpha | `LiveTriggerOne` draws `SQXLIVE` and returns. Nothing reaches `_trades` or `_log` |
| Greyed panel R accounting | Alpha | `DrawConsoPanel` 4783 sums `won` and `lost` only for outcome 1 and outcome not-3. The one panel that gets expired trades right |
| Alpha reward floor is float-safe | Alpha | `MinR` is used only as `rMult` in `BuildRisk`. There is no post-construction re-test, so B3 cannot occur here |
| Spike stats built outside the window | Synthetics | `ProcessBar` calls `UpdateSpikes(i)` before the `WindowStart` / `WindowEnd` return, so the mean interval is stable before scoring opens |
| Compile hygiene | Both | Brace, paren and bracket balance clean. 27 enums in Alpha and 3 in Synthetics, every member reference resolves. No duplicate `[Parameter]` display name inside a group. Every `DefaultValue` inside its `MinValue..MaxValue`. `[Indicator]` present on both classes. No em dashes anywhere in either file |

---

## Part B: faults found

| Component | File | Fault | Impact on numbers | Fix |
|---|---|---|---|---|
| `PanelRows = 50` vs `DrawPanel` | Alpha 1489 | `Row()` guards `if (r >= PanelRows) return;`. Full layout needs up to 79 rows. Everything past index 49 is written nowhere | **Confirmed by checker.** In Full layout roughly the last 18 diagnostic rows never render, including `Ele win by size`, `Zone age bars`, `Zone size atr`, `Zone fired ... wide`, `Arm loss`, `Pre-exp` and `Refused`. Four of the five measured wins in brief section 6 were read off rows now past the limit | `PanelRows` to 90 |
| Expired R in the numerator | Alpha 4332, 4340, 4348, 4443, 4905; Synthetics 1081, 1152, 1231 | `net += r.R` runs for every result, then `n = tp + sl + be` excludes outcome 3. Total R and average trade are computed on different populations | **Total R and Average trade are both wrong by the sum of expired-trade R.** Affects headline, Today, Week, every per-engine row and the whole weekday table, in both files. The `+81.97 R` and `+0.27 R` figures are on this basis. The comment above `DrawPanel` claims expired trades are excluded from every count, which is true of the rates and false of the sums | Move `net += r.R` inside the outcome branches, matching `DrawConsoPanel` |
| Reward floor float boundary | Synthetics 843 and 849 | `target = entry + _targetR * risk` then `if (Math.Abs(target - entry) / risk < MinRewardR) refuse`. `DriftR` and `MinRewardR` both ship at 2.00, so every drift signal lands exactly on the floor and one rounding bit decides it | **Confirmed numerically: 49.7 % of Drift rider signals silently refused**, over 200,000 samples at Boom 900, Boom 600 and Crash 500 scales. See `check_reward_floor.py`. Halves the trade count and net R of the only engine that measured profitable. Does **not** explain a 2 % win rate: the discarded half is random with respect to outcome | **Fixed in b1.** Compare with a 1e-9 tolerance |
| `SpikeFade` reads the wrong object | Synthetics 683 to 709 | With `Detect Spikes On m1` on (default), the spike is an m1 bar but `sDir`, `sRange` and `_stopPx` all come from `Bars[_lastSpikeBar]`, the **chart** bar. On Boom a chart bar can contain an up spike and still close down. `sDir` then reads -1, `isLong` becomes true, and the engine buys a down-drifting instrument after an up spike | Direction can be inverted on exactly the instrument family the engine was written for. Frequency not measured | Carry the m1 spike's direction, high, low and range on the record and build the fade from those. Queued as b2 |
| Panel divides bars by minutes | Synthetics 1123 to 1126 | `since` is in chart bars, `_meanInterval` is in minutes when `SpikeOnM1` is on, and the row prints `100.0 * since / _meanInterval` as "% of mean" | The `Since last ... % of mean` row is wrong by the bar length whenever m1 detection is on. On m15 it reads 15x low. `SpikeOverdue` converts correctly at 645, so the arming logic is right and only the display lies | Multiply `since` by `BarDuration().TotalMinutes` before the division |
| m1 history capped at 41.7 days | Synthetics 380 to 386 | `while (g2++ < 200 && _m1.Count < 60000)`. 60,000 m1 bars on a 24/7 synthetic is 41.7 days. `ScanM1` breaks immediately for any chart bar older than `_m1.OpenTimes[0]` | Over a 180 day window, spikes exist for the last ~42 days only. Spike clock and Spike fade cannot fire in the other ~138 days, and `SpikeOverdue` returns false there because `_spikeBar.Count < MinSpikeSample`, so **Drift rider's overdue block is also inert over most of the window.** The aggregate is a blend of two different systems | Raise the m1 cap to cover `AnalyseDays + OffsetDays`, or fall back to chart-bar detection where m1 does not reach and say so on the panel. Queued as b3 |
| Live trigger still consumes the arm | Alpha 2848 | The v36 fix removed the fire and the log, but the chase-abandon branch above it still runs `_arm.Fired = true` on a **forming** bar. `BarTriggerOne` measures chase from `Bars.OpenPrices[i]`, `LiveTriggerOne` from the live price, so a tick that spikes past the trigger and comes back kills an arm that would have fired at the close | Zero effect on historical measurement. Costs real Pre-expansion signals in live trading. The change review states "the arm is not consumed", which is false for this branch | Set a local abandon flag instead of `_arm.Fired`, or skip the chase test in the live path |
| Engine state starts cold at the window edge | Alpha 1563 to 1595 | The window gate returns before `TrackPivots`, `UpdateZones`, `UpdateFvg`, `UpdateSmc`, `UpdateElephants` and `UpdateConsolidation`. The 1200 warmup bars are loaded so raw-price lookbacks work, but `_phVal`, `_plVal`, `_zones`, `_gaps`, `_smZ*` and `_conts` are all empty at the first scored bar | Small and not measured. Roughly the first 50 to 100 bars of the window, well under 1 % of a 17,280 bar m15 sample. Flagged because the `Initialize` comment claims the opposite, and because it applies again at the new window start when `Window Offset Days` is set | Run the state builders before the window gate and keep only signal raising inside it, the way Synthetics already does with `UpdateSpikes` |
| `_tfTooHigh` cannot fire when needed | Synthetics 629 | Only assigned inside `RecordSpike`. If no spike is ever recorded, or `SpikeOnM1` is off, it stays false | The red `TIMEFRAME TOO HIGH` warning is absent in exactly the two cases that most need it | Compute it in `DrawPanel` from `_meanInterval` and `BarDuration()` regardless of path |

---

## Part C: areas of improvement

| Component | File | Opportunity | What it would be worth | What it would cost |
|---|---|---|---|---|
| `WindowOffsetDays` | Both | Never run at 180. Every setting was chosen on the window it is scored on | The difference between an edge and a curve fit. Larger than any remaining tuning | Two screenshots, no code |
| `box` in `BuildRisk` | Alpha 3660 | `_arm` is nulled by `CheckBarTrigger` (2797) and only reassigned by `UpdateArm` (2632), which runs last. Every engine between them sees `_arm == null` | `Stop Source = CompressionBox` silently falls back to nearest structure for nine of ten engines. No effect on shipped defaults | Pass the arm explicitly, or document that the box is Pre-expansion only |
| `MinGapBars` and `MinGapSameEngine` | Alpha | Two per-engine spacing mechanisms over `_lastBarBySrc` and `_lastSrcBar`, defaults 2 and 0 | One setting instead of two doing the same job | Rename risk: a changed default never reaches a saved instance |
| `flip` breakeven | Alpha 4356 | `100 / (1 + MinR)` ignores that break-even exits pay the spread and sit outside the win-rate denominator | The 25 % line she compares against is marginally optimistic | Arithmetic only |
| `MaxPerDay` and `PerEngineDaily` | Synthetics 83 and 86 | Both ship at 3, so the per-engine cap can never bind first | `_refusedCap` cannot distinguish which limit refused a signal | Set per-engine to 2, or drop to one counter |
| `_spikeBar` holds two units | Synthetics 527 vs 605 | Bar indices in the chart path, interval-in-minutes in the m1 path. `meanUnits` at 648 is a ternary with two identical branches, left over from the patch | Any future edit to this list will pick the wrong meaning | Split into `_spikeIntervalMins` and `_spikeChartBar` |
| First spike interval | Synthetics 611 | A fabricated `0` is pushed for the first spike. The mean loop starts at `q = 1` to skip it, but once the 500-cap trim starts, index 0 is a real sample that gets dropped | About 0.2 % on the mean interval | Do not push the placeholder |
| `UsePartial` accounting | Alpha 4043 | Banks `PartialAtR` gross of cost and relabels a partial-then-break-even exit as outcome 1 | Would lift the reported win rate without lifting expectancy. Default off, so nothing today | Charge the cost on the partial leg before shipping it on |
| Runner trail intrabar order | Alpha 3930 to 3936 | `t.Peak` is raised from this bar's extreme, then `stopped` is tested against this bar's opposite extreme | Assumes high before low inside one bar, the opposite of the honest convention used for the entry leg | Trail from the previous bar's peak |
| ATR index | Alpha | `UpdateArm` and `RunContinuations` use `_atr.Result[i]`, everything else uses `[i - 1]` | Both lookahead-free on a closed bar. Inconsistency only | Pick one |
| `_lastBarBySrc` | Alpha 1341 | Not initialised, unlike `_lastSrcBar` which is set to -1000000 | Blocks signals on the first 2 bars of history only | One line in `Initialize` |
| SMC structure | Alpha | 140 signals fired, never measured, held out of the dashboard | Unverified. Could be the sixth working engine or dead weight | Toggle `SmcInStats` and read the row |
| FVG retest | Alpha | Rebuilt with the displacement requirement in v41, never re-measured | Unverified. The 22.9 % reading predates the rebuild | Toggle `FvgInStats` and read the row |
| Position sizing | Both | Calculated in chat, absent from the code | At 17.97 points average risk on XAUUSD, 1 lot is 18 % of a 10,000 account and the worst run of 17 ends it | A panel row taking risk % and printing lots |

---

## Note on ordering

Two Part B faults corrupt the measuring instrument rather than the strategy: the
panel truncation hides the bucket rows, and the expired-R sum makes Total and
Average trade unreconcilable with the win rate. Brief section 6 records measured
changes at 5 for 5 and reasoned changes at 0 for 6. Both fixes are needed before
any measurement taken from here is trustworthy, and neither touches a trading
rule. Queued as b4 and b5.
