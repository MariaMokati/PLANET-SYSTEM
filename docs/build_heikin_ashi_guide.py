# -*- coding: utf-8 -*-
"""Generate the Heikin Ashi reading guide as a self-contained HTML file."""

G = "#0EA36A"   # emerald  - bullish
R = "#E5484D"   # coral    - bearish
P = "#6E3AD6"   # purple   - structure / brand
T = "#0B9BAB"   # teal     - informational
A = "#E8930C"   # amber    - caution

def candle(o, c, h, l, colour, w=26, gap=0):
    """One candle. Values are 0-100 where 100 is the top of the drawing box."""
    def y(v):
        return 100 - v
    body_top, body_bot = max(o, c), min(o, c)
    x = gap
    cx = x + w / 2
    parts = []
    parts.append(
        f'<line x1="{cx:.1f}" y1="{y(h):.1f}" x2="{cx:.1f}" y2="{y(body_top):.1f}" '
        f'stroke="{colour}" stroke-width="2.4"/>')
    parts.append(
        f'<line x1="{cx:.1f}" y1="{y(body_bot):.1f}" x2="{cx:.1f}" y2="{y(l):.1f}" '
        f'stroke="{colour}" stroke-width="2.4"/>')
    hgt = max(body_top - body_bot, 1.2)
    parts.append(
        f'<rect x="{x:.1f}" y="{y(body_top):.1f}" width="{w}" height="{hgt:.1f}" '
        f'rx="1.5" fill="{colour}"/>')
    return "".join(parts)

def chart(candles, height=150, pad=14):
    """candles = list of (o, c, h, l, colour). Returns one inline SVG."""
    w, gap = (38, 16) if len(candles) == 1 else (26, 16)
    total = len(candles) * (w + gap) + pad * 2
    out = [f'<svg class="cs" viewBox="0 0 {total} 128" width="{min(total,470)}" '
           f'height="{height}" role="img">']
    for i, (o, c, h, l, col) in enumerate(candles):
        x = pad + i * (w + gap)
        out.append(f'<g transform="translate({x},14)">{candle(o,c,h,l,col)}</g>')
    out.append("</svg>")
    return "".join(out)

