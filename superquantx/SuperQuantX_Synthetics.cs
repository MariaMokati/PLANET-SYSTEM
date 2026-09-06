// =============================================================================
// SuperQuantX_Synthetics    cTrader 5.9.10 / cAlgo.API
//
// BUILD 2. This is the repair build. Panel header reads b2.
//
// WHAT THE b1 PANELS SHOWED, AND WHAT IS FIXED HERE
//
//   Boom 600 m15   93 trades, 3.2%, -94.55 R, Spike fade the only engine
//   Volatility 10 m5   559 trades, 23.6%, -175.60 R, Sigma the only engine
//   Drift rider, the only engine that ever measured positive, fired ZERO
//   times on both charts.
//
// 1  SPIKE FADE WAS TRADING THE WRONG OBJECT.  Spikes are detected on m1 but
//    the fade took its direction, its stop and its target from the m15 CANDLE
//    that contained the spike. On Boom a candle can hold an up spike and still
//    close down, and the engine then BOUGHT a down-drifting instrument. That is
//    the 3.2%. Each spike now carries its own direction, high, low and range,
//    and the fade is built from the spike.
//
// 2  DRIFT RIDER COULD NOT FIRE ON m15.  It required N consecutive counter
//    drift closes. On an instrument that grinds one way, three consecutive
//    counter closes almost never happen on m15, which is why it read zero.
//    The pullback is now measured as a retracement in ATR from the recent
//    drift extreme, which fires on any timeframe. The old rule is still
//    selectable as ConsecutiveCloses so the two can be compared.
//
// 3  THE TIMEFRAME WARNING WAS A LABEL, NOT A GUARD.  The panel printed
//    TIMEFRAME TOO HIGH in red and then traded anyway. When a chart bar is
//    larger than half the mean spike interval, both spike engines are now
//    blocked, not just the clock, and the guard is computed even when no spike
//    has been found yet.
//
// 4  SPIKES ONLY EXISTED FOR THE LAST 42 DAYS.  The m1 loader stopped at
//    60000 bars. It now loads what the window needs and the panel reports the
//    coverage in red when it falls short.
//
// 5  EVERY ENABLED ENGINE NOW TAKES A ROW, EVEN AT ZERO.  Drift rider was
//    invisible rather than shown as zero, which is why the failure was not
//    obvious. There is a gate block underneath naming why each engine refused.
//
// 6  REFUSED IS PER ENGINE.  It was one counter for the whole file, so the
//    number could not be attributed to anything.
//
// 7  R TOTALS NO LONGER MIX IN EXPIRED TRADES.  net summed every outcome while
//    n excluded expired, so Total R and Average trade were on different
//    populations.
//
// 8  SINCE LAST IS NO LONGER BARS DIVIDED BY MINUTES.
//
// 9  SIGMA REVERSION SHIPS OFF.  A Volatility index is a driftless random walk
//    by construction. Mean reversion on one has an expectancy of exactly zero
//    before costs and negative after. That is a premise error, not a tuning
//    error, and no setting repairs it. The parameter is renamed so the new
//    default actually reaches a chart that already has this indicator on it.
//    Turn it on if you want to watch it prove the point.
//
// CARRIED OVER, UNCHANGED
//   Bars evaluated once after close, no forward index reads anywhere, history
//   loader fills the requested window, dealing cost read from the broker,
//   entry at the close of the signal bar, stop tested before target inside the
//   same bar.
// =============================================================================

