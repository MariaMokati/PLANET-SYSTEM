# Setup noise — what separates a real setup from clutter

The complaint: too many setups fire. The question asked of the data: which
measurable properties of a setup predict whether it was worth taking, under the
rule as traded — 4H range, sweep, confirming close back inside, stop 50% of the
range past the level, half banked at EQ with the stop to breakeven, rest to the
opposite extreme. Seven instruments, 2010/2012–2026, net of realistic costs.

Baseline: every qualifying setup, mean **−0.067 R** per trade, ~25 setups per
month on gold.

## The three properties that matter

**1. Reclaim strength — the biggest one, and it improved all 7 of 7.**
Measure where the confirming close lands, as a share of the range measured back
in from the swept level (0 = right at the level, 1 = at the opposite extreme).

| reclaim quartile | mean net R |
|---|---|
| Q1 — barely back inside | **−0.145** |
| Q2 | −0.037 |
| Q3 | −0.025 |
| Q4 — closed too deep | −0.059 |

The noise is concentrated in **closes that barely scrape back inside the
level** — hesitant reclaims with no displacement. Overextended closes (past
~75% of the range) are also degraded: the move already happened and the
remaining reward is small. The band **25%–75%** keeps 46% of setups and
improves every instrument.

**2. Range size vs its own recent history — improved 6 of 7.**
A range at least as large as the median of the previous 20 ranges: keeps 43%,
improves 6/7. The smallest quintile of ranges is the worst bucket everywhere
(−0.126). This is a *relative* measure — it adapts to the instrument and the
volatility regime, unlike a fixed point threshold.

**3. Session — already known, still true here.** Dropping Asia (03/04 SAST) and
NY afternoon (19/20 SAST) ranges improves 5/7 on its own.

Two things that do **not** work, measured so nobody re-adds them:
- An R:R floor (re-confirmed harmful, every instrument).
- Requiring extra room to the EQ — worse on 7/7; it selects the shallow
  reclaims that are the actual noise.

## The combination

**Reclaim 25–75% + range ≥ recent median + not Asia / NY-afternoon:**

| | XAUUSD | XAGUSD | NAS100 | US30 | UK100 | EURUSD | GBPUSD | mean |
|---|---|---|---|---|---|---|---|---|
| all setups | −0.071 | −0.052 | −0.062 | −0.038 | −0.073 | −0.088 | −0.082 | −0.067 |
| filtered | **+0.009** | **+0.076** | **+0.042** | **+0.034** | **+0.059** | −0.018 | **+0.051** | **+0.036** |

Keeps **15%** of setups — on gold that is 25.5/month → **3.6/month**. Positive
on 6 of 7 instruments net of base costs. This is the first configuration in
the entire project that is net positive across most instruments.

## Why it should be believed (and where it is fragile)

- **Structure, not cherry-picking:** each component is monotone across its
  quartiles and was individually 5–7/7 before any combination.
- **Split-half:** 13 of 14 instrument-halves sit at −0.035 or better; 10 of 14
  are outright positive; recent halves are mostly the stronger ones.
- **Bootstrap:** pooled 95% CI **[+0.012, +0.052]** — excludes zero.
- **Permutation test:** random subsets of the same size beat the real filter
  0 times in 500 (p ≈ 0.000).
- **Transfers across timeframes:** on gold 1H it moves −0.169 → −0.034 and on
  Daily −0.033 → −0.004 — the same direction, though not positive there.
- **Fragile to costs:** at DOUBLE the assumed costs the edge thins to roughly
  breakeven (indices/silver stay positive; gold −0.039, EURUSD −0.087).
  EURUSD is the weak instrument throughout. Real fills decide the margin.

## What this means for the indicator

Three settings, all of which already have a natural home:

1. **Reclaim band** (new): only confirm a setup whose closing bar lands
   25%–75% back inside the range. This is the single biggest noise cut.
2. **Relative range floor** (new variant): skip ranges smaller than the median
   of the last 20 — replaces guessing an ATR multiple with a self-calibrating
   measure.
3. **Sessions**: the existing slot picker / time filter already covers Asia and
   NY afternoon.

Expected effect at defaults: roughly **85% fewer setups**, and the ones that
remain measured **+0.036 R** per trade net across seven instruments instead of
−0.067. That is an edge of about a third of a percent of account risk per
trade at 10% risk — thin, real in the data, and entirely dependent on fills
staying near the assumed costs. It is not a license to size up.