# ---------------------------------------------------------------- candle types
# (o, c, h, l) on a 0-100 scale
TYPES = [
    dict(n="2.1", pic=[(20, 78, 92, 20, G)],
         name="Flat-bottom green",
         short="Green body, no wick underneath at all.",
         kind="MECHANICAL",
         defn="The bottom of the body is also the bottom of the candle. There is "
              "nothing sticking out below.",
         maths="This happens when the middle of the last candle's body sat at or "
               "below the lowest price of this bar. In plain words: this whole "
               "bar traded above the middle of the last one.",
         means="Buyers held the entire bar. Sellers never pushed price back down "
               "into the last candle's body, not even for a second.",
         trend="The strongest ordinary up bar there is. In a run, these are the "
               "engine of the move.",
         do="Stay with the trend. This is not a warning candle. Do not look for a "
            "top while these keep printing.",
         wrong="One flat-bottom green in the middle of chop means nothing. It only "
               "carries weight inside a run of them."),
    dict(n="2.2", pic=[(80, 22, 80, 8, R)],
         name="Flat-top red",
         short="Red body, no wick on top at all.",
         kind="MECHANICAL",
         defn="The top of the body is also the top of the candle. Nothing sticks "
              "out above.",
         maths="The mirror of 2.1. The middle of the last candle's body sat at or "
               "above the highest price of this bar.",
         means="Sellers held the whole bar. Buyers never got price back up into "
               "the last candle's body.",
         trend="The strongest ordinary down bar. The engine of a sell-off.",
         do="Stay with the down move. Do not hunt for a bottom while these keep "
            "printing.",
         wrong="Same as above. One on its own is noise."),
    dict(n="2.3", pic=[(12, 88, 96, 12, G)],
         name="Big flat-bottom green",
         short="Flat bottom, and a body much bigger than the recent ones.",
         kind="CONVENTIONAL",
         defn="A flat-bottom green whose body is clearly taller than the bodies "
              "around it.",
         maths="Nothing extra is forced by the maths. 'Bigger' is your judgement "
               "against the recent bars, which is why this one is a convention.",
         means="Buyers were not just in control, they were in a hurry.",
         trend="Expansion. Moves that go somewhere usually contain a few of these.",
         do="This is the candle you want to already be positioned for, not the one "
            "you chase. Entering here means buying at the top of a fast bar.",
         wrong="A big body right at the end of a very long run is just as often "
               "the last push as the first."),
    dict(n="2.4", pic=[(90, 12, 90, 4, R)],
         name="Big flat-top red",
         short="Flat top, body much bigger than the recent ones.",
         kind="CONVENTIONAL",
         defn="The mirror of 2.3.",
         maths="Same caveat. 'Big' is relative and judged by eye.",
         means="Sellers in a hurry.",
         trend="Downside expansion.",
         do="Same rule. Do not chase it.",
         wrong="Same. A huge red bar after a long fall can be the end, not the "
               "start."),
    dict(n="2.5", pic=[(34, 80, 92, 16, G)],
         name="Green that grows a lower wick",
         short="Still green, but now something sticks out below the body.",
         kind="MECHANICAL",
         defn="A green candle where the low of the candle is below the bottom of "
              "the body.",
         maths="This can only happen if price traded BELOW the middle of the last "
               "candle's body at some point in this bar. That is exactly what the "
               "flat-bottom candle told you had not happened.",
         means="Sellers got a foot in the door. For the first time in the run, "
               "price came back into old ground.",
         trend="This is the first crack. Not a reversal, a crack. It is the "
               "earliest thing on the chart that says the one-way move has ended.",
         do="Tighten your attention, not necessarily your stop. Start looking for "
            "the sequences in section 3.",
         wrong="Lower wicks appear all the time in normal healthy pullbacks. One "
               "wick is information, not an instruction."),
    dict(n="2.6", pic=[(30, 60, 96, 30, G)],
         name="Green with a long upper wick",
         short="Green, but with a long tail sticking up above the body.",
         kind="MECHANICAL then CONVENTIONAL",
         defn="A green candle whose upper wick is long compared with its body.",
         maths="Here is the fact almost nobody tells you: a green Heikin Ashi "
               "candle ALWAYS has some upper wick. The maths guarantees it. So an "
               "upper wick on a green candle is never news. Only its SIZE relative "
               "to the body carries meaning, and 'long' is your judgement.",
         means="Price reached up there and did not stay. The average price of the "
               "bar finished well below its high.",
         trend="Inside an up run, repeated long upper wicks say buyers are paying "
               "up and getting nothing for it.",
         do="Read it together with the body. Long wick plus shrinking body is a "
             "much louder warning than either on its own.",
         wrong="A single long upper wick during a strong session often just means "
               "one spike got faded. Wait for it to repeat."),
    dict(n="2.7", pic=[(70, 40, 92, 40, R)],
         name="Red with a long upper wick",
         short="Red body with a long tail above it.",
         kind="CONVENTIONAL",
         defn="A red candle whose upper wick is long relative to its body.",
         maths="Because a red HA candle's body top is its open, a long upper wick "
               "means price pushed well above where the previous body sat, then "
               "gave it all back.",
         means="An attempt up that failed inside the bar.",
         trend="Inside a down move, this is continuation with a struggle. At the "
               "end of an up move, this is often where the turn actually happened.",
         do="Mark the real high of that bar from the NORMAL candle chart. That "
            "level matters. The HA wick top does not.",
         wrong="If the next candle is a flat-bottom green, the failure failed."),
    dict(n="2.8", pic=[(66, 44, 66, 8, R)],
         name="Red with a long lower wick",
         short="Red body with a long tail below it.",
         kind="MECHANICAL then CONVENTIONAL",
         defn="A red candle whose lower wick is long compared with its body.",
         maths="The mirror of 2.6. A red Heikin Ashi candle ALWAYS has some lower "
               "wick. Only the size is information.",
         means="Price fell down there and did not stay. Buyers lifted it back.",
         trend="Inside a down run, repeated long lower wicks say sellers are "
               "pressing and getting nothing.",
         do="Watch for it to repeat, and watch the bodies shrink alongside it.",
         wrong="One long lower wick is very often just a stop run that goes "
               "nowhere."),
    dict(n="2.9", pic=[(40, 62, 92, 20, G)],
         name="Wicks on both sides",
         short="Body in the middle, tails above and below.",
         kind="MECHANICAL",
         defn="A candle with visible wick above AND below the body.",
         maths="For a green candle, the lower wick is the one that had to be "
               "earned. Its presence proves price went back below the middle of "
               "the last body.",
         means="Both sides got a turn inside the same bar. Nobody kept control.",
         trend="Two-sided wicks are the signature of a market that has stopped "
               "trending and started arguing.",
         do="If these appear at the top of a long green run, treat the run as "
            "finished until proven otherwise.",
         wrong="During a quiet session, two-sided wicks may only mean nothing is "
               "happening yet. Volume and time of day matter."),
    dict(n="2.10", pic=[(44, 54, 62, 40, G), (40, 62, 72, 34, G),
                        (32, 78, 90, 26, G)],
         name="Bodies getting bigger",
         short="Each body taller than the last, same colour.",
         kind="CONVENTIONAL",
         defn="Three or more candles of the same colour where each body is "
              "noticeably taller than the one before.",
         maths="Nothing forced. This is a shape you read.",
         means="The move is speeding up, not just continuing.",
         trend="Expansion. This is what a real move looks like while it is working.",
         do="Hold. Do not take profit into strength just because it feels far.",
         wrong="Expansion also happens at the very end, into a blow-off. Size "
               "alone cannot tell you which."),
    dict(n="2.11", pic=[(24, 76, 88, 24, G), (34, 70, 82, 30, G),
                        (46, 62, 76, 40, G)],
         name="Bodies getting smaller",
         short="Same colour, but each body shorter than the last.",
         kind="CONVENTIONAL",
         defn="Three or more same-colour candles whose bodies shrink step by step.",
         maths="Nothing forced. Read by eye.",
         means="The side in control is still in control, but pushing less hard "
               "each bar.",
         trend="This is the quiet warning. It usually arrives BEFORE the first "
               "candle of the other colour, which is exactly what makes it useful.",
         do="This is your cue to start managing an open position, not to reverse.",
         wrong="Bodies shrink during every normal pause. A pause and a top look "
               "identical until price proves otherwise."),
    dict(n="2.12", pic=[(52, 48, 88, 12, A)],
         name="Heikin Ashi doji",
         short="Tiny body, long wicks on both sides.",
         kind="MECHANICAL",
         defn="A candle whose body is very small compared with its total height.",
         maths="A small body means the average price of this bar finished almost "
               "exactly on the middle of the last body. Nothing moved, net.",
         means="Complete disagreement. Everything that was won inside the bar was "
               "given back.",
         trend="At the end of a long run, this is the classic pause-or-turn bar. "
               "In the middle of a range, it is just more range.",
         do="Do nothing on the doji itself. Act on the candle AFTER it, and on "
            "which side of the doji's real range price leaves.",
         wrong="Dojis are extremely common on low timeframes and around session "
               "changeovers. Location decides whether one matters."),
    dict(n="2.13", pic=[(50, 50, 84, 16, A)],
         name="The flat line",
         short="A body so small it is basically a line.",
         kind="MECHANICAL",
         defn="Body height near zero.",
         maths="The bar's average price landed on the previous body's midpoint "
               "almost exactly.",
         means="Total indecision, or almost no participation at all.",
         trend="Very often a session boundary, a holiday, or the quiet hours "
               "rather than a real signal.",
         do="Check the clock before you read anything into it.",
         wrong="Reading a dead-hours flat line as a reversal signal is one of the "
               "most common ways to lose money on this chart."),
]

# ------------------------------------------------------------------ sequences
def run(spec):
    return [(o, c, h, l, col) for (o, c, h, l, col) in spec]

