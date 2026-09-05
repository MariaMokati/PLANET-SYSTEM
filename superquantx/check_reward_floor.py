#!/usr/bin/env python3
# Reproduces the b1 finding: Synthetics Raise() builds the drift target as
# exactly MinRewardR * risk, so the floor test decides on one rounding bit.
import random

def refused(entry, risk, R=2.00, floor=2.00, is_long=True):
    target = entry + R * risk if is_long else entry - R * risk
    return abs(target - entry) / risk < floor

def run(seed=7, n=200000):
    random.seed(seed)
    cases = [
        ("Boom 900   price ~9000   risk 2 to 20",  8000.0, 10000.0,  2.0, 20.0),
        ("Boom 600   price ~14000  risk 5 to 60", 13000.0, 15000.0,  5.0, 60.0),
        ("Crash 500  price ~4000   risk 1 to 15",  3500.0,  4500.0,  1.0, 15.0),
    ]
    for name, lo, hi, rlo, rhi in cases:
        bad = 0
        for _ in range(n):
            if refused(random.uniform(lo, hi), random.uniform(rlo, rhi),
                       is_long=random.random() < 0.5):
                bad += 1
        print("%-40s  refused %6.2f %%" % (name, 100.0 * bad / n))

    e, r = 1234.5678, 0.0001
    t = e + 2.00 * r
    print()
    print("worked example that rounds the wrong way")
    print("  entry  %.17g" % e)
    print("  risk   %.17g" % r)
    print("  target %.17g" % t)
    print("  (target-entry)/risk = %.17g  ->  refused by a bare < : %s"
          % ((t - e) / r, (t - e) / r < 2.00))

if __name__ == '__main__':
    run()
