// =============================================================================
// SuperQuantX_Synthetics    cTrader 5.9.10 / cAlgo.API
//
// BUILD 1. One change from the previous file: the reward floor in Raise() is
// now compared with a tolerance. Drift rider's target is built as exactly
// 2.00 times risk and the floor is exactly 2.00, so every drift signal sat on
// the boundary and one floating point rounding decided it. That silently
// refused 49.7 percent of them. Nothing else changed. No trade that already
// passed has a different entry, stop or target. The panel header reads b1.
//
// What to read after pasting: Refused reward should fall sharply and Drift
// rider's n should roughly double. Win rate should NOT move much, because the
// signals that were being thrown away were a random half, not a bad half.
//
// Built for Deriv synthetic indices. Standalone: it shares nothing with
// SuperQuantX_Alpha, so neither file can affect the other.
//
// WHY A SEPARATE FILE
//   The CFD indicator lost 83 R on Boom 900 and the reason is structural.
//   Every engine in it assumes a large candle means continuation. On Boom the
//   large candle IS the event and price reverts to drift immediately after.
//   Elephant read 14.3% there, which is that assumption inverted. Compression
//   never happens either, so Pre-expansion fired zero times.
//
// WHAT THESE INSTRUMENTS ACTUALLY DO
//   Boom      drifts down in small steps, sudden large UP spikes
//   Crash     drifts up in small steps, sudden large DOWN spikes
//   Jump      jumps both ways at a roughly fixed average interval
//   Volatility  continuous, constant volatility, no spikes, no gaps, no news
//   Step      fixed step size, direction is a coin flip
//   Range Break ranges, then breaks, by construction
//
//   The exploitable fact none of these share with a real market: the spike
//   interval is a property of the instrument. Bars since the last spike is a
//   genuine probability signal. That is the core of this file.
//
// THE INTERVAL IS MEASURED, NOT ASSUMED
//   Boom 900 does not spike every 900 bars on m15. The engine measures the
//   mean interval and the mean spike size from the chart it is on, and arms
//   against those. The name is used only to decide direction.
//
// TARGETS ARE PER ENGINE
//   A spike catch is a tiny stop against a very large target. A fade is a
//   modest target at a high win rate. Forcing one reward multiple on both is
//   what produced the 18.9% win rate on Boom 900.
//
// CARRIED OVER FROM THE CFD WORK
//   Bars evaluated once, after close. Nothing reads a later index, so there is
//   no lookahead. The history loader fills the requested window instead of
//   stopping at a bar count. Dealing cost is read from the broker. Entry is
//   the close of the signal bar, the first price actually tradable.
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

        // Nothing is taken below this. At 2R breakeven is 33.3%, so a 40% rule
        // leaves a real margin instead of scraping the line.
        [Parameter("Min Reward R", Group = "General", DefaultValue = 2.00, MinValue = 0.50, MaxValue = 20.00)]
        public double MinRewardR { get; set; }

        [Parameter("Min Bars Between Same Engine", Group = "General", DefaultValue = 2, MinValue = 0, MaxValue = 200)]
        public int MinGapSameEngine { get; set; }

        [Parameter("Manual Spread (price)", Group = "General", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10000.0)]
        public double ManualSpread { get; set; }

        [Parameter("Use Broker Spread", Group = "General", DefaultValue = true)]
        public bool BrokerSpread { get; set; }

        // ===================== SPIKE DETECTION ==============================
        // A spike is a bar whose range dwarfs the recent typical range. The
        // comparison is against the MEDIAN, because a mean is dragged upward
        // by the very spikes being measured.
        // Boom 600 spikes about every 600 ticks, roughly every ten minutes.
        // On h1 that is six spikes inside every bar, so no bar stands out and
        // only ONE spike was found in 23,705 bars. Spikes are detected on m1
        // and mapped onto the chart, so the count is right on any timeframe.
        [Parameter("Detect Spikes On m1", Group = "Spike Detection", DefaultValue = true)]
        public bool SpikeOnM1 { get; set; }

        [Parameter("Spike Range Multiple", Group = "Spike Detection", DefaultValue = 4.0, MinValue = 1.5, MaxValue = 50.0)]
        public double SpikeMult { get; set; }

        [Parameter("Typical Range Lookback", Group = "Spike Detection", DefaultValue = 200, MinValue = 20, MaxValue = 3000)]
        public int TypicalLookback { get; set; }

        [Parameter("Spike Needs Directional Body", Group = "Spike Detection", DefaultValue = true)]
        public bool SpikeNeedsBody { get; set; }

        [Parameter("Spike Min Body %", Group = "Spike Detection", DefaultValue = 40, MinValue = 0, MaxValue = 99)]
        public int SpikeMinBodyPct { get; set; }

        // ===================== SPIKE CLOCK ==================================
        // Bars since the last spike against the measured mean interval. When a
        // spike is overdue the odds of one arriving are at their best.
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
        // The bar after a spike, back toward the drift. The spike is the whole
        // event on these instruments and it does not extend.
        [Parameter("Spike Fade Signals", Group = "Spike Fade", DefaultValue = true)]
        public bool UseFade { get; set; }

        [Parameter("Fade Stop Beyond Spike (ATR)", Group = "Spike Fade", DefaultValue = 0.40, MinValue = 0.00, MaxValue = 5.00)]
        public double FadeStopPad { get; set; }

        [Parameter("Fade Target As Share Of Spike", Group = "Spike Fade", DefaultValue = 0.50, MinValue = 0.05, MaxValue = 3.00)]
        public double FadeTargetShare { get; set; }

        [Parameter("Fade Min Reward", Group = "Spike Fade", DefaultValue = 2.00, MinValue = 0.20, MaxValue = 10.00)]
        public double FadeMinR { get; set; }

        // ===================== DRIFT RIDER ==================================
        // Boom drifts down and Crash drifts up, relentlessly and in small
        // steps. This trades that drift after a counter-drift pullback, with
        // the stop beyond the pullback so a spike does not take it.
        [Parameter("Drift Rider Signals", Group = "Drift Rider", DefaultValue = true)]
        public bool UseDrift { get; set; }

        [Parameter("Drift Pullback Bars", Group = "Drift Rider", DefaultValue = 3, MinValue = 1, MaxValue = 30)]
        public int DriftPullBars { get; set; }

        [Parameter("Drift Stop Pad (ATR)", Group = "Drift Rider", DefaultValue = 0.60, MinValue = 0.05, MaxValue = 10.00)]
        public double DriftStopPad { get; set; }

        [Parameter("Drift Reward", Group = "Drift Rider", DefaultValue = 2.00, MinValue = 0.20, MaxValue = 10.00)]
        public double DriftR { get; set; }

        [Parameter("Block Drift While Spike Overdue", Group = "Drift Rider", DefaultValue = true)]
        public bool DriftAvoidOverdue { get; set; }

        // ===================== SIGMA REVERSION ==============================
        // Volatility and Step indices have no spikes and no trend, only
        // constant volatility. Distance from the mean is the only edge there.
        [Parameter("Sigma Reversion Signals", Group = "Sigma Reversion", DefaultValue = true)]
        public bool UseSigma { get; set; }

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

        // spike measurement
        private readonly List<int> _spikeBar = new List<int>();
        private readonly List<double> _spikeSize = new List<double>();
        private int _lastSpikeBar = -1;
        private double _meanInterval;        // in minutes
        private double _meanSpike;
        private Bars _m1;
        private int _m1Seen;
        private DateTime _lastSpikeTime = DateTime.MinValue;
        private bool _tfTooHigh;

        // per engine bookkeeping
        private readonly int[] _capUsed = new int[8];
        private readonly int[] _lastSrcBar = new int[8];
        private DateTime _capDay = DateTime.MinValue;
        private int _dayUsed;
        private int _refusedR, _refusedCap;

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

        private const int Rows = 34;
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

            for (int k = 0; k < _lastSrcBar.Length; k++) _lastSrcBar[k] = -1000000;

            _fam = FamilyIn == SynthFamily.Auto ? DetectFamily() : FamilyIn;

            if (SpikeOnM1 && HasSpikes())
            {
                _m1 = MarketData.GetBars(TimeFrame.Minute);
                int g2 = 0;
                while (g2++ < 200 && _m1.Count < 60000)
                {
                    if (_m1.LoadMoreHistory() == 0) break;
                }
            }
        }

        // The name is used for direction only. Everything numeric is measured.
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

        // Boom spikes up and drifts down. Crash is the mirror. Jump goes both
        // ways. The others have no spike at all.
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

            if (WinMode == WindowMode.Days)
            {
                if (Bars.OpenTimes[i] < WindowStart() || Bars.OpenTimes[i] > WindowEnd()) return;
            }
            else if (i < Bars.Count - PreloadBars) return;

            UpdateTrades(i);

            if (HasSpikes())
            {
                SpikeFade(i);
                SpikeClock(i);
                DriftRider(i);
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

            // Boom only spikes up, Crash only down. Jump takes either.
            int want = SpikeDir();
            if (want != 0 && dir != want) return;

            _spikeBar.Add(i);
            _spikeSize.Add(range);
            _lastSpikeBar = i;

            while (_spikeBar.Count > 400) { _spikeBar.RemoveAt(0); _spikeSize.RemoveAt(0); }

            int c = _spikeBar.Count;
            if (c >= 2)
            {
                double sum = 0.0;
                for (int k = 1; k < c; k++) sum += _spikeBar[k] - _spikeBar[k - 1];
                _meanInterval = sum / (c - 1);
            }

            double ss = 0.0;
            for (int k = 0; k < c; k++) ss += _spikeSize[k];
            _meanSpike = c == 0 ? 0.0 : ss / c;

            if (MarkSpikes && ShouldDraw(i))
            {
                ChartText t = Chart.DrawText("SYNSPK_" + Bars.OpenTimes[i].Ticks.ToString(), "\u25CF",
                    Bars.OpenTimes[i], dir > 0 ? Bars.HighPrices[i] : Bars.LowPrices[i], SpikeColor);
                t.FontSize = MarkerSize;
                t.IsInteractive = false;
            }
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

                RecordSpike(i, range, _m1.OpenTimes[k]);
            }
        }

        private void RecordSpike(int chartBar, double size, DateTime when)
        {
            if (_lastSpikeTime != DateTime.MinValue)
            {
                double mins = (when - _lastSpikeTime).TotalMinutes;
                if (mins > 0)
                {
                    _spikeSize.Add(size);
                    _spikeBar.Add((int)Math.Round(mins));      // interval in minutes
                }
            }
            else
            {
                _spikeSize.Add(size);
                _spikeBar.Add(0);
            }

            _lastSpikeTime = when;
            _lastSpikeBar = chartBar;

            while (_spikeBar.Count > 500) { _spikeBar.RemoveAt(0); _spikeSize.RemoveAt(0); }

            double sum = 0.0; int cnt = 0;
            for (int q = 1; q < _spikeBar.Count; q++) { sum += _spikeBar[q]; cnt++; }
            _meanInterval = cnt == 0 ? 0.0 : sum / cnt;

            double ss = 0.0;
            for (int q = 0; q < _spikeSize.Count; q++) ss += _spikeSize[q];
            _meanSpike = _spikeSize.Count == 0 ? 0.0 : ss / _spikeSize.Count;

            // the clock is only usable when a chart bar is smaller than the gap
            // between spikes. On h1 with a ten minute interval it is not.
            _tfTooHigh = _meanInterval > 0 && BarDuration().TotalMinutes > _meanInterval / 2.0;
        }

        private int BarsSinceSpike(int i)
        {
            return _lastSpikeBar < 0 ? -1 : i - _lastSpikeBar;
        }

        private bool SpikeOverdue(int i)
        {
            if (_spikeBar.Count < MinSpikeSample || _meanInterval <= 0) return false;
            if (_tfTooHigh) return false;      // a chart bar holds several spikes

            int sinceBars = BarsSinceSpike(i);
            if (sinceBars < 0) return false;

            double sinceMins = SpikeOnM1 && _m1 != null
                ? sinceBars * BarDuration().TotalMinutes
                : sinceBars;
            double meanUnits = SpikeOnM1 && _m1 != null ? _meanInterval : _meanInterval;

            return sinceMins >= ArmShare * meanUnits;
        }

        // ===================== ENGINE 0  SPIKE CLOCK ========================
        // Overdue means the odds of a spike are at their best. Tiny stop, the
        // target is a share of the measured mean spike, so the reward multiple
        // is whatever that ratio happens to be rather than a number I picked.
        private void SpikeClock(int i)
        {
            if (!UseSpikeClock) return;
            if (!SpikeOverdue(i)) return;
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
        // The bar after a spike, back toward the drift. On these instruments
        // the spike is the entire event and does not extend.
        private void SpikeFade(int i)
        {
            if (!UseFade) return;
            if (_lastSpikeBar < 0 || i != _lastSpikeBar + 1) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            int sBar = _lastSpikeBar;
            double sRange = Bars.HighPrices[sBar] - Bars.LowPrices[sBar];
            if (sRange <= 0) return;

            int sDir = Bars.ClosePrices[sBar] >= Bars.OpenPrices[sBar] ? 1 : -1;
            bool isLong = sDir < 0;                    // fade it

            double entry = Bars.ClosePrices[i];
            _stopPx = isLong ? Bars.LowPrices[sBar] - FadeStopPad * atr
                             : Bars.HighPrices[sBar] + FadeStopPad * atr;

            double risk = Math.Abs(entry - _stopPx);
            if (risk <= 0) return;

            double move = FadeTargetShare * sRange;
            if (move / risk < FadeMinR) return;        // not worth the stop it needs

            _targetPx = isLong ? entry + move : entry - move;
            Raise(i, isLong, entry, 1, "FADE");
        }

        // ===================== ENGINE 2  DRIFT RIDER ========================
        // Boom grinds down, Crash grinds up. Enter with the drift after a short
        // counter move, stop beyond that counter move so the grind itself does
        // not stop you out. Blocked while a spike is overdue, because that is
        // exactly when the drift is about to be interrupted.
        private void DriftRider(int i)
        {
            if (!UseDrift) return;

            int dir = DriftDir();
            if (dir == 0) return;
            if (DriftAvoidOverdue && SpikeOverdue(i)) return;
            if (i < DriftPullBars + 2) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            bool isLong = dir > 0;

            // a counter-drift stretch, then a bar closing back with the drift
            for (int k = i - DriftPullBars; k < i; k++)
            {
                bool up = Bars.ClosePrices[k] > Bars.OpenPrices[k];
                if (isLong ? up : !up) return;         // that bar already went with the drift
            }
            bool nowWith = isLong ? Bars.ClosePrices[i] > Bars.OpenPrices[i]
                                  : Bars.ClosePrices[i] < Bars.OpenPrices[i];
            if (!nowWith) return;

            double ext = isLong ? Bars.LowPrices[i] : Bars.HighPrices[i];
            for (int k = i - DriftPullBars; k <= i; k++)
            {
                if (isLong && Bars.LowPrices[k] < ext) ext = Bars.LowPrices[k];
                if (!isLong && Bars.HighPrices[k] > ext) ext = Bars.HighPrices[k];
            }

            double entry = Bars.ClosePrices[i];
            _stopPx = isLong ? ext - DriftStopPad * atr : ext + DriftStopPad * atr;
            _targetR = DriftR;
            Raise(i, isLong, entry, 2, "DRIFT");
        }

        // ===================== ENGINE 3  SIGMA REVERSION ====================
        // Volatility and Step have no spikes and no trend. Distance from the
        // mean is the only thing there is to trade.
        private void SigmaReversion(int i)
        {
            if (!UseSigma) return;
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

            // the target has to be the right side of entry to be a trade
            if (isLong ? _targetPx <= entry : _targetPx >= entry) return;

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
                for (int k = 0; k < _capUsed.Length; k++) _capUsed[k] = 0;
            }

            if (_dayUsed >= MaxPerDay) { _refusedCap++; Clear(); return; }
            if (_capUsed[src] >= PerEngineDaily) { _refusedCap++; Clear(); return; }
            if (MinGapSameEngine > 0 && i - _lastSrcBar[src] < MinGapSameEngine) { Clear(); return; }

            if (double.IsNaN(_stopPx)) { Clear(); return; }

            double risk = Math.Abs(entry - _stopPx);
            if (risk <= 0) { Clear(); return; }

            double target;
            if (!double.IsNaN(_targetPx)) target = _targetPx;
            else if (!double.IsNaN(_targetR)) target = isLong ? entry + _targetR * risk : entry - _targetR * risk;
            else { Clear(); return; }

            if (isLong ? target <= entry : target >= entry) { Clear(); return; }

            // The reward floor, applied to every engine without exception.
            //
            // BUILD 1. Drift rider builds its target as exactly DriftR times risk
            // and DriftR ships at 2.00, the same value as this floor, so every
            // drift signal lands precisely on this line rather than clear of it.
            // target is entry + 2.00 * risk rounded once to a double. Subtracting
            // entry back out returns 2.00 * risk give or take half an ulp of
            // entry, and the sign of that last bit is a coin flip. A bare less
            // than refused whichever half rounded down: 49.7 percent of them,
            // measured over 200000 samples at Boom 900, Boom 600 and Crash 500
            // price and risk scales.
            //
            // The tolerance is one part in a billion of a reward multiple, far
            // below any reward difference that could matter, so nothing that
            // genuinely falls short of the floor survives it. No trade geometry
            // changes: every signal that already passed still has the same
            // entry, the same stop and the same target.
            double rewardR = Math.Abs(target - entry) / risk;
            if (rewardR < MinRewardR - 1e-9) { _refusedR++; Clear(); return; }

            _capUsed[src]++;
            _dayUsed++;
            _lastSrcBar[src] = i;
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
            for (int q = 0; q < Cols; q++)
            {
                _cellBg[r, q].BackgroundColor = SectionBack;
                _cell[r, q].ForegroundColor = SectionText;
            }
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

            int tp = 0, sl = 0, be = 0, ex = 0;
            double net = 0.0;
            for (int k = 0; k < _log.Count; k++)
            {
                Res r = _log[k];
                net += r.R;
                if (r.Outcome == 1) tp++;
                else if (r.Outcome == 2) be++;
                else if (r.Outcome == 3) ex++;
                else sl++;
            }
            int n = tp + sl + be;
            double days = WinMode == WindowMode.Days ? AnalyseDays : Math.Max(1.0, _histDays);

            int y = 0;
            // The build tag is the paste check. If this does not read b1 the
            // file on the chart is still the previous one.
            Section(y, "SUPERQUANTX SYN", "b1", SymbolName, TfLabel()); y++;

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
                Section(y, "SPIKES", "", "", _spikeBar.Count.ToString() + " seen"); y++;
                Row(y, "Mean interval", "", "", _meanInterval <= 0 ? "" :
                       Math.Round(_meanInterval, 1).ToString() + (SpikeOnM1 ? " min" : " bars")); y++;
                if (_tfTooHigh)
                {
                    Row(y, "TIMEFRAME TOO HIGH", "", "",
                           "use m" + Math.Max(1, (int)Math.Round(_meanInterval / 4.0)).ToString());
                    RowCol(y, PanelNeg); y++;
                }
                Row(y, "Mean size", "", "", _meanSpike <= 0 ? "" :
                       Math.Round(_meanSpike, Symbol.Digits).ToString()); y++;

                int since = BarsSinceSpike(_lastClosed);
                double pct = _meanInterval > 0 && since >= 0 ? 100.0 * since / _meanInterval : 0.0;
                Row(y, "Since last", since < 0 ? "none" : since.ToString() + " bars", "",
                       _meanInterval > 0 ? Math.Round(pct, 0).ToString() + "% of mean" : "");
                RowCol(y, SpikeOverdue(_lastClosed) ? PanelPos : PanelText); y++;

                Row(y, "Clock", "", "", SpikeOverdue(_lastClosed) ? "ARMED" : "waiting");
                RowCol(y, SpikeOverdue(_lastClosed) ? PanelPos : PanelMuted); y++;
            }

            Section(y, "RESULTS", "", "", n.ToString() + " closed"); y++;
            Row(y, "Signals per day", "", "", days > 1 ? Math.Round(n / days, 2).ToString() : ""); y++;
            Row(y, "Won / Lost / BE", tp.ToString(), sl.ToString(), be.ToString()); y++;
            Row(y, "Win rate", "", Rate(tp, sl) + " %", ""); y++;
            Row(y, "Average trade", "", "", Sign(n == 0 ? 0.0 : net / n) + " R"); y++;
            Row(y, "Total", "", "", Sign(net) + " R");
            RowCol(y, net >= 0 ? PanelPos : PanelNeg); y++;
            Row(y, "Expired / open", ex.ToString(), "", _open.Count.ToString()); y++;
            Row(y, "Refused", "reward " + _refusedR.ToString(), "cap " + _refusedCap.ToString(),
                   "max " + MaxPerDay.ToString() + "/day"); y++;

            Section(y, "ENGINES", "n", "win%", "net R"); y++;
            for (int src = 0; src < 5; src++)
            {
                int a = 0, b = 0, c = 0;
                double r2 = 0.0;
                for (int k = 0; k < _log.Count; k++)
                {
                    if (_log[k].Src != src) continue;
                    r2 += _log[k].R;
                    if (_log[k].Outcome == 1) a++;
                    else if (_log[k].Outcome == 2) c++;
                    else if (_log[k].Outcome != 3) b++;
                }
                int t2 = a + b + c;
                if (t2 == 0) continue;

                Row(y, EngineName(src), t2.ToString(), Rate(a, b), Sign(r2));
                RowCol(y, r2 >= 0 ? PanelPos : PanelNeg);
                y++;
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

                net += x.R;
                if (x.Outcome == 1) a++;
                else if (x.Outcome == 2) c++;
                else if (x.Outcome != 3) b++;
            }

            int n = a + b + c;
            DRow(r, label, n.ToString(), a.ToString() + "/" + b.ToString() + "/" + c.ToString(),
                 n == 0 ? "" : Rate(a, b) + " %", n == 0 ? "" : Sign(net) + " R");

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