SEQS = [
    dict(n="3.1", name="Many greens, one red, greens again",
         pic=[(20,74,86,20,G),(24,78,88,24,G),(30,80,90,30,G),
              (76,58,88,44,R),(46,84,92,36,G),(30,86,94,30,G)],
         what="A single red candle interrupts a run of greens, then the greens "
              "come straight back.",
         under="One bar of profit-taking. Price dipped back into the last body, "
               "found buyers immediately, and carried on.",
         bias="Continuation", biascol=G,
         confirm="The very next candle is green AND has no lower wick. That flat "
                 "bottom is the tell: the pullback is already over.",
         kills="The next candle is a second red with a bigger body than the first.",
         do="Nothing. This is what a healthy trend looks like. If you are already "
            "long, this is not your exit."),
    dict(n="3.2", name="Many greens, two reds, greens again",
         pic=[(22,76,88,22,G),(28,80,90,28,G),(78,60,90,48,R),
              (60,46,72,34,R),(44,80,88,36,G),(32,86,94,32,G)],
         what="Two reds in a row inside an up run, then green again.",
         under="A deeper pause. Price gave back more, but the second red did not "
               "accelerate.",
         bias="Continuation, with less confidence than 3.1", biascol=G,
         confirm="The second red has a SMALLER body than the first, and the green "
                 "that follows closes above the first red's body top.",
         kills="The second red is bigger than the first. That is acceleration "
               "down, not a pause, and it turns this into 3.4.",
         do="Wait for the green. Two reds is the point where you stop adding and "
            "start watching."),
    dict(n="3.3", name="Greens, then three or more reds",
         pic=[(26,78,88,26,G),(80,62,90,50,R),(64,44,74,34,R),
              (48,26,58,18,R),(30,16,40,8,R)],
         what="Three or more reds in a row after an up run.",
         under="This is no longer a pause. Sellers have had three consecutive bars "
               "of control.",
         bias="Change", biascol=R,
         confirm="Three reds where the bodies do NOT shrink, plus a red that takes "
                 "out the real low of the last obvious swing on the normal candle "
                 "chart. Structure confirms, Heikin Ashi only describes.",
         kills="Bodies shrink across the three, wicks appear underneath, and a "
               "flat-bottom green follows. Then it was a deep pullback.",
         do="This is the honest answer to 'where is the line between a pullback "
            "and a real change': there is no candle count that settles it. Two is "
            "usually a pause, three is a question, four with growing bodies plus a "
            "broken structure level is a change. The structure break is what "
            "decides, not the count."),
    dict(n="3.4", name="Two greens, one red, then many reds",
         pic=[(30,72,84,30,G),(36,76,86,36,G),(74,58,86,46,R),
              (58,40,70,30,R),(42,24,54,16,R),(26,12,36,6,R)],
         what="The up run was already short. One red, then the reds keep coming "
              "and get bigger.",
         under="The rolling-over sequence. The two greens were the weak end of a "
               "move, not the start of one.",
         bias="Change", biascol=R,
         confirm="Each red body bigger than the last, and upper wicks on the reds "
                 "getting shorter.",
         kills="A flat-bottom green appears inside the reds.",
         do="If you were long from the greens, this sequence is why you are out. "
            "Do not wait for a fifth red to admit it."),
    dict(n="3.5", name="Many reds, one green, then red again",
         pic=[(74,54,84,44,R),(56,36,66,28,R),(40,20,50,14,R),
              (24,42,54,18,G),(44,22,52,14,R),(24,10,34,6,R)],
         what="A single green interrupts a down run and is immediately followed by "
              "red again.",
         under="The failed bounce. Buyers showed up for exactly one bar and got "
               "run over.",
         bias="Continuation of the down move", biascol=R,
         confirm="The lone green has a long upper wick and a small body, and the "
                 "red after it takes out the green's low.",
         kills="The lone green has NO lower wick and a big body. That is 3.6 "
               "starting, not a failed bounce.",
         do="This is the single most expensive pattern for someone trying to catch "
            "a bottom. One green candle is not a turn."),
    dict(n="3.6", name="Many reds, one green, then many greens",
         pic=[(72,52,82,42,R),(54,34,64,26,R),(38,18,48,12,R),
              (18,52,60,18,G),(52,76,86,52,G),(40,88,94,40,G)],
         what="A down run ends, one green prints, and the greens keep coming.",
         under="The real turn. Control changed hands and stayed changed.",
         bias="Change", biascol=G,
         confirm="The FIRST green has no lower wick, and the second green's body "
                 "is bigger than the first's. That combination is the strongest "
                 "flip signal on this chart.",
         kills="The first green has a long lower wick and a small body, then a red "
               "with a bigger body follows. Back to 3.5.",
         do="Note the difference between this and 3.5 is only visible on the "
            "SECOND candle after the green. That is the cost of the smoothing: you "
            "cannot know on the green itself."),
    dict(n="3.7", name="Red, green, red, green, all small",
         pic=[(54,44,68,32,R),(44,56,68,32,G),(56,46,70,34,R),
              (46,58,70,34,G),(58,48,72,36,R),(48,60,72,36,G)],
         what="Colours alternate every bar or two and no body is large.",
         under="Chop. Neither side can hold the bar.",
         bias="No bias", biascol=A,
         confirm="Three or more colour flips inside six candles, with all bodies "
                 "smaller than the recent average and wicks on both sides.",
         kills="One candle with a flat side and a body bigger than everything "
               "around it.",
         do="Stop trading it. This is the pattern that quietly takes back what the "
            "trending days gave you. Recognising it fast is worth more than any "
            "entry rule in this document."),
    dict(n="3.8", name="Greens whose bodies shrink",
         pic=[(18,80,90,18,G),(26,78,88,26,G),(36,72,84,36,G),
              (46,64,78,46,G),(54,60,74,50,G)],
         what="Still all green. No red anywhere. But each body is shorter than the "
              "last.",
         under="Buyers still win every bar, by less and less.",
         bias="Warning, not a signal", biascol=A,
         confirm="Four or more greens in a row with steadily shrinking bodies.",
         kills="One big flat-bottom green resets the whole thing.",
         do="This is the earliest warning Heikin Ashi gives, and it arrives while "
            "the chart is still entirely green. It is the reason to read bodies "
            "and not just colours."),
    dict(n="3.9", name="Greens where lower wicks start appearing",
         pic=[(16,82,92,16,G),(24,80,90,24,G),(34,76,88,28,G),
              (44,72,86,32,G),(50,70,84,34,G)],
         what="Still all green, but wicks begin to show underneath the bodies and "
              "get longer each bar.",
         under="Price is dipping back into old ground during the bar, and doing it "
               "more each time.",
         bias="Warning, not a signal", biascol=A,
         confirm="Three greens in a row where each lower wick is longer than the "
                 "last.",
         kills="A green with a flat bottom.",
         do="In practice these two warnings — 3.8 and 3.9 — usually arrive "
            "together, and when they do the warning is much stronger than either "
            "alone. When they arrive separately, the shrinking bodies of 3.8 tend "
            "to come first, because the body shrinks as soon as the push weakens, "
            "while the wick needs price to actually trade back down."),
    dict(n="3.10", name="A colour flip with no wick on the trend side",
         pic=[(24,78,88,24,G),(32,80,90,32,G),(80,58,80,44,R)],
         what="After a green run, the first red has NO upper wick at all.",
         under="The flip did not just happen, it happened decisively. Price never "
               "got back up into the last body during the whole bar.",
         bias="Change", biascol=R,
         confirm="It is already its own confirmation. This is the strongest single "
                 "flip candle there is.",
         kills="Very little on the bar itself. It can still fail on the next bar, "
               "but rarely quietly.",
         do="Compare with a first red that has a long upper wick. That one is a "
            "shrug. This one is a decision."),
    dict(n="3.11", name="A red inside an uptrend that is bigger than the greens",
         pic=[(28,72,84,28,G),(34,74,86,34,G),(38,76,88,38,G),
              (82,34,88,26,R)],
         what="One red candle whose body is clearly taller than every green around "
              "it.",
         under="Everything the last few bars built was undone in one bar.",
         bias="Change", biascol=R,
         confirm="It is size against its neighbours that matters, so compare it "
                 "with the last five bodies, not with your memory.",
         kills="An immediate flat-bottom green that closes above the red's body "
               "top.",
         do="Treat this as more serious than three small reds. Speed of the give-"
            "back matters more than the number of bars it takes."),
    dict(n="3.12", name="Two-sided wicks at the top of a long green run",
         pic=[(20,80,90,20,G),(28,78,88,28,G),(40,68,88,30,G),
              (46,58,86,34,G),(50,54,84,32,G)],
         what="After a strong run, the last few greens all have visible wicks above "
              "AND below.",
         under="The run has stopped being one-sided. Both sides are getting a turn "
               "inside every bar now.",
         bias="Warning", biascol=A,
         confirm="Two or more consecutive greens with wicks on both sides after a "
                 "run of flat-bottomed ones.",
         kills="A flat-bottom green with an expanding body.",
         do="Combine with 3.8. Shrinking bodies plus two-sided wicks at the top of "
            "a run is the most reliable warning combination on the chart, and it "
            "is still only a warning."),
]

