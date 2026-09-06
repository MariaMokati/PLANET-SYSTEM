#!/usr/bin/env python3
# Does the b2 drift rule actually fire on Boom-like m15 data?
# b1 shipped a fix for an engine that turned out to fire zero times.
# This checks the new rule before shipping, on synthetic Boom 600 style bars:
# a persistent down grind with occasional large up spikes.
import random

def make_boom(n, seed, spike_every=54, drift=-0.35, noise=1.1, spike=22.0):
    # spike_every 54 m15 bars ~ one spike per 13.5 hours of grind; Boom 600
    # spikes far more often than that on m1, but only a fraction of m15 bars
    # carry one big enough to matter for the drift engine.
    random.seed(seed)
    px, bars = 5300.0, []
    for i in range(n):
        o = px
        step = drift + random.gauss(0, noise)
        if i % spike_every == spike_every - 1:
            step += abs(random.gauss(spike, spike * 0.3))
        c = o + step
        hi = max(o, c) + abs(random.gauss(0, noise * 0.6))
        lo = min(o, c) - abs(random.gauss(0, noise * 0.6))
        bars.append((o, hi, lo, c))
        px = c
    return bars

def atr(bars, i, p=14):
    if i < p: return None
    s = 0.0
    for k in range(i - p + 1, i + 1):
        pc = bars[k - 1][3]
        s += max(bars[k][1] - bars[k][2], abs(bars[k][1] - pc), abs(bars[k][2] - pc))
    return s / p

def old_rule(bars, i, pull_bars=3):
    # every one of the last N bars must close counter to the drift (up on Boom)
    for k in range(i - pull_bars, i):
        if not (bars[k][3] > bars[k][0]): return False
    return bars[i][3] < bars[i][0]

def new_rule(bars, i, win=8, min_pull_atr=0.80):
    a = atr(bars, i - 1)
    if a is None or a <= 0: return False
    frm = max(0, i - win)
    ext = frm
    for k in range(frm, i + 1):
        if bars[k][1] > bars[ext][1]: ext = k          # highest high: drift extreme for a short
    pull_to = bars[ext][2]
    for k in range(ext, i + 1):
        if bars[k][2] < pull_to: pull_to = bars[k][2]
    pull = bars[ext][1] - pull_to
    if pull < min_pull_atr * a: return False
    return bars[i][3] < bars[i][0]

print("%-6s %10s %14s %14s" % ("seed", "bars", "old rule", "b2 retracement"))
to, tn = 0, 0
for seed in range(1, 6):
    bars = make_boom(17280, seed)                       # 180 days of m15
    o = sum(1 for i in range(20, len(bars)) if old_rule(bars, i))
    nn = sum(1 for i in range(20, len(bars)) if new_rule(bars, i))
    to += o; tn += nn
    print("%-6d %10d %14d %14d" % (seed, len(bars), o, nn))
print("%-6s %10s %14d %14d" % ("total", "", to, tn))
print()
print("per 180 day window, mean:  old %.0f   b2 %.0f" % (to / 5.0, tn / 5.0))
print("daily cap is 3/day = 540 max over 180 days, so b2 has a real population")
print("to select from and the cap does the rationing, not the rule.")

# ---------------------------------------------------------------------------
# The run above does NOT reproduce the observed zero, so it does not by itself
# explain b1. Boom is described as drifting down in SMALL UNIFORM STEPS, which
# means the grind dominates the noise. Sweep that ratio and watch which rule
# survives it.
print()
print("drift dominance sweep, 180 days of m15, one seed")
print("%-14s %10s %14s %14s" % ("drift/noise", "up bars %", "old rule", "b2 retracement"))
for dn in (0.3, 0.6, 1.0, 1.5, 2.5, 4.0):
    noise = 1.0
    bars = make_boom(17280, 11, drift=-dn * noise, noise=noise)
    ups = sum(1 for b in bars if b[3] > b[0])
    o = sum(1 for i in range(20, len(bars)) if old_rule(bars, i))
    nn = sum(1 for i in range(20, len(bars)) if new_rule(bars, i))
    print("%-14.1f %9.1f%% %14d %14d" % (dn, 100.0 * ups / len(bars), o, nn))