using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo
{
    public enum SynthFamily { Auto, Boom, Crash, Jump, Volatility, Step, RangeBreak }

    public enum PanelSpot { TopRight, TopLeft, BottomRight, BottomLeft }

    public enum WindowMode { Days, Bars }

    public enum DriftPull { Retracement, ConsecutiveCloses }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SuperQuantX_Synthetics : Indicator
    {
        // ===================== GENERAL ======================================
        [Parameter("Family", Group = "General", DefaultValue = SynthFamily.Auto)]
        public SynthFamily FamilyIn { get; set; }

        [Parameter("History Preload Bars", Group = "General", DefaultValue = 30000, MinValue = 500, MaxValue = 200000)]
        public int PreloadBars { get; set; }

        [Parameter("Analysis Window", Group = "General", DefaultValue = WindowMode.Days)]
        public WindowMode WinMode { get; set; }

        [Parameter("Analyse Last N Days", Group = "General", DefaultValue = 180, MinValue = 5, MaxValue = 3000)]
        public int AnalyseDays { get; set; }

        // Slides the window back so the engines can be scored on data they were
        // never tuned against.
        [Parameter("Window Offset Days", Group = "General", DefaultValue = 0, MinValue = 0, MaxValue = 3000)]
        public int OffsetDays { get; set; }

        [Parameter("ATR Period", Group = "General", DefaultValue = 14, MinValue = 2, MaxValue = 200)]
        public int AtrPeriod { get; set; }

        // A hard ceiling across every engine. Three is three, whichever engine
        // finds them.
        [Parameter("Max Signals Per Day", Group = "General", DefaultValue = 3, MinValue = 1, MaxValue = 50)]
        public int MaxPerDay { get; set; }

        [Parameter("Signals Per Engine Per Day", Group = "General", DefaultValue = 3, MinValue = 1, MaxValue = 100)]
        public int PerEngineDaily { get; set; }

        // Nothing is taken below this. At 2R breakeven is 33.3%.
        [Parameter("Min Reward R", Group = "General", DefaultValue = 2.00, MinValue = 0.50, MaxValue = 20.00)]
        public double MinRewardR { get; set; }

        [Parameter("Min Bars Between Same Engine", Group = "General", DefaultValue = 2, MinValue = 0, MaxValue = 200)]
        public int MinGapSameEngine { get; set; }

        // b2. The old build printed TIMEFRAME TOO HIGH in red and then traded
        // through it. With this on, both spike engines are blocked outright
        // when a chart bar is wider than half the measured spike interval,
        // because the spike is then inside the bar you would be entering on.
        [Parameter("Block Spike Engines On Wrong TF", Group = "General", DefaultValue = true)]
        public bool TfGuardOn { get; set; }

        [Parameter("Manual Spread (price)", Group = "General", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10000.0)]
        public double ManualSpread { get; set; }

        [Parameter("Use Broker Spread", Group = "General", DefaultValue = true)]
        public bool BrokerSpread { get; set; }

        // ===================== SPIKE DETECTION ==============================
        // A spike is a bar whose range dwarfs the recent typical range, judged
        // against the MEDIAN because a mean is dragged upward by the very
        // spikes being measured. Detection runs on m1 and is mapped onto the
        // chart, so the count is right on any timeframe.
        [Parameter("Detect Spikes On m1", Group = "Spike Detection", DefaultValue = true)]
        public bool SpikeOnM1 { get; set; }

        // b2. Was a hard 60000 m1 bars, which is 41.7 days on a 24/7 synthetic.
        // Over a 180 day window that left 138 days with no spikes at all.
        [Parameter("Spike History Days", Group = "Spike Detection", DefaultValue = 180, MinValue = 5, MaxValue = 400)]
        public int M1Days { get; set; }

        [Parameter("Spike Range Multiple", Group = "Spike Detection", DefaultValue = 4.0, MinValue = 1.5, MaxValue = 50.0)]
        public double SpikeMult { get; set; }

        [Parameter("Typical Range Lookback", Group = "Spike Detection", DefaultValue = 200, MinValue = 20, MaxValue = 3000)]
        public int TypicalLookback { get; set; }

        [Parameter("Spike Needs Directional Body", Group = "Spike Detection", DefaultValue = true)]
        public bool SpikeNeedsBody { get; set; }

        [Parameter("Spike Min Body %", Group = "Spike Detection", DefaultValue = 40, MinValue = 0, MaxValue = 99)]
        public int SpikeMinBodyPct { get; set; }

        // ===================== SPIKE CLOCK ==================================
        [Parameter("Spike Clock Signals", Group = "Spike Clock", DefaultValue = true)]
        public bool UseSpikeClock { get; set; }

        [Parameter("Arm At Share Of Mean Interval", Group = "Spike Clock", DefaultValue = 0.80, MinValue = 0.10, MaxValue = 5.00)]
        public double ArmShare { get; set; }

        [Parameter("Stop (ATR)", Group = "Spike Clock", DefaultValue = 1.20, MinValue = 0.10, MaxValue = 20.00)]
        public double ClockStopAtr { get; set; }

        [Parameter("Target As Share Of Mean Spike", Group = "Spike Clock", DefaultValue = 0.60, MinValue = 0.10, MaxValue = 3.00)]
        public double ClockTargetShare { get; set; }

        [Parameter("Give Up After (bars)", Group = "Spike Clock", DefaultValue = 60, MinValue = 5, MaxValue = 2000)]
        public int ClockGiveUp { get; set; }

        [Parameter("Min Spikes Before Trading", Group = "Spike Clock", DefaultValue = 8, MinValue = 3, MaxValue = 200)]
        public int MinSpikeSample { get; set; }

        // ===================== SPIKE FADE ===================================
        // The bar after a spike, back toward the drift. b2 builds direction,
        // stop and target from the SPIKE, not from the chart candle that
        // happened to contain it.
        [Parameter("Spike Fade Signals", Group = "Spike Fade", DefaultValue = true)]
        public bool UseFade { get; set; }

        [Parameter("Fade Stop Beyond Spike (ATR)", Group = "Spike Fade", DefaultValue = 0.40, MinValue = 0.00, MaxValue = 5.00)]
        public double FadeStopPad { get; set; }

        [Parameter("Fade Target As Share Of Spike", Group = "Spike Fade", DefaultValue = 0.50, MinValue = 0.05, MaxValue = 3.00)]
        public double FadeTargetShare { get; set; }

        [Parameter("Fade Min Reward", Group = "Spike Fade", DefaultValue = 2.00, MinValue = 0.20, MaxValue = 10.00)]
        public double FadeMinR { get; set; }

        // ===================== DRIFT RIDER ==================================
        // Boom grinds down and Crash grinds up. Trade that grind after a
        // counter move, stop beyond the counter move so the grind itself does
        // not stop you out.
        [Parameter("Drift Rider Signals", Group = "Drift Rider", DefaultValue = true)]
        public bool UseDrift { get; set; }

        // b2. ConsecutiveCloses is the old rule: every one of the last N bars
        // had to close counter to the drift. On an instrument that grinds one
        // way that almost never happens below h1, which is why the engine read
        // zero on m15. Retracement measures the pullback in ATR from the recent
        // drift extreme instead, so it fires on any timeframe. Both are here so
        // they can be compared on the same chart.
        [Parameter("Drift Pullback Mode", Group = "Drift Rider", DefaultValue = DriftPull.Retracement)]
        public DriftPull DriftMode { get; set; }

        [Parameter("Drift Pullback Window", Group = "Drift Rider", DefaultValue = 8, MinValue = 2, MaxValue = 60)]
        public int DriftPullWin { get; set; }

        [Parameter("Drift Min Pullback (ATR)", Group = "Drift Rider", DefaultValue = 0.80, MinValue = 0.05, MaxValue = 10.00)]
        public double DriftPullMinAtr { get; set; }

        [Parameter("Drift Pullback Bars (old mode)", Group = "Drift Rider", DefaultValue = 3, MinValue = 1, MaxValue = 30)]
        public int DriftPullBars { get; set; }

        [Parameter("Drift Stop Pad (ATR)", Group = "Drift Rider", DefaultValue = 0.60, MinValue = 0.05, MaxValue = 10.00)]
        public double DriftStopPad { get; set; }

        [Parameter("Drift Reward", Group = "Drift Rider", DefaultValue = 2.00, MinValue = 0.20, MaxValue = 10.00)]
        public double DriftR { get; set; }

        [Parameter("Block Drift While Spike Overdue", Group = "Drift Rider", DefaultValue = true)]
        public bool DriftAvoidOverdue { get; set; }

        // ===================== SIGMA REVERSION ==============================
        // Renamed from "Sigma Reversion Signals" so the new default of OFF
        // actually reaches a chart that already carries this indicator.
        // A Volatility index is a driftless random walk. Mean reversion on one
        // has an expectancy of exactly zero before costs and negative after.
        // Measured 23.6% against a 31.5% breakeven over 559 trades.
        [Parameter("Sigma Reversion (no edge, off)", Group = "Sigma Reversion", DefaultValue = false)]
        public bool SigmaOn { get; set; }

        [Parameter("Mean Length", Group = "Sigma Reversion", DefaultValue = 50, MinValue = 5, MaxValue = 1000)]
        public int SigmaLen { get; set; }

        [Parameter("Entry At Sigma", Group = "Sigma Reversion", DefaultValue = 3.00, MinValue = 0.50, MaxValue = 8.00)]
        public double SigmaEntry { get; set; }

        [Parameter("Stop At Sigma", Group = "Sigma Reversion", DefaultValue = 4.20, MinValue = 0.60, MaxValue = 12.00)]
        public double SigmaStop { get; set; }

        [Parameter("Target Sigma From Mean", Group = "Sigma Reversion", DefaultValue = 0.40, MinValue = 0.00, MaxValue = 4.00)]
        public double SigmaTarget { get; set; }

        // ===================== RANGE BREAK ==================================
        [Parameter("Range Break Signals", Group = "Range Break", DefaultValue = true)]
        public bool UseRangeBreak { get; set; }

        [Parameter("Range Window", Group = "Range Break", DefaultValue = 24, MinValue = 5, MaxValue = 300)]
        public int RangeWindow { get; set; }

        [Parameter("Range Max Height (ATR)", Group = "Range Break", DefaultValue = 2.20, MinValue = 0.30, MaxValue = 10.00)]
        public double RangeMaxAtr { get; set; }

        [Parameter("Break Buffer (ATR)", Group = "Range Break", DefaultValue = 0.15, MinValue = 0.00, MaxValue = 3.00)]
        public double BreakBuffer { get; set; }

        [Parameter("Range Break Reward", Group = "Range Break", DefaultValue = 2.50, MinValue = 0.20, MaxValue = 10.00)]
        public double RangeR { get; set; }

        // ===================== TRACKING =====================================
        [Parameter("Give Up After (days)", Group = "Tracking", DefaultValue = 10, MinValue = 1, MaxValue = 400)]
        public int TradeWindowDays { get; set; }

        [Parameter("Use Break Even", Group = "Tracking", DefaultValue = false)]
        public bool UseBreakEven { get; set; }

        [Parameter("Break Even At R", Group = "Tracking", DefaultValue = 1.00, MinValue = 0.10, MaxValue = 10.00)]
        public double BreakEvenAtR { get; set; }

        // ===================== VISUALS ======================================
        [Parameter("Show Signals", Group = "Visuals", DefaultValue = true)]
        public bool ShowSignals { get; set; }

        [Parameter("Show Stop And Target", Group = "Visuals", DefaultValue = true)]
        public bool ShowRisk { get; set; }

        [Parameter("Mark Spikes", Group = "Visuals", DefaultValue = false)]
        public bool MarkSpikes { get; set; }

        [Parameter("Marker Size", Group = "Visuals", DefaultValue = 10, MinValue = 5, MaxValue = 40)]
        public int MarkerSize { get; set; }

        [Parameter("Label Size", Group = "Visuals", DefaultValue = 9, MinValue = 5, MaxValue = 24)]
        public int LabelSize { get; set; }

        [Parameter("Buy Colour", Group = "Visuals", DefaultValue = "DodgerBlue")]
        public Color BuyColor { get; set; }

        [Parameter("Sell Colour", Group = "Visuals", DefaultValue = "DeepPink")]
        public Color SellColor { get; set; }

        [Parameter("Stop Colour", Group = "Visuals", DefaultValue = "OrangeRed")]
        public Color StopColor { get; set; }

        [Parameter("Target Colour", Group = "Visuals", DefaultValue = "LimeGreen")]
        public Color TargetColor { get; set; }

        [Parameter("Spike Colour", Group = "Visuals", DefaultValue = "Gold")]
        public Color SpikeColor { get; set; }

        // ===================== DASHBOARD ====================================
        [Parameter("Show Dashboard", Group = "Dashboard", DefaultValue = true)]
        public bool ShowPanel { get; set; }

        [Parameter("Panel Corner", Group = "Dashboard", DefaultValue = PanelSpot.TopRight)]
        public PanelSpot PanelWhere { get; set; }

        [Parameter("Show Engine Gates", Group = "Dashboard", DefaultValue = true)]
        public bool ShowGates { get; set; }

        [Parameter("Show Day Table", Group = "Dashboard", DefaultValue = true)]
        public bool ShowDayTable { get; set; }

        [Parameter("Day Table Corner", Group = "Dashboard", DefaultValue = PanelSpot.BottomLeft)]
        public PanelSpot DayWhere { get; set; }

        [Parameter("Panel Font Size", Group = "Dashboard", DefaultValue = 7, MinValue = 5, MaxValue = 16)]
        public int PanelFont { get; set; }

        [Parameter("Panel Background", Group = "Dashboard", DefaultValue = "White")]
        public Color PanelBack { get; set; }

        [Parameter("Panel Text", Group = "Dashboard", DefaultValue = "Black")]
        public Color PanelText { get; set; }

        [Parameter("Section Bar", Group = "Dashboard", DefaultValue = "RebeccaPurple")]
        public Color SectionBack { get; set; }

        [Parameter("Section Text", Group = "Dashboard", DefaultValue = "White")]
        public Color SectionText { get; set; }

        [Parameter("Positive", Group = "Dashboard", DefaultValue = "SeaGreen")]
        public Color PanelPos { get; set; }

        [Parameter("Negative", Group = "Dashboard", DefaultValue = "Crimson")]
        public Color PanelNeg { get; set; }

        [Parameter("Muted", Group = "Dashboard", DefaultValue = "Gray")]
        public Color PanelMuted { get; set; }

        // ===================== STATE ========================================
        private AverageTrueRange _atr;
        private SynthFamily _fam;
        private int _lastClosed = -1;
        private long _barDurTicks;
        private int _uid;
        private int _histBars;
        private double _histDays;
        private bool _histShort;

        // ---- spike measurement -------------------------------------------
        // b2. A spike carries its own geometry. The chart candle that contains
        // it is not the spike and must not be used to build a trade.
        private class Spike
        {
            public int Bar;
            public DateTime When;
            public int Dir;
            public double High, Low, Range;
        }
        private readonly List<Spike> _spikes = new List<Spike>();
        private double _meanInterval;        // always minutes
        private double _meanSpike;
        private Bars _m1;
        private int _m1Seen;
        private double _m1Days;
        private bool _m1Short;
        private bool _tfTooHigh;

        // ---- per engine bookkeeping ---------------------------------------
        // 0 clock, 1 fade, 2 drift, 3 sigma, 4 range break
        private const int Engines = 5;
        private readonly int[] _capUsed = new int[Engines];
        private readonly int[] _lastSrcBar = new int[Engines];
        private readonly int[] _refusedR = new int[Engines];
        private readonly int[] _refusedCap = new int[Engines];
        private readonly int[] _refusedGap = new int[Engines];
        private readonly int[] _fired = new int[Engines];
        private DateTime _capDay = DateTime.MinValue;
        private int _dayUsed;

        // ---- engine gate counters -----------------------------------------
        private int _gTf, _gNoSpike, _gSample;
        private int _dNoPull, _dNoTurn, _dOverdue;
        private int _fNoSpike, _fWeakR;
        private int _cNotDue;

        // overrides set by an engine just before it raises a signal
        private double _stopPx = double.NaN;
        private double _targetR = double.NaN;
        private double _targetPx = double.NaN;

        private class Trade
        {
            public int Bar, Src;
            public string Id;
            public bool IsLong;
            public double Entry, Stop, Target, Risk, Cost, MaxFav;
            public bool Closed, BeMoved;
        }
        private readonly List<Trade> _open = new List<Trade>();

        private class Res
        {
            public DateTime Day;
            public int Src, Outcome;      // 1 win, 2 break even, 3 expired, 0 loss
            public double R;
        }
        private readonly List<Res> _log = new List<Res>();

        private const int Rows = 64;
        private const int Cols = 4;
        private bool _built;
        private TextBlock[,] _cell;
        private Grid[,] _cellBg;

        private const int DayRows = 10;
        private const int DayCols = 5;
        private bool _dayBuilt;
        private TextBlock[,] _dcell;
        private Grid[,] _dcellBg;

        // ===================== INIT =========================================
        protected override void Initialize()
        {
            int guard = 0;
            while (guard++ < 400)
            {
                int need = PreloadBars;
                if (WinMode == WindowMode.Days && Bars.Count > 80)
                {
                    int want = DaysToBars(AnalyseDays + OffsetDays) + 1200;
                    if (want > need) need = want;
                }
                if (Bars.Count >= need) break;
                if (Bars.LoadMoreHistory() == 0) break;
            }

            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            for (int k = 0; k < Engines; k++) _lastSrcBar[k] = -1000000;

            _fam = FamilyIn == SynthFamily.Auto ? DetectFamily() : FamilyIn;

            if (SpikeOnM1 && HasSpikes())
            {
                _m1 = MarketData.GetBars(TimeFrame.Minute);

                // b2. The old loader stopped at a flat 60000 m1 bars, which is
                // 41.7 days on an instrument that never closes. Ask for what the
                // analysis window actually needs, and report the shortfall
                // rather than quietly measuring a fraction of the window.
                long want = (long)M1Days * 1440L + 5000L;
                if (want > 600000L) want = 600000L;

                int g2 = 0;
                while (g2++ < 3000 && _m1.Count < want)
                {
                    if (_m1.LoadMoreHistory() == 0) break;
                }
            }
        }

        // The name decides direction only. Every number is measured.
        private SynthFamily DetectFamily()
        {
            string n = SymbolName.ToUpperInvariant();
            if (n.Contains("BOOM")) return SynthFamily.Boom;
            if (n.Contains("CRASH")) return SynthFamily.Crash;
            if (n.Contains("JUMP")) return SynthFamily.Jump;
            if (n.Contains("RANGE")) return SynthFamily.RangeBreak;
            if (n.Contains("STEP")) return SynthFamily.Step;
            if (n.Contains("VOLATILITY") || n.Contains("VOL ")) return SynthFamily.Volatility;
            return SynthFamily.Volatility;
        }

        private int SpikeDir()
        {
            if (_fam == SynthFamily.Boom) return 1;
            if (_fam == SynthFamily.Crash) return -1;
            return 0;
        }

        private int DriftDir()
        {
            if (_fam == SynthFamily.Boom) return -1;
            if (_fam == SynthFamily.Crash) return 1;
            return 0;
        }

        private bool HasSpikes()
        {
            return _fam == SynthFamily.Boom || _fam == SynthFamily.Crash || _fam == SynthFamily.Jump;
        }

        private bool IsContinuous()
        {
            return _fam == SynthFamily.Volatility || _fam == SynthFamily.Step;
        }

        // ===================== MAIN =========================================
        public override void Calculate(int index)
        {
            int lastClosed = index - 1;
            while (_lastClosed < lastClosed)
            {
                _lastClosed++;
                ProcessBar(_lastClosed);
            }

            if (IsLastBar)
            {
                _histBars = Bars.Count;
                _histDays = Bars.Count > 1
                    ? (Bars.OpenTimes[Bars.Count - 1] - Bars.OpenTimes[0]).TotalDays : 0.0;
                _histShort = WinMode == WindowMode.Days &&
                             _histDays < (AnalyseDays + OffsetDays) - 1.0;

                if (_m1 != null && _m1.Count > 1)
                {
                    _m1Days = (_m1.OpenTimes[_m1.Count - 1] - _m1.OpenTimes[0]).TotalDays;
                    _m1Short = _m1Days < (AnalyseDays + OffsetDays) - 1.0;
                }

                UpdateTfGuard();
                DrawPanel(index);
                DrawDayPanel();
            }
        }

        private void ProcessBar(int i)
        {
            if (i < AtrPeriod + 5) return;

            // spike statistics are built from the whole loaded history, so the
            // mean interval is stable before the analysis window even opens
            UpdateSpikes(i);
            UpdateTfGuard();

            if (WinMode == WindowMode.Days)
            {
                if (Bars.OpenTimes[i] < WindowStart() || Bars.OpenTimes[i] > WindowEnd()) return;
            }
            else if (i < Bars.Count - PreloadBars) return;

            UpdateTrades(i);

            // b2. Drift rider runs FIRST on Boom and Crash. It is the only
            // engine that has ever measured positive, and under a 3 per day cap
            // whichever engine runs first takes the slots.
            if (HasSpikes())
            {
                DriftRider(i);
                SpikeFade(i);
                SpikeClock(i);
            }
            if (IsContinuous()) SigmaReversion(i);
            if (_fam == SynthFamily.RangeBreak || _fam == SynthFamily.Step) RangeBreakEngine(i);
        }

        private DateTime WindowStart()
        {
            if (Bars.Count < 2) return DateTime.MinValue;
            return Bars.OpenTimes[Bars.Count - 1].AddDays(-AnalyseDays - OffsetDays);
        }

        private DateTime WindowEnd()
        {
            if (Bars.Count < 2) return DateTime.MaxValue;
            return Bars.OpenTimes[Bars.Count - 1].AddDays(-OffsetDays);
        }

        // ===================== SPIKE MEASUREMENT ============================
        // Median, not mean. The mean of recent ranges is dragged upward by the
        // very spikes being detected, which would hide the next one.
        private double TypicalRange(int i)
        {
            int start = Math.Max(0, i - TypicalLookback);
            int n = i - start;
            if (n < 10) return double.NaN;

            List<double> v = new List<double>(n);
            for (int k = start; k < i; k++) v.Add(Bars.HighPrices[k] - Bars.LowPrices[k]);
            v.Sort();
            return v[v.Count / 2];
        }

        private void UpdateSpikes(int i)
        {
            if (!HasSpikes()) return;
            if (SpikeOnM1 && _m1 != null) { ScanM1(i); return; }

            double typ = TypicalRange(i);
            if (double.IsNaN(typ) || typ <= 0) return;

            double range = Bars.HighPrices[i] - Bars.LowPrices[i];
            if (range < SpikeMult * typ) return;

            int dir = Bars.ClosePrices[i] >= Bars.OpenPrices[i] ? 1 : -1;

            if (SpikeNeedsBody)
            {
                double body = Math.Abs(Bars.ClosePrices[i] - Bars.OpenPrices[i]);
                if (range <= 0 || body / range * 100.0 < SpikeMinBodyPct) return;
            }

            int want = SpikeDir();
            if (want != 0 && dir != want) return;

            RecordSpike(i, Bars.OpenTimes[i], dir,
                        Bars.HighPrices[i], Bars.LowPrices[i], range);
        }

        // Every m1 bar that closed inside chart bar i is tested against the m1
        // median range, so a spike is found whatever the chart timeframe is.
        private void ScanM1(int i)
        {
            DateTime t0 = Bars.OpenTimes[i];
            DateTime t1 = t0.Add(BarDuration());

            while (_m1Seen < _m1.Count - 1)
            {
                DateTime bt = _m1.OpenTimes[_m1Seen];
                if (bt >= t1) break;
                if (bt < t0) { _m1Seen++; continue; }

                int k = _m1Seen;
                _m1Seen++;

                int start = Math.Max(0, k - TypicalLookback);
                int n = k - start;
                if (n < 20) continue;

                List<double> v = new List<double>(n);
                for (int q = start; q < k; q++) v.Add(_m1.HighPrices[q] - _m1.LowPrices[q]);
                v.Sort();
                double typ = v[v.Count / 2];
                if (typ <= 0) continue;

                double range = _m1.HighPrices[k] - _m1.LowPrices[k];
                if (range < SpikeMult * typ) continue;

                int dir = _m1.ClosePrices[k] >= _m1.OpenPrices[k] ? 1 : -1;
                if (SpikeNeedsBody)
                {
                    double body = Math.Abs(_m1.ClosePrices[k] - _m1.OpenPrices[k]);
                    if (body / range * 100.0 < SpikeMinBodyPct) continue;
                }

                int want = SpikeDir();
                if (want != 0 && dir != want) continue;

                // the spike keeps its OWN high, low, range and direction
                RecordSpike(i, _m1.OpenTimes[k], dir,
                            _m1.HighPrices[k], _m1.LowPrices[k], range);
            }
        }

        private void RecordSpike(int chartBar, DateTime when, int dir,
                                 double hi, double lo, double range)
        {
            Spike s = new Spike
            {
                Bar = chartBar,
                When = when,
                Dir = dir,
                High = hi,
                Low = lo,
                Range = range
            };
            _spikes.Add(s);
            while (_spikes.Count > 1000) _spikes.RemoveAt(0);

            // mean interval in MINUTES, from real gaps only. No placeholder
            // zero is pushed for the first spike any more.
            double sum = 0.0;
            int cnt = 0;
            for (int q = 1; q < _spikes.Count; q++)
            {
                double mins = (_spikes[q].When - _spikes[q - 1].When).TotalMinutes;
                if (mins > 0) { sum += mins; cnt++; }
            }
            _meanInterval = cnt == 0 ? 0.0 : sum / cnt;

            double ss = 0.0;
            for (int q = 0; q < _spikes.Count; q++) ss += _spikes[q].Range;
            _meanSpike = _spikes.Count == 0 ? 0.0 : ss / _spikes.Count;

            if (MarkSpikes && ShouldDraw(chartBar))
            {
                ChartText t = Chart.DrawText("SYNSPK_" + when.Ticks.ToString(), "\u25CF",
                    Bars.OpenTimes[chartBar], dir > 0 ? hi : lo, SpikeColor);
                t.FontSize = MarkerSize;
                t.IsInteractive = false;
            }
        }

        // b2. Computed in one place and always, so the guard is right even when
        // the detector has found nothing at all.
        private void UpdateTfGuard()
        {
            _tfTooHigh = false;
            if (!HasSpikes()) return;
            if (_meanInterval <= 0) return;
            _tfTooHigh = BarDuration().TotalMinutes > _meanInterval / 2.0;
        }

        private int SuggestedTfMinutes()
        {
            if (_meanInterval <= 0) return 1;
            int m = (int)Math.Round(_meanInterval / 4.0);
            return m < 1 ? 1 : m;
        }

        private Spike LastSpike()
        {
            return _spikes.Count == 0 ? null : _spikes[_spikes.Count - 1];
        }

        private int BarsSinceSpike(int i)
        {
            Spike s = LastSpike();
            return s == null ? -1 : i - s.Bar;
        }

        // Elapsed minutes since the last spike, correctly converted from chart
        // bars. The old build compared bars against minutes.
        private double MinutesSinceSpike(int i)
        {
            int b = BarsSinceSpike(i);
            if (b < 0) return -1.0;
            return b * BarDuration().TotalMinutes;
        }

        private bool SpikeOverdue(int i)
        {
            if (_spikes.Count < MinSpikeSample || _meanInterval <= 0) return false;
            if (TfGuardOn && _tfTooHigh) return false;

            double mins = MinutesSinceSpike(i);
            if (mins < 0) return false;

            return mins >= ArmShare * _meanInterval;
        }

        // ===================== ENGINE 0  SPIKE CLOCK ========================
        private void SpikeClock(int i)
        {
            if (!UseSpikeClock) return;
            if (TfGuardOn && _tfTooHigh) { _gTf++; return; }
            if (_spikes.Count < MinSpikeSample) { _gSample++; return; }
            if (!SpikeOverdue(i)) { _cNotDue++; return; }
            if (_meanSpike <= 0) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            int dir = SpikeDir();
            if (dir == 0) dir = Bars.ClosePrices[i] >= Bars.OpenPrices[i] ? 1 : -1;   // Jump
            bool isLong = dir > 0;

            double entry = Bars.ClosePrices[i];
            double risk = ClockStopAtr * atr;

            _stopPx = isLong ? entry - risk : entry + risk;
            _targetPx = isLong ? entry + ClockTargetShare * _meanSpike
                               : entry - ClockTargetShare * _meanSpike;

            Raise(i, isLong, entry, 0, "SPK");
        }

        // ===================== ENGINE 1  SPIKE FADE =========================
        // b2. Direction, stop and target all come from the spike itself. The
        // old build read them off the chart candle that contained the spike,
        // which on Boom can close DOWN around an UP spike and made the engine
        // buy a down-drifting instrument. That is the measured 3.2%.
        private void SpikeFade(int i)
        {
            if (!UseFade) return;
            if (TfGuardOn && _tfTooHigh) { _gTf++; return; }

            Spike s = LastSpike();
            if (s == null) { _fNoSpike++; return; }
            if (i != s.Bar + 1) return;
            if (s.Range <= 0) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            bool isLong = s.Dir < 0;                   // fade the spike
            double entry = Bars.ClosePrices[i];

            _stopPx = isLong ? s.Low - FadeStopPad * atr
                             : s.High + FadeStopPad * atr;

            double risk = Math.Abs(entry - _stopPx);
            if (risk <= 0) { Clear(); return; }

            double move = FadeTargetShare * s.Range;
            if (move / risk < FadeMinR) { _fWeakR++; Clear(); return; }

            _targetPx = isLong ? entry + move : entry - move;
            Raise(i, isLong, entry, 1, "FADE");
        }

        // ===================== ENGINE 2  DRIFT RIDER ========================
        // b2. Retracement mode. The old rule needed every one of the last N
        // bars to close counter to the drift, which on a one way grind almost
        // never happens below h1 and is why this read zero on m15.
        private void DriftRider(int i)
        {
            if (!UseDrift) return;

            int dir = DriftDir();
            if (dir == 0) return;
            if (DriftAvoidOverdue && SpikeOverdue(i)) { _dOverdue++; return; }

            int win = DriftMode == DriftPull.Retracement ? DriftPullWin : DriftPullBars;
            if (i < win + 2) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            bool isLong = dir > 0;
            double ext;

            if (DriftMode == DriftPull.ConsecutiveCloses)
            {
                for (int k = i - DriftPullBars; k < i; k++)
                {
                    bool up = Bars.ClosePrices[k] > Bars.OpenPrices[k];
                    if (isLong ? up : !up) { _dNoPull++; return; }
                }
                bool with = isLong ? Bars.ClosePrices[i] > Bars.OpenPrices[i]
                                   : Bars.ClosePrices[i] < Bars.OpenPrices[i];
                if (!with) { _dNoTurn++; return; }

                ext = isLong ? Bars.LowPrices[i] : Bars.HighPrices[i];
                for (int k = i - DriftPullBars; k <= i; k++)
                {
                    if (isLong && Bars.LowPrices[k] < ext) ext = Bars.LowPrices[k];
                    if (!isLong && Bars.HighPrices[k] > ext) ext = Bars.HighPrices[k];
                }
            }
            else
            {
                int from = Math.Max(0, i - win);

                // the extreme the drift reached, then how far price pulled back
                // against the drift from that point
                int extIdx = from;
                for (int k = from; k <= i; k++)
                {
                    if (isLong)
                    {
                        if (Bars.LowPrices[k] < Bars.LowPrices[extIdx]) extIdx = k;
                    }
                    else
                    {
                        if (Bars.HighPrices[k] > Bars.HighPrices[extIdx]) extIdx = k;
                    }
                }

                double pullTo = isLong ? Bars.HighPrices[extIdx] : Bars.LowPrices[extIdx];
                for (int k = extIdx; k <= i; k++)
                {
                    if (isLong && Bars.HighPrices[k] > pullTo) pullTo = Bars.HighPrices[k];
                    if (!isLong && Bars.LowPrices[k] < pullTo) pullTo = Bars.LowPrices[k];
                }

                double pull = isLong ? pullTo - Bars.LowPrices[extIdx]
                                     : Bars.HighPrices[extIdx] - pullTo;

                if (pull < DriftPullMinAtr * atr) { _dNoPull++; return; }

                bool with = isLong ? Bars.ClosePrices[i] > Bars.OpenPrices[i]
                                   : Bars.ClosePrices[i] < Bars.OpenPrices[i];
                if (!with) { _dNoTurn++; return; }

                // stop beyond the far side of the pullback, not beyond the bar
                ext = isLong ? Bars.LowPrices[extIdx] : Bars.HighPrices[extIdx];
                for (int k = extIdx; k <= i; k++)
                {
                    if (isLong && Bars.LowPrices[k] < ext) ext = Bars.LowPrices[k];
                    if (!isLong && Bars.HighPrices[k] > ext) ext = Bars.HighPrices[k];
                }
            }

            double entry = Bars.ClosePrices[i];
            _stopPx = isLong ? ext - DriftStopPad * atr : ext + DriftStopPad * atr;
            _targetR = DriftR;
            Raise(i, isLong, entry, 2, "DRIFT");
        }

        // ===================== ENGINE 3  SIGMA REVERSION ====================
        private void SigmaReversion(int i)
        {
            if (!SigmaOn) return;
            if (i < SigmaLen + 2) return;

            double sum = 0.0;
            for (int k = i - SigmaLen + 1; k <= i; k++) sum += Bars.ClosePrices[k];
            double mean = sum / SigmaLen;

            double sq = 0.0;
            for (int k = i - SigmaLen + 1; k <= i; k++)
            {
                double d = Bars.ClosePrices[k] - mean;
                sq += d * d;
            }
            double sd = Math.Sqrt(sq / SigmaLen);
            if (sd <= 0) return;

            double z = (Bars.ClosePrices[i] - mean) / sd;
            if (Math.Abs(z) < SigmaEntry) return;

            bool isLong = z < 0;
            double entry = Bars.ClosePrices[i];

            _stopPx = isLong ? mean - SigmaStop * sd : mean + SigmaStop * sd;
            _targetPx = isLong ? mean - SigmaTarget * sd : mean + SigmaTarget * sd;

            // b2. Beyond the stop sigma the old build produced a long whose stop
            // sat ABOVE its entry, which is not a trade. Refuse it.
            if (isLong ? _stopPx >= entry : _stopPx <= entry) { Clear(); return; }
            if (isLong ? _targetPx <= entry : _targetPx >= entry) { Clear(); return; }

            Raise(i, isLong, entry, 3, "SIG");
        }

        // ===================== ENGINE 4  RANGE BREAK ========================
        private void RangeBreakEngine(int i)
        {
            if (!UseRangeBreak) return;
            if (i < RangeWindow + 2) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            int start = i - RangeWindow;
            double hi = Bars.HighPrices[start], lo = Bars.LowPrices[start];
            for (int k = start; k < i; k++)
            {
                if (Bars.HighPrices[k] > hi) hi = Bars.HighPrices[k];
                if (Bars.LowPrices[k] < lo) lo = Bars.LowPrices[k];
            }
            if (hi - lo > RangeMaxAtr * atr) return;

            double buf = BreakBuffer * atr;
            double c = Bars.ClosePrices[i];

            bool up = c > hi + buf;
            bool dn = c < lo - buf;
            if (!up && !dn) return;

            double entry = c;
            _stopPx = up ? lo - buf : hi + buf;
            _targetR = RangeR;
            Raise(i, up, entry, 4, "RBK");
        }

        // ===================== SIGNAL =======================================
        private void Raise(int i, bool isLong, double entry, int src, string tag)
        {
            DateTime bd = Bars.OpenTimes[i].Date;
            if (bd != _capDay)
            {
                _capDay = bd;
                _dayUsed = 0;
                for (int k = 0; k < Engines; k++) _capUsed[k] = 0;
            }

            if (_dayUsed >= MaxPerDay) { _refusedCap[src]++; Clear(); return; }
            if (_capUsed[src] >= PerEngineDaily) { _refusedCap[src]++; Clear(); return; }
            if (MinGapSameEngine > 0 && i - _lastSrcBar[src] < MinGapSameEngine)
            { _refusedGap[src]++; Clear(); return; }

            if (double.IsNaN(_stopPx)) { Clear(); return; }

            double risk = Math.Abs(entry - _stopPx);
            if (risk <= 0) { Clear(); return; }

            double target;
            if (!double.IsNaN(_targetPx)) target = _targetPx;
            else if (!double.IsNaN(_targetR)) target = isLong ? entry + _targetR * risk : entry - _targetR * risk;
            else { Clear(); return; }

            if (isLong ? target <= entry : target >= entry) { Clear(); return; }

            // The reward floor. Drift rider builds its target as exactly
            // DriftR times risk and DriftR ships at the same 2.00 as this
            // floor, so every drift signal lands precisely on the line and one
            // rounding bit decided it. Measured over 200000 samples that
            // refused 49.7 percent of them. The tolerance is one part in a
            // billion, far below any reward difference that could matter.
            double rewardR = Math.Abs(target - entry) / risk;
            if (rewardR < MinRewardR - 1e-9) { _refusedR[src]++; Clear(); return; }

            _capUsed[src]++;
            _dayUsed++;
            _lastSrcBar[src] = i;
            _fired[src]++;
            _uid++;

            string id = "SYN_" + _uid.ToString() + "_" + tag;

            _open.Add(new Trade
            {
                Bar = i,
                Src = src,
                Id = id,
                IsLong = isLong,
                Entry = entry,
                Stop = _stopPx,
                Target = target,
                Risk = risk,
                Cost = DealCost(),
                MaxFav = 0.0,
                Closed = false,
                BeMoved = false
            });

            if (ShowSignals && ShouldDraw(i)) DrawSignal(i, id, isLong, entry, _stopPx, target, tag, risk);

            Clear();
        }

        private void Clear()
        {
            _stopPx = double.NaN;
            _targetR = double.NaN;
            _targetPx = double.NaN;
        }

        private double DealCost()
        {
            if (!BrokerSpread) return ManualSpread;
            double sp = Symbol.Spread;
            if (double.IsNaN(sp) || sp <= 0.0) return ManualSpread;
            return sp;
        }

        private void DrawSignal(int i, string id, bool isLong, double entry, double stop, double target, string tag, double risk)
        {
            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) atr = risk;

            Color col = isLong ? BuyColor : SellColor;
            DateTime t = Bars.OpenTimes[i];

            double y = isLong ? Bars.LowPrices[i] - 0.35 * atr : Bars.HighPrices[i] + 0.35 * atr;
            ChartText m = Chart.DrawText(id + "_A", isLong ? "\u2191" : "\u2193", t, y, col);
            m.FontSize = MarkerSize;
            m.IsInteractive = false;

            double ly = isLong ? Bars.LowPrices[i] - 0.95 * atr : Bars.HighPrices[i] + 0.95 * atr;
            ChartText lb = Chart.DrawText(id + "_T", tag + "  " +
                Math.Round(Math.Abs(target - entry) / risk, 1).ToString() + "R", t, ly, col);
            lb.FontSize = LabelSize;
            lb.IsInteractive = false;

            if (!ShowRisk) return;

            ChartText sl = Chart.DrawText(id + "_S", "\u00B7\u00B7\u00B7 SL", t,
                isLong ? stop - 0.2 * atr : stop + 0.2 * atr, StopColor);
            sl.FontSize = LabelSize;
            sl.IsInteractive = false;

            ChartText tp = Chart.DrawText(id + "_P", "\u00B7\u00B7\u00B7 TP", t,
                isLong ? target + 0.2 * atr : target - 0.2 * atr, TargetColor);
            tp.FontSize = LabelSize;
            tp.IsInteractive = false;
        }

        // ===================== TRACKING =====================================
        private void UpdateTrades(int i)
        {
            for (int k = _open.Count - 1; k >= 0; k--)
            {
                Trade t = _open[k];
                if (i <= t.Bar) continue;
                if (t.Closed) { _open.RemoveAt(k); continue; }

                double fav = t.IsLong ? Bars.HighPrices[i] - t.Entry : t.Entry - Bars.LowPrices[i];
                if (fav > t.MaxFav) t.MaxFav = fav;

                if (UseBreakEven && !t.BeMoved && t.Risk > 0 && t.MaxFav >= BreakEvenAtR * t.Risk)
                {
                    t.Stop = t.Entry;
                    t.BeMoved = true;
                }

                bool hitStop = t.IsLong ? Bars.LowPrices[i] <= t.Stop : Bars.HighPrices[i] >= t.Stop;
                bool hitTgt = t.IsLong ? Bars.HighPrices[i] >= t.Target : Bars.LowPrices[i] <= t.Target;

                // inside one bar the order is unknowable, so the stop is taken
                // first. That is the only honest reading.
                if (hitStop)
                {
                    Close(t, t.BeMoved ? -t.Cost / t.Risk : RAt(t, t.Stop), t.BeMoved ? 2 : 0, i);
                    _open.RemoveAt(k);
                }
                else if (hitTgt)
                {
                    Close(t, (Math.Abs(t.Target - t.Entry) - t.Cost) / t.Risk, 1, i);
                    _open.RemoveAt(k);
                }
                else if ((t.Src == 0 && i - t.Bar >= ClockGiveUp) ||
                         i - t.Bar >= DaysToBars(TradeWindowDays))
                {
                    double move = t.IsLong ? Bars.ClosePrices[i] - t.Entry : t.Entry - Bars.ClosePrices[i];
                    Close(t, (move - t.Cost) / t.Risk, 3, i);
                    _open.RemoveAt(k);
                }
            }
        }

        private double RAt(Trade t, double px)
        {
            if (t.Risk <= 0) return 0.0;
            double move = t.IsLong ? px - t.Entry : t.Entry - px;
            return (move - t.Cost) / t.Risk;
        }

        private void Close(Trade t, double r, int kind, int i)
        {
            t.Closed = true;
            _log.Add(new Res { Day = Bars.OpenTimes[t.Bar].Date, Src = t.Src, Outcome = kind, R = r });
            if (_log.Count > 40000) _log.RemoveRange(0, 2000);

            if (!ShowRisk || !ShouldDraw(i)) return;

            string g = kind == 1 ? "\u2714" : (kind == 2 ? "b=" : (kind == 3 ? "\u00B7" : "\u2718"));
            Color c = kind == 1 ? TargetColor : (kind == 0 ? StopColor : PanelMuted);
            double px = kind == 1 ? t.Target : (kind == 0 ? t.Stop : Bars.ClosePrices[i]);

            ChartText o = Chart.DrawText(t.Id + "_O", g, Bars.OpenTimes[i], px, c);
            o.FontSize = MarkerSize;
            o.IsInteractive = false;
        }

        // ===================== PANEL ========================================
        private void BuildPanel()
        {
            if (_built) return;
            _built = true;

            _cell = new TextBlock[Rows, Cols];
            _cellBg = new Grid[Rows, Cols];

            Grid outer = new Grid(Rows, Cols);
            outer.BackgroundColor = PanelBack;
            outer.Opacity = 0.95;

            bool left = PanelWhere == PanelSpot.TopLeft || PanelWhere == PanelSpot.BottomLeft;
            bool bottom = PanelWhere == PanelSpot.BottomRight || PanelWhere == PanelSpot.BottomLeft;
            outer.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            outer.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            outer.Margin = 4;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = "";
                    tb.FontSize = PanelFont;
                    tb.ForegroundColor = PanelText;
                    tb.Margin = 2;
                    tb.HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    Grid cell = new Grid(1, 1);
                    cell.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    cell.AddChild(tb, 0, 0);

                    _cell[r, c] = tb;
                    _cellBg[r, c] = cell;
                    outer.AddChild(cell, r, c);
                }
            }

            Chart.AddControl(outer);
        }

        private void Row(int r, string a, string b, string c, string d)
        {
            if (r < 0 || r >= Rows) return;
            _cell[r, 0].Text = a;
            _cell[r, 1].Text = b;
            _cell[r, 2].Text = c;
            _cell[r, 3].Text = d;
        }

        private void RowCol(int r, Color col)
        {
            if (r < 0 || r >= Rows) return;
            for (int c = 0; c < Cols; c++) _cell[r, c].ForegroundColor = col;
        }

        private void Section(int r, string a, string b, string c, string d)
        {
            Row(r, a, b, c, d);
            if (r < 0 || r >= Rows) return;
            for (int q = 0; q < Cols; q++)
            {
                _cellBg[r, q].BackgroundColor = SectionBack;
                _cell[r, q].ForegroundColor = SectionText;
            }
        }

        private bool EngineOn(int src)
        {
            if (src == 0) return HasSpikes() && UseSpikeClock;
            if (src == 1) return HasSpikes() && UseFade;
            if (src == 2) return DriftDir() != 0 && UseDrift;
            if (src == 3) return IsContinuous() && SigmaOn;
            return (_fam == SynthFamily.RangeBreak || _fam == SynthFamily.Step) && UseRangeBreak;
        }

        private void DrawPanel(int i)
        {
            if (!ShowPanel) return;
            BuildPanel();

            for (int r = 0; r < Rows; r++)
            {
                Row(r, "", "", "", "");
                for (int c = 0; c < Cols; c++)
                {
                    _cellBg[r, c].BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    _cell[r, c].ForegroundColor = PanelText;
                }
            }

            // b2. net is summed inside the outcome branches. The old build
            // added every R including expired trades while n excluded them, so
            // Total R and Average trade were on different populations.
            int tp = 0, sl = 0, be = 0, ex = 0;
            double net = 0.0, exR = 0.0;
            for (int k = 0; k < _log.Count; k++)
            {
                Res r = _log[k];
                if (r.Outcome == 1) { tp++; net += r.R; }
                else if (r.Outcome == 2) { be++; net += r.R; }
                else if (r.Outcome == 3) { ex++; exR += r.R; }
                else { sl++; net += r.R; }
            }
            int n = tp + sl + be;
            double days = WinMode == WindowMode.Days ? AnalyseDays : Math.Max(1.0, _histDays);

            int y = 0;
            Section(y, "SUPERQUANTX SYN", "b2", SymbolName, TfLabel()); y++;

            Row(y, "Family", FamilyIn == SynthFamily.Auto ? "auto" : "set", "", _fam.ToString()); y++;

            Row(y, _histShort ? "HISTORY SHORT" : "History", _histBars.ToString() + " bars",
                   Math.Round(_histDays, 0).ToString() + "d loaded",
                   AnalyseDays.ToString() + "d asked");
            RowCol(y, _histShort ? PanelNeg : PanelText); y++;

            if (Bars.Count > 1)
            {
                DateTime ws = WindowStart(), we = WindowEnd();
                if (ws < Bars.OpenTimes[0]) ws = Bars.OpenTimes[0];
                if (we > Bars.OpenTimes[Bars.Count - 1]) we = Bars.OpenTimes[Bars.Count - 1];
                Row(y, "Window", ws.ToString("dd MMM yy"), we.ToString("dd MMM yy"),
                       Math.Round((we - ws).TotalDays, 0).ToString() + "d"); y++;
            }

            if (HasSpikes())
            {
                Section(y, "SPIKES", "", "", _spikes.Count.ToString() + " seen"); y++;

                if (SpikeOnM1)
                {
                    Row(y, _m1Short ? "M1 HISTORY SHORT" : "m1 history",
                           _m1 == null ? "0" : _m1.Count.ToString() + " bars",
                           Math.Round(_m1Days, 0).ToString() + "d covered",
                           AnalyseDays.ToString() + "d asked");
                    RowCol(y, _m1Short ? PanelNeg : PanelText); y++;
                }

                Row(y, "Mean interval", "", "", _meanInterval <= 0 ? "none" :
                       Math.Round(_meanInterval, 1).ToString() + " min"); y++;

                if (_tfTooHigh)
                {
                    Row(y, TfGuardOn ? "TF TOO HIGH, SPIKE ENGINES OFF" : "TF TOO HIGH, not blocked",
                           "", "", "use m" + SuggestedTfMinutes().ToString());
                    RowCol(y, PanelNeg); y++;
                }

                Row(y, "Mean spike size", "", "", _meanSpike <= 0 ? "none" :
                       Math.Round(_meanSpike, Symbol.Digits).ToString()); y++;

                int since = BarsSinceSpike(_lastClosed);
                double mins = MinutesSinceSpike(_lastClosed);
                double pct = _meanInterval > 0 && mins >= 0 ? 100.0 * mins / _meanInterval : 0.0;
                Row(y, "Since last", since < 0 ? "none" : Math.Round(mins, 0).ToString() + " min", "",
                       _meanInterval > 0 && mins >= 0 ? Math.Round(pct, 0).ToString() + "% of mean" : "");
                RowCol(y, SpikeOverdue(_lastClosed) ? PanelPos : PanelText); y++;

                Row(y, "Clock", "", "", SpikeOverdue(_lastClosed) ? "ARMED" : "waiting");
                RowCol(y, SpikeOverdue(_lastClosed) ? PanelPos : PanelMuted); y++;
            }

            if (IsContinuous() && !SigmaOn)
            {
                Row(y, "NO ENGINE ON THIS FAMILY", "", "", "random walk");
                RowCol(y, PanelNeg); y++;
                Row(y, "Volatility is driftless by design", "", "", "reversion pays 0");
                RowCol(y, PanelMuted); y++;
            }

            Section(y, "RESULTS", "", "", n.ToString() + " closed"); y++;
            Row(y, "Signals per day", "", "", days > 1 ? Math.Round(n / days, 2).ToString() : ""); y++;
            Row(y, "Won / Lost / BE", tp.ToString(), sl.ToString(), be.ToString()); y++;
            Row(y, "Win rate", "", Rate(tp, sl) + " %", ""); y++;
            Row(y, "Average trade", "", "", Sign(n == 0 ? 0.0 : net / n) + " R"); y++;
            Row(y, "Total", "", "", Sign(net) + " R");
            RowCol(y, net >= 0 ? PanelPos : PanelNeg); y++;
            Row(y, "Expired (excluded)", ex.ToString(), Sign(exR) + " R", _open.Count.ToString() + " open");
            RowCol(y, PanelMuted); y++;

            // ---- every enabled engine takes a row, even at zero -------------
            Section(y, "ENGINES", "n", "win%", "net R"); y++;
            for (int src = 0; src < Engines; src++)
            {
                if (!EngineOn(src)) continue;

                int a = 0, b = 0, c = 0;
                double r2 = 0.0;
                for (int k = 0; k < _log.Count; k++)
                {
                    if (_log[k].Src != src) continue;
                    if (_log[k].Outcome == 1) { a++; r2 += _log[k].R; }
                    else if (_log[k].Outcome == 2) { c++; r2 += _log[k].R; }
                    else if (_log[k].Outcome != 3) { b++; r2 += _log[k].R; }
                }
                int t2 = a + b + c;

                Row(y, EngineName(src), t2.ToString(),
                       t2 == 0 ? "" : Rate(a, b),
                       t2 == 0 ? "no trades" : Sign(r2));
                RowCol(y, t2 == 0 ? PanelNeg : (r2 >= 0 ? PanelPos : PanelNeg));
                y++;
            }

            if (!ShowGates) return;

            Section(y, "WHY REFUSED", "reward", "cap", "spacing"); y++;
            for (int src = 0; src < Engines; src++)
            {
                if (!EngineOn(src)) continue;
                Row(y, EngineName(src), _refusedR[src].ToString(),
                       _refusedCap[src].ToString(), _refusedGap[src].ToString());
                y++;
            }

            Section(y, "ENGINE GATES", "", "", ""); y++;
            if (DriftDir() != 0 && UseDrift)
            {
                Row(y, "Drift  mode", "", "", DriftMode.ToString()); y++;
                Row(y, "Drift  no pullback / no turn", _dNoPull.ToString(), _dNoTurn.ToString(),
                       "fired " + _fired[2].ToString()); y++;
                Row(y, "Drift  blocked, spike overdue", "", "", _dOverdue.ToString()); y++;
            }
            if (HasSpikes() && UseFade)
            {
                Row(y, "Fade  no spike / weak R", _fNoSpike.ToString(), _fWeakR.ToString(),
                       "fired " + _fired[1].ToString()); y++;
            }
            if (HasSpikes() && UseSpikeClock)
            {
                Row(y, "Clock  not due / sample short", _cNotDue.ToString(), _gSample.ToString(),
                       "fired " + _fired[0].ToString()); y++;
            }
            if (HasSpikes())
            {
                Row(y, "Blocked by timeframe guard", "", "", _gTf.ToString());
                RowCol(y, _gTf > 0 ? PanelNeg : PanelMuted); y++;
            }
        }

        // ===================== DAY PANEL ====================================
        private void BuildDayPanel()
        {
            if (_dayBuilt) return;
            _dayBuilt = true;

            _dcell = new TextBlock[DayRows, DayCols];
            _dcellBg = new Grid[DayRows, DayCols];

            Grid outer = new Grid(DayRows, DayCols);
            outer.BackgroundColor = PanelBack;
            outer.Opacity = 0.95;

            bool left = DayWhere == PanelSpot.TopLeft || DayWhere == PanelSpot.BottomLeft;
            bool bottom = DayWhere == PanelSpot.BottomRight || DayWhere == PanelSpot.BottomLeft;
            outer.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            outer.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            outer.Margin = 4;

            for (int r = 0; r < DayRows; r++)
            {
                for (int c = 0; c < DayCols; c++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = "";
                    tb.FontSize = PanelFont;
                    tb.ForegroundColor = PanelText;
                    tb.Margin = 2;
                    tb.HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    Grid cell = new Grid(1, 1);
                    cell.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    cell.AddChild(tb, 0, 0);

                    _dcell[r, c] = tb;
                    _dcellBg[r, c] = cell;
                    outer.AddChild(cell, r, c);
                }
            }

            Chart.AddControl(outer);
        }

        private void DRow(int r, string a, string b, string c, string d, string e)
        {
            if (r < 0 || r >= DayRows) return;
            _dcell[r, 0].Text = a;
            _dcell[r, 1].Text = b;
            _dcell[r, 2].Text = c;
            _dcell[r, 3].Text = d;
            _dcell[r, 4].Text = e;
        }

        // dow 0 to 6 with Monday first, -1 for everything
        private void DayLine(int r, string label, int dow)
        {
            int a = 0, b = 0, c = 0;
            double net = 0.0;

            for (int k = 0; k < _log.Count; k++)
            {
                Res x = _log[k];
                int wd = ((int)x.Day.DayOfWeek + 6) % 7;
                if (dow >= 0 && wd != dow) continue;

                if (x.Outcome == 1) { a++; net += x.R; }
                else if (x.Outcome == 2) { c++; net += x.R; }
                else if (x.Outcome != 3) { b++; net += x.R; }
            }

            int n = a + b + c;
            DRow(r, label, n.ToString(), a.ToString() + "/" + b.ToString() + "/" + c.ToString(),
                 n == 0 ? "" : Rate(a, b) + " %", n == 0 ? "" : Sign(net) + " R");

            if (r < 0 || r >= DayRows) return;
            Color col = n == 0 ? PanelMuted : (net >= 0 ? PanelPos : PanelNeg);
            for (int q = 0; q < DayCols; q++) _dcell[r, q].ForegroundColor = col;
        }

        private void DrawDayPanel()
        {
            if (!ShowDayTable) return;
            BuildDayPanel();

            for (int r = 0; r < DayRows; r++)
            {
                DRow(r, "", "", "", "", "");
                for (int c = 0; c < DayCols; c++)
                {
                    _dcellBg[r, c].BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    _dcell[r, c].ForegroundColor = PanelText;
                }
            }

            string[] dn = new string[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

            int y = 0;
            DRow(y, "BY DAY", "n", "W/L/BE", "win%", "net R");
            for (int q = 0; q < DayCols; q++)
            {
                _dcellBg[y, q].BackgroundColor = SectionBack;
                _dcell[y, q].ForegroundColor = SectionText;
            }
            y++;

            // these run seven days a week, so every day gets a line
            for (int d = 0; d < 7; d++) { DayLine(y, dn[d], d); y++; }

            DayLine(y, "ALL", -1);
            if (y >= 0 && y < DayRows)
                for (int q = 0; q < DayCols; q++) _dcellBg[y, q].BackgroundColor = SectionBack;
        }

        // ===================== HELPERS ======================================
        private string EngineName(int src)
        {
            if (src == 0) return "Spike clock";
            if (src == 1) return "Spike fade";
            if (src == 2) return "Drift rider";
            if (src == 3) return "Sigma reversion";
            return "Range break";
        }

        private string Rate(int w, int l)
        {
            int d = w + l;
            if (d == 0) return "";
            return Math.Round(100.0 * w / d, 1).ToString();
        }

        private string Sign(double v)
        {
            double r = Math.Round(v, 2);
            return (r > 0 ? "+" : "") + r.ToString();
        }

        private string TfLabel()
        {
            double m = BarDuration().TotalMinutes;
            if (m < 60.0) return "m" + Math.Round(m, 0).ToString();
            if (m < 1440.0) return "h" + Math.Round(m / 60.0, 0).ToString();
            return "D" + Math.Round(m / 1440.0, 0).ToString();
        }

        private int DaysToBars(int days)
        {
            double m = BarDuration().TotalMinutes;
            if (m <= 0.0) m = 60.0;
            int v = (int)Math.Round(days * 1440.0 / m);
            if (v < 10) v = 10;
            if (v > 200000) v = 200000;
            return v;
        }

        // Median of recent gaps, so a data break cannot distort it.
        private TimeSpan BarDuration()
        {
            if (_barDurTicks > 0) return TimeSpan.FromTicks(_barDurTicks);

            int end = Bars.Count - 1;
            int start = Math.Max(1, end - 60);
            List<long> gaps = new List<long>();
            for (int k = start; k <= end; k++)
            {
                long g = (Bars.OpenTimes[k] - Bars.OpenTimes[k - 1]).Ticks;
                if (g > 0) gaps.Add(g);
            }
            if (gaps.Count < 5) return TimeSpan.FromMinutes(1);

            gaps.Sort();
            long med = gaps[gaps.Count / 2];
            if (gaps.Count >= 20) _barDurTicks = med;
            return TimeSpan.FromTicks(med);
        }

        private bool ShouldDraw(int i)
        {
            return i >= 0 && i < Bars.Count;
        }
    }
}