MTF = [
    ("Green", "Green", "Green", "All three agree, up",
     "The cleanest condition there is. Pullbacks on the 15m are the only thing to "
     "wait for.", G),
    ("Green", "Green", "Red", "Big picture up, small picture pulling back",
     "This is the normal buying opportunity. The 15m red run is the pullback.", T),
    ("Green", "Red", "Red", "Daily up, but the 4H has turned",
     "A deeper correction is running. The daily has not broken, but do not treat "
     "the 15m greens as trend continuation yet.", A),
    ("Red", "Green", "Green", "Daily down, lower timeframes bouncing",
     "The most dangerous combination for a new trader. Most of these bounces end. "
     "The daily is the one telling the truth.", A),
    ("Red", "Red", "Green", "Big picture down, small picture pulling back",
     "The mirror of row two. This is the normal selling opportunity.", T),
    ("Red", "Red", "Red", "All three agree, down",
     "Clean downside. Same logic as row one, reversed.", R),
]

CHECK = [
    ("What colour is the daily, and is its body growing or shrinking?",
     "This sets which direction you are even allowed to look for."),
    ("What colour is the 4H, and does it agree with the daily?",
     "Agreement means trade with it. Disagreement means you are inside a "
     "correction, so expect it to be messier."),
    ("On my trading timeframe, are the bodies growing or shrinking?",
     "Growing means the move is working. Shrinking is the early warning, and it "
     "comes before any colour change."),
    ("Are wicks appearing on the trend side?",
     "Lower wicks in an up run, upper wicks in a down run. Their arrival is the "
     "first crack."),
    ("Which named sequence from section 3 am I looking at right now?",
     "If you cannot name it, you do not have a read. That is a valid answer and it "
     "means do nothing."),
    ("Where are my real levels, from the NORMAL candle chart?",
     "Heikin Ashi never supplies a level. Not one. Ever."),
    ("If I am wrong, what will the chart do to prove it?",
     "Say it out loud before you act, not afterwards."),
]

NOTTRUE = [
    ("&ldquo;Heikin Ashi removes the noise.&rdquo;",
     "It does not remove anything. It averages each bar with the bar before it, so "
     "the noise is smeared across several candles instead of sitting in one. The "
     "chart looks calmer. The market is exactly as messy as it was. And the "
     "averaging adds a delay that plain candles do not have."),
    ("&ldquo;Green means buy, red means sell.&rdquo;",
     "The colour flips when the average price of the new bar crosses the middle of "
     "the last candle's body. That is a backward-looking test by construction, so "
     "the flip is always late. Trading every flip in a market that is not trending "
     "is a reliable way to lose money slowly."),
    ("&ldquo;Heikin Ashi filters out false signals.&rdquo;",
     "It delays them. A false signal that arrives two bars late is still a false "
     "signal, and you now have a worse price."),
    ("&ldquo;You can trade it the same way you trade normal candles.&rdquo;",
     "You cannot, and this is the one that costs real money. The high and low of a "
     "Heikin Ashi candle are not prices that traded. A stop placed on one sits at "
     "a price that never existed."),
    ("&ldquo;It shows you the real trend.&rdquo;",
     "It shows you a smoothed version of recent bars. Whether that constitutes a "
     "trend is your interpretation, and the smoothing makes ranges look like "
     "trends more often than it makes trends look like ranges."),
    ("&ldquo;The backtest on the Heikin Ashi chart was profitable.&rdquo;",
     "Backtests run on a Heikin Ashi chart fill orders at Heikin Ashi prices, "
     "which do not exist in the market. Results from such a test are not "
     "achievable. This is one of the best-known ways to produce a beautiful equity "
     "curve that cannot be traded."),
]

# ------------------------------------------------------------------ rendering
CSS = """
@page { size: A4; margin: 15mm 14mm 16mm 14mm; }
* { box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { margin:0; background:#FFFFFF; color:#16181D;
  font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Inter,Helvetica,Arial,sans-serif;
  font-weight:400; font-size:10.4pt; line-height:1.52; }
h1,h2,h3,h4 { font-weight:500; margin:0; letter-spacing:-0.01em; }
p { margin:0 0 8px 0; }
.page-break { break-before:page; }
.avoid { break-inside:avoid; }

/* cover */
.cover { height:255mm; display:flex; flex-direction:column; justify-content:center; }
.eyebrow { font-size:10pt; color:#6E3AD6; font-weight:500; letter-spacing:.14em;
  text-transform:uppercase; margin-bottom:14px; }
.cover h1 { font-size:34pt; line-height:1.1; margin-bottom:16px; }
.cover .sub { font-size:13pt; color:#4A505C; max-width:150mm; margin-bottom:30px; }
.rule { height:5px; width:120px; border-radius:3px; margin-bottom:26px;
  background:linear-gradient(90deg,#6E3AD6,#0B9BAB,#0EA36A,#E8930C,#E5484D); }
.meta { font-size:9.6pt; color:#6B7280; }
.meta b { color:#16181D; font-weight:500; }
.covergrid { display:flex; gap:26px; margin:34px 0 30px; }
.covergrid div { flex:1; }
.covergrid .k { font-size:9pt; color:#6E3AD6; font-weight:500; margin-bottom:4px; }
.covergrid .v { font-size:10pt; color:#3A4150; }

/* section headings */
.sec { break-before:page; }
.sec > .lbl { font-size:9.4pt; font-weight:500; letter-spacing:.12em;
  text-transform:uppercase; margin-bottom:6px; }
.sec > h2 { font-size:21pt; margin-bottom:6px; }
.sec > .lede { font-size:11pt; color:#4A505C; margin-bottom:20px; max-width:160mm; }
h3 { font-size:13pt; margin:22px 0 8px; }
h4 { font-size:11pt; margin:16px 0 6px; color:#2A2F3A; }

/* callouts */
.callout { border-radius:10px; padding:14px 16px; margin:16px 0; break-inside:avoid; }
.callout .ttl { font-weight:500; font-size:11pt; margin-bottom:5px; }
.c-warn { background:#FFF1F1; border-left:5px solid #E5484D; }
.c-warn .ttl { color:#C42127; }
.c-info { background:#ECFAFC; border-left:5px solid #0B9BAB; }
.c-info .ttl { color:#07727E; }
.c-note { background:#FFF7E8; border-left:5px solid #E8930C; }
.c-note .ttl { color:#A96606; }
.c-good { background:#EBFBF3; border-left:5px solid #0EA36A; }
.c-good .ttl { color:#0A7A50; }
.c-brand { background:#F4EFFE; border-left:5px solid #6E3AD6; }
.c-brand .ttl { color:#5326B0; }
.big-warn { background:#FFF1F1; border:2px solid #E5484D; border-radius:12px;
  padding:18px 20px; margin:20px 0; break-inside:avoid; }
.big-warn .ttl { color:#C42127; font-size:14pt; font-weight:500; margin-bottom:8px; }

/* formula strip */
.formulas { display:grid; grid-template-columns:1fr 1fr; gap:10px; margin:14px 0; }
.fx { background:#FAFAFC; border:1px solid #E7E8EE; border-radius:9px; padding:11px 13px;
  break-inside:avoid; }
.fx .f { font-family:"SF Mono",Menlo,Consolas,monospace; font-size:9.2pt;
  color:#5326B0; margin-bottom:5px; }
.fx .t { font-size:9.6pt; color:#3A4150; }

/* candle cards */
.card { border:1px solid #E7E8EE; border-radius:12px; padding:14px 16px; margin:14px 0;
  break-inside:avoid; display:flex; gap:16px; }
.card .art { flex:0 0 118px; display:flex; align-items:center; justify-content:center;
  background:#FBFBFD; border-radius:9px; padding:6px; }
.card .art svg { max-width:100%; height:auto; }
.card .body { flex:1; min-width:0; }
.card .hd { display:flex; align-items:baseline; gap:9px; margin-bottom:3px;
  flex-wrap:wrap; }
.card .num { font-size:9pt; color:#6E3AD6; font-weight:500; }
.card .nm { font-size:12pt; font-weight:500; }
.tag { font-size:7.6pt; font-weight:500; letter-spacing:.07em; text-transform:uppercase;
  padding:2px 7px; border-radius:20px; }
.t-mech { background:#EBFBF3; color:#0A7A50; }
.t-conv { background:#FFF7E8; color:#A96606; }
.t-both { background:#F4EFFE; color:#5326B0; }
.card .short { color:#4A505C; font-size:10pt; margin-bottom:8px; }
.rows { display:grid; grid-template-columns:90px 1fr; gap:3px 11px; font-size:9.6pt; }
.rows .k { color:#6E3AD6; font-weight:500; }
.rows .k.warn { color:#C42127; }
.rows .v { color:#252A34; }

/* sequence cards */
.seq { border:1px solid #E7E8EE; border-radius:12px; padding:15px 17px; margin:15px 0;
  break-inside:avoid; }
.seq .hd { display:flex; align-items:baseline; gap:10px; margin-bottom:9px;
  flex-wrap:wrap; }
.seq .art { background:#FBFBFD; border-radius:9px; padding:6px 4px; margin:6px 0 11px;
  text-align:center; }
.bias { font-size:8pt; font-weight:500; letter-spacing:.06em; text-transform:uppercase;
  padding:3px 9px; border-radius:20px; color:#fff; }

/* tables */
table { width:100%; border-collapse:collapse; font-size:9.2pt; margin:12px 0; }
th { text-align:left; font-weight:500; font-size:8.4pt; letter-spacing:.07em;
  text-transform:uppercase; color:#6E3AD6; padding:7px 8px;
  border-bottom:2px solid #E7DDFB; }
td { padding:8px; border-bottom:1px solid #EFF0F4; vertical-align:top; }
tr { break-inside:avoid; }
td.pic { width:92px; text-align:center; }
td.pic svg { height:46px; width:auto; }
.pill { display:inline-block; font-size:8pt; font-weight:500; padding:2px 8px;
  border-radius:20px; color:#fff; }

/* checklist */
.chk { counter-reset:c; }
.chk .item { display:flex; gap:12px; padding:10px 0; border-bottom:1px solid #EFF0F4;
  break-inside:avoid; }
.chk .n { flex:0 0 26px; height:26px; border-radius:50%; background:#6E3AD6; color:#fff;
  font-size:10pt; font-weight:500; display:flex; align-items:center;
  justify-content:center; }
.chk .q { font-size:10.6pt; font-weight:500; margin-bottom:2px; }
.chk .a { font-size:9.6pt; color:#4A505C; }

/* not-true */
.nt { border-left:5px solid #E5484D; background:#FFF6F6; border-radius:0 9px 9px 0;
  padding:12px 15px; margin:11px 0; break-inside:avoid; }
.nt .claim { font-weight:500; color:#C42127; margin-bottom:4px; font-size:10.6pt; }

/* toc */
.toc { margin-top:18px; }
.toc a, .toc div.row { display:flex; justify-content:space-between; gap:12px;
  padding:9px 0; border-bottom:1px solid #EFF0F4; text-decoration:none; color:inherit; }
.toc .t { font-size:11pt; font-weight:500; }
.toc .d { font-size:9.4pt; color:#6B7280; max-width:98mm; text-align:right;
  font-weight:400; }
footer { margin-top:26px; padding-top:12px; border-top:1px solid #EFF0F4;
  font-size:8.6pt; color:#8A90A0; }
"""

def esc(s):
    return s

def card_html(d):
    kind = d["kind"]
    cls = "t-both" if "then" in kind else ("t-mech" if kind == "MECHANICAL" else "t-conv")
    rows = [
        ("What it is", d["defn"]),
        ("The maths", d["maths"]),
        ("What it means", d["means"]),
        ("In a trend", d["trend"]),
        ("What I do", d["do"]),
    ]
    r = "".join(f'<div class="k">{k}</div><div class="v">{v}</div>' for k, v in rows)
    r += (f'<div class="k warn">Wrong when</div><div class="v">{d["wrong"]}</div>')
    return f"""<div class="card">
  <div class="art">{chart(d["pic"], height=118)}</div>
  <div class="body">
    <div class="hd"><span class="num">{d["n"]}</span>
      <span class="nm">{d["name"]}</span>
      <span class="tag {cls}">{kind}</span></div>
    <div class="short">{d["short"]}</div>
    <div class="rows">{r}</div>
  </div>
</div>"""

def seq_html(d):
    return f"""<div class="seq">
  <div class="hd"><span class="num" style="font-size:9pt;color:#6E3AD6;font-weight:500">{d["n"]}</span>
    <span style="font-size:12.5pt;font-weight:500">{d["name"]}</span>
    <span class="bias" style="background:{d["biascol"]}">{d["bias"]}</span></div>
  <div class="art">{chart(d["pic"], height=120)}</div>
  <div class="rows">
    <div class="k">What you see</div><div class="v">{d["what"]}</div>
    <div class="k">Underneath</div><div class="v">{d["under"]}</div>
    <div class="k">Confirms it</div><div class="v">{d["confirm"]}</div>
    <div class="k warn">Kills it</div><div class="v">{d["kills"]}</div>
    <div class="k">What I do</div><div class="v">{d["do"]}</div>
  </div>
</div>"""

def summary_row(d):
    kind = "Mechanical" if d["kind"] == "MECHANICAL" else (
        "Convention" if d["kind"] == "CONVENTIONAL" else "Both")
    col = {"Mechanical": G, "Convention": A, "Both": P}[kind]
    return (f'<tr><td class="pic">{chart(d["pic"], height=46)}</td>'
            f'<td><b style="font-weight:500">{d["name"]}</b><br>'
            f'<span style="color:#6B7280">{d["short"]}</span></td>'
            f'<td><span class="pill" style="background:{col}">{kind}</span></td>'
            f'<td>{d["means"]}</td><td>{d["do"]}</td></tr>')

TOC = [
    ("1 &nbsp;The basics", "What a Heikin Ashi candle really is, and the one warning that matters most."),
    ("2 &nbsp;The candle types", "Every shape, sorted by its wicks. The catalogue you keep beside the screen."),
    ("3 &nbsp;The sequences", "What it means when the colours change, run by run."),
    ("4 &nbsp;Structure and timeframes", "Reading structure with Heikin Ashi, and stacking daily, 4H and 15m."),
    ("5 &nbsp;Expert: where it fails", "Chop, gaps, the lag, and the stop-loss trap that costs real money."),
    ("6 &nbsp;The seven questions", "The checklist, short enough to memorise."),
    ("7 &nbsp;What is not true", "The claims about Heikin Ashi that do not hold up."),
]

html = []
A_ = html.append

A_(f"<style>{CSS}</style>")

# ---- cover
A_(f"""<section class="cover">
  <div class="eyebrow">XAUUSD &middot; price action</div>
  <h1>Reading the chart with<br>Heikin Ashi candles</h1>
  <div class="rule"></div>
  <div class="sub">Every candle shape, every colour change, and what each one actually
    tells you about who is in control. Basics to expert, in the plainest English
    possible.</div>
  <div class="covergrid">
    <div><div class="k">📊 What this is</div>
      <div class="v">A guide to <b>reading</b> a chart. Not a strategy, no entries,
        no stops, no signals.</div></div>
    <div><div class="k">🎯 Built for</div>
      <div class="v">XAUUSD on D1, 4H, 1H, 15m and 5m, alongside the normal candle
        chart you already read.</div></div>
    <div><div class="k">⚠️ Honesty</div>
      <div class="v">Nothing here has been backtested. It is a way of seeing, not
        evidence that any of it makes money.</div></div>
  </div>
  <div class="meta">Every claim in this document is labelled
    <b>mechanical</b> (forced by the formulas, always true) or
    <b>convention</b> (how traders read it, not a law).</div>
</section>""")

# ---- contents
A_('<section class="sec"><div class="lbl" style="color:#6E3AD6">Contents</div>'
   '<h2>What is in here</h2>'
   '<div class="lede">Seven sections. Read one to four in order. Five to seven are '
   'the ones you come back to.</div><div class="toc">')
for t, d in TOC:
    A_(f'<div class="row"><div class="t">{t}</div><div class="d">{d}</div></div>')
A_('</div>')

A_("""<div class="big-warn">
  <div class="ttl">⚠️ Read this before anything else</div>
  <p><b style="font-weight:500">A Heikin Ashi price is not a real price.</b> The
  close of a Heikin Ashi candle is an average. The high and low of a Heikin Ashi
  candle can sit at prices that never traded at all.</p>
  <p style="margin-bottom:0">So: you may read the chart with Heikin Ashi. You may
  never take a level from it. Every entry, every stop, every target comes off the
  normal candle chart. There is no exception to this and it is the only mistake in
  this whole document that costs money rather than accuracy.</p>
</div>""")
A_('</section>')

# ---- section 1
A_("""<section class="sec"><div class="lbl" style="color:#0B9BAB">Section one</div>
<h2>The basics</h2>
<div class="lede">Four small sums turn an ordinary chart into a Heikin Ashi chart.
Understanding them is what separates reading this chart from guessing at it.</div>

<h3>What a Heikin Ashi candle is made of</h3>
<p>Each Heikin Ashi candle is built from two things: the real bar happening right
now, and the Heikin Ashi candle just before it. That second ingredient is the
whole story.</p>
<div class="formulas">
  <div class="fx"><div class="f">HA close = (open + high + low + close) &divide; 4</div>
    <div class="t">The <b>average price of this bar</b>. Not where it closed &mdash;
      where it spent its time.</div></div>
  <div class="fx"><div class="f">HA open = (previous HA open + previous HA close) &divide; 2</div>
    <div class="t">The <b>middle of the last candle's body</b>. This has nothing to
      do with the market right now. It is memory.</div></div>
  <div class="fx"><div class="f">HA high = highest of (real high, HA open, HA close)</div>
    <div class="t">Usually the real high &mdash; but it can be the HA open or close
      instead, and then the top of the candle is a price that never traded.</div></div>
  <div class="fx"><div class="f">HA low = lowest of (real low, HA open, HA close)</div>
    <div class="t">The same in reverse.</div></div>
</div>

<h4>Why this makes the chart look smoother</h4>
<p>Every candle carries half of the one before it. So a single wild bar cannot
produce a single wild candle &mdash; it gets spread across the next few. The chart
calms down. The market does not.</p>

<div class="callout c-info">
  <div class="ttl">💡 The one sentence that explains everything else</div>
  <p style="margin-bottom:0">The Heikin Ashi open is the middle of the last candle's
  body. So every candle is really answering one question: <b>did this bar trade
  above or below the middle of the last one?</b> Every shape in section two is just
  a different answer to that question.</p>
</div>

<h3>The lag, and what it costs you</h3>
<p>Because half of each candle comes from the past, turns show up late. On a sharp
reversal you will typically see the colour flip one to two candles after the actual
turn, and on a fast V-shaped turn it can be more. On the daily that is a day. On
the 15m it is half an hour.</p>
<p>The lag hurts most on fast, sharp turns and on low timeframes where each candle
is a small slice of time. It hurts least in slow, grinding trends, which is exactly
where Heikin Ashi is worth using.</p>

<h3>What the colour does and does not tell you</h3>
<p>Green means the HA close finished above the HA open. Red means below. That is
all it means.</p>
<p>It does <b>not</b> mean the bar closed up. It does not mean the trend is up. It
means the average price of this bar landed above the middle of the last candle's
body. Everything useful on this chart lives in the <b>wicks and the body sizes</b>,
not in the colour.</p>
<div class="callout c-note">
  <div class="ttl">📌 Two facts that kill a lot of bad advice</div>
  <p>A green Heikin Ashi candle <b>always</b> has an upper wick. A red one
  <b>always</b> has a lower wick. The formulas guarantee it.</p>
  <p style="margin-bottom:0">So "a green candle with an upper wick" is never a
  signal &mdash; it is every green candle. Only the <b>size</b> of that wick against
  the body carries any information. The wick that genuinely means something is the
  one on the <i>other</i> side: a lower wick on a green candle, an upper wick on a
  red one. Those have to be earned.</p>
</div>
</section>""")

# ---- section 2
A_("""<section class="sec"><div class="lbl" style="color:#0EA36A">Section two</div>
<h2>The candle types</h2>
<div class="lede">Sorted by their wicks, because that is where the information is.
Each one is labelled mechanical or convention, so you always know whether you are
looking at a fact or a habit.</div>""")

A_("""<div class="callout c-brand">
  <div class="ttl">🎯 Answering the two questions directly</div>
  <p><b style="font-weight:500">A wick on the upper side.</b> On a <b>green</b>
  candle it is always there, so only its length matters &mdash; a long one says
  price reached up and did not stay. On a <b>red</b> candle it had to be earned, so
  its presence is the news: price got back up into the last body and was pushed
  down again. In an uptrend a long upper wick is a first sign of buyers paying up
  for nothing. In a downtrend it is a failed attempt to rally. Different meanings,
  same shape.</p>
  <p style="margin-bottom:0"><b style="font-weight:500">A wick on the lower side.</b>
  Exactly the mirror. On a <b>red</b> candle it is always there. On a <b>green</b>
  candle it had to be earned, and its appearance is the single earliest crack in an
  up move. In an uptrend a growing lower wick says sellers are getting a foot in the
  door. In a downtrend a long lower wick says buyers are absorbing the selling.</p>
</div>""")

for d in TYPES:
    A_(card_html(d))
A_('</section>')

# ---- summary table page
A_('<section class="sec"><div class="lbl" style="color:#0EA36A">Section two &middot; summary</div>'
   '<h2>The one page to print</h2>'
   '<div class="lede">Every candle type on a single page. This is the page to keep '
   'beside the screen.</div>'
   '<table><thead><tr><th></th><th>Shape</th><th>Type</th><th>What it means</th>'
   '<th>What I do</th></tr></thead><tbody>')
for d in TYPES:
    A_(summary_row(d))
A_('</tbody></table></section>')

# ---- section 3
A_("""<section class="sec"><div class="lbl" style="color:#E8930C">Section three</div>
<h2>The sequences</h2>
<div class="lede">One candle is a word. A run of them is a sentence. This section is
the part that actually changes how you see the chart.</div>
<div class="callout c-info">
  <div class="ttl">💡 How to use this section</div>
  <p style="margin-bottom:0">Each sequence has a thing that <b>confirms</b> it and a
  thing that <b>kills</b> it. Learn the killers first. Knowing what would prove you
  wrong is worth more than knowing what would prove you right, because you will meet
  the killers far more often.</p>
</div>""")
for d in SEQS:
    A_(seq_html(d))

A_("""<h3>Ranked, most likely to continue &rarr; most likely to turn</h3>
<p style="font-size:9.8pt;color:#4A505C">This ranking is a <b>reading convention</b>.
It has not been measured on data, here or anywhere in this document. Treat it as a
way to order your attention, not as a probability.</p>
<table><thead><tr><th>#</th><th>Sequence</th><th>Reads as</th></tr></thead><tbody>""")
ORDER = [("3.1", "Continues"), ("3.10", "Turns"), ("3.5", "Continues"),
         ("3.2", "Continues"), ("3.8", "Warning"), ("3.9", "Warning"),
         ("3.12", "Warning"), ("3.7", "Neither"), ("3.3", "Turns"),
         ("3.11", "Turns"), ("3.4", "Turns"), ("3.6", "Turns")]
RANKED = [("3.1", "Continues", G), ("3.5", "Continues", G), ("3.2", "Continues", G),
          ("3.8", "Warning", A), ("3.9", "Warning", A), ("3.12", "Warning", A),
          ("3.7", "No bias", A), ("3.3", "Turns", R), ("3.11", "Turns", R),
          ("3.4", "Turns", R), ("3.10", "Turns", R), ("3.6", "Turns", R)]
byn = {d["n"]: d for d in SEQS}
for i, (n, lab, col) in enumerate(RANKED, 1):
    A_(f'<tr><td style="color:#6E3AD6;font-weight:500">{i}</td>'
       f'<td><b style="font-weight:500">{n}</b> &nbsp;{byn[n]["name"]}</td>'
       f'<td><span class="pill" style="background:{col}">{lab}</span></td></tr>')
A_('</tbody></table></section>')

# ---- section 4
A_("""<section class="sec"><div class="lbl" style="color:#6E3AD6">Section four</div>
<h2>Structure and timeframes</h2>
<div class="lede">Where Heikin Ashi belongs in the reading you already do, and where
it must be kept out of it.</div>

<h3>The split, in one line</h3>
<div class="callout c-brand">
  <div class="ttl">🧭 The working rule</div>
  <p style="margin-bottom:0"><b style="font-weight:500">Heikin Ashi tells you who is
  in control. Normal candles tell you where the levels are.</b> Never let the two
  jobs cross over.</p>
</div>

<h3>Reading structure on a Heikin Ashi chart</h3>
<p>Higher highs, higher lows, breaks of structure, changes of character &mdash; you
can see the <i>shape</i> of all of these on Heikin Ashi, and it is often easier to
see than on normal candles. But you cannot take the <i>prices</i> from it.</p>
<p>Here is why, precisely. The high of a Heikin Ashi candle is the highest of three
numbers, and two of them are averages. So a Heikin Ashi swing high can sit
<b>above</b> the real high of that bar, at a price that never traded. If you mark
your swing high there, you have drawn a line at a price that does not exist, and
every decision that follows is built on it.</p>
<div class="callout c-warn">
  <div class="ttl">⚠️ Three things you must always take from normal candles</div>
  <p style="margin-bottom:0">Swing highs and swing lows. Session highs and lows,
  previous day and previous week levels. Gaps &mdash; and note that Heikin Ashi
  <b>never gaps at all</b>, because each candle opens from the last candle's body
  rather than from the market. Sunday's gap simply disappears from this chart.</p>
</div>

<h3>How to work, every session</h3>
<p>1. Mark your levels on the normal candle chart first, before you look at Heikin
Ashi at all. 2. Switch to Heikin Ashi and read control: colour, body size,
wicks. 3. Switch back to normal candles to place anything. The reading and the
levels never come from the same chart.</p>

<h3>Stacking the timeframes</h3>
<p>Read daily first, then 4H, then your trading timeframe. What you are looking for
is agreement.</p>
<table><thead><tr><th>Daily</th><th>4H</th><th>15m</th><th>What it is</th>
<th>What it means</th></tr></thead><tbody>""")
for d1, h4, m15, name, mean, col in MTF:
    def pl(x):
        c = G if x == "Green" else R
        return f'<span class="pill" style="background:{c}">{x}</span>'
    A_(f'<tr><td>{pl(d1)}</td><td>{pl(h4)}</td><td>{pl(m15)}</td>'
       f'<td><b style="font-weight:500">{name}</b></td><td>{mean}</td></tr>')
A_("""</tbody></table>

<h3>Heikin Ashi at a level</h3>
<p>When price arrives at a level you drew from the normal candles, the shapes tell
you whether it is holding or going.</p>
<p><b style="font-weight:500">The level is holding</b> when the candles arriving into
it lose their flat side &mdash; greens start growing lower wicks, bodies shrink, then
you get two-sided wicks or a doji sitting on the level. That is 3.8 and 3.9 arriving
exactly where you expected resistance.</p>
<p><b style="font-weight:500">The level is going</b> when a candle arrives at it with
a flat bottom and a body bigger than its neighbours, and the next one does the same.
Price is not testing the level, it is walking through it.</p>

<h3>Sessions</h3>
<p>Honestly: the formulas do not know what time it is, so nothing about Heikin Ashi
changes by session. What changes is the market underneath it.</p>
<p>In the quiet Asian hours, small bodies and two-sided wicks are usually just low
participation rather than a signal &mdash; section 2.13 is mostly an Asian-session
candle. Around the London and New York opens, the first Heikin Ashi candle of a fast
move is badly affected by the lag, because half of it is still made of the quiet
hour before. Give the open a candle or two before trusting the shape.</p>
<p style="color:#6B7280;font-size:9.6pt">That is a reasoned expectation from how the
formulas work, not a measured result. Nothing in this document has been tested
session by session.</p>
</section>""")

# ---- section 5
A_("""<section class="sec"><div class="lbl" style="color:#E5484D">Section five</div>
<h2>Expert: where it fails</h2>
<div class="lede">Every tool has conditions where it misleads you. These are Heikin
Ashi's, and knowing them is the expert part.</div>

<h3>1. Ranges and chop</h3>
<p>This is the big one. Smoothing makes a range look like a series of small trends.
You get three greens, three reds, three greens, and each run looks like something
starting. Section 3.7 is the pattern to recognise, and the tell is that no body ever
gets large and every candle has wicks on both sides.</p>

<h3>2. Gaps and the Sunday open</h3>
<p>Heikin Ashi has no gaps. Ever. The candle after a gap opens from the middle of
the last body, so a weekend gap in gold simply vanishes from the chart. If gaps
matter to you, they are invisible here.</p>

<h3>3. Thin hours</h3>
<p>In low-liquidity periods you get tiny bodies and long wicks that look like
indecision at a turning point. Usually it is just nobody trading. Check the clock
before you read anything into a small candle.</p>

<h3>4. Fast news candles</h3>
<p>A single enormous news bar gets averaged into the following candles. The chart
shows you a smooth push over three bars where the market actually made one violent
move and then stood still. Your read of "a strong sustained move" is an artefact of
the maths.</p>

<h3>5. The sharp V</h3>
<p>The lag is worst here. On a genuine V reversal the colour can flip several
candles after the low, by which point a large part of the move is gone.</p>

<div class="big-warn">
  <div class="ttl">⚠️ The stop-loss trap, with numbers</div>
  <p>Illustrative figures, chosen to show the mechanism &mdash; not a real bar.</p>
  <p>Say the real bar on gold ran from a low of <b>2,650.00</b> to a high of
  <b>2,656.00</b>, opening at 2,651.00 and closing at 2,655.00. The Heikin Ashi close
  is the average of those four: <b>2,653.00</b>. Suppose the previous Heikin Ashi
  body had its middle at <b>2,648.50</b>. Then the Heikin Ashi low is the lowest of
  (2,650.00, 2,648.50, 2,653.00) = <b>2,648.50</b>.</p>
  <p>So the Heikin Ashi candle shows a low of 2,648.50. <b>Price never went there.</b>
  The real low was 2,650.00, a dollar and a half higher.</p>
  <p style="margin-bottom:0">Put your stop "just below the wick" at 2,648.30 and you
  are risking $1.70 more per ounce than you think, on a level that has no meaning to
  anyone else in the market. Do it the other way &mdash; a Heikin Ashi wick that sits
  <i>inside</i> the real range &mdash; and your stop is above the real low, where it
  will be hit by a move that never actually broke anything.</p>
</div>

<h3>How to live with the lag</h3>
<p>Three things work, and none of them removes it.</p>
<p><b style="font-weight:500">Read the body and the wicks, not the colour.</b> The
warnings in 3.8 and 3.9 arrive while the run is still entirely green. That is the
whole value of the tool: it is early there, and late on the flip.</p>
<p><b style="font-weight:500">Read Heikin Ashi one timeframe up.</b> Use the 4H
Heikin Ashi for control and the 15m normal candles for timing. The lag on the higher
timeframe costs you less, because you were not trying to time it there anyway.</p>
<p><b style="font-weight:500">Let structure decide the turns.</b> A broken level on
the normal chart is a fact. A colour flip is an average crossing another average.</p>

<div class="callout c-good">
  <div class="ttl">✅ What it is genuinely good at, stated narrowly</div>
  <p style="margin-bottom:0">Showing you whether a trend is <b>strengthening or
  weakening while it is still going</b>, through body size and the appearance of
  wicks on the trend side. That is it. It is not better than plain candles at
  spotting turns, finding levels, timing entries, or judging strength at a single
  bar. It is better at one thing: making a fading run visible before the colour
  changes.</p>
</div>
</section>""")

# ---- section 6
A_("""<section class="sec"><div class="lbl" style="color:#0B9BAB">Section six</div>
<h2>The seven questions</h2>
<div class="lede">In order, every time you look at the chart. Short enough to
memorise, which is the only reason it will actually get used.</div>
<div class="chk">""")
for i, (q, a) in enumerate(CHECK, 1):
    A_(f'<div class="item"><div class="n">{i}</div><div><div class="q">{q}</div>'
       f'<div class="a">{a}</div></div></div>')
A_("""</div>
<div class="callout c-note" style="margin-top:20px">
  <div class="ttl">📌 The honest answer to question five</div>
  <p style="margin-bottom:0">"I cannot name the sequence" is the correct answer far
  more often than any of the twelve names. When it is the answer, the action is
  nothing. Most of the money lost reading charts is lost during the bars where there
  was nothing to read.</p>
</div>
</section>""")

# ---- section 7
A_("""<section class="sec"><div class="lbl" style="color:#E5484D">Section seven</div>
<h2>What is not true</h2>
<div class="lede">Six claims you will meet everywhere. Each one is either wrong or
much narrower than it sounds.</div>""")
for claim, why in NOTTRUE:
    A_(f'<div class="nt"><div class="claim">{claim}</div><div>{why}</div></div>')

A_("""<div class="callout c-brand" style="margin-top:22px">
  <div class="ttl">🧾 What this document is, exactly</div>
  <p>A way of <b>reading</b> a chart. Every shape and every sequence in here is a
  description of what the candles are doing and a convention for what traders take
  it to mean.</p>
  <p style="margin-bottom:0">None of it has been backtested, here or anywhere else in
  this project. No win rate is claimed, no edge is claimed, and no sequence in
  section three has been counted on real data. If you want to know whether any of it
  is true on gold, it has to be measured &mdash; and until it is, treat every ranking
  and every "usually" in this document as a habit of reading rather than a fact about
  the market.</p>
</div>
<footer>Reading the chart with Heikin Ashi candles &middot; XAUUSD &middot; a guide to
reading, not a trading strategy. Prices shown in section five are illustrative.</footer>
</section>""")

import io, os
out = os.environ.get("OUT", "heikin_ashi_guide.html")
with io.open(out, "w", encoding="utf-8") as f:
    f.write("".join(html))
print("wrote", out, sum(len(x) for x in html), "chars")
