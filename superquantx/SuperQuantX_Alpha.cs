// =============================================================================
// SuperQuantX_Alpha   (rollback build)
// cTrader 5.9.10 / cAlgo.API
//
// FOUR SIGNAL ENGINES
//   Pre-expansion  compression detected, levels armed, fires on the break
//   Elephant       the momentum candle itself, at its close
//   Breakout       continuation through the elephant candle's extreme
//   Pullback       re-entry into the elephant candle's 38 to 62 percent zone
//
// RISK
//   Stop sits beyond the nearest meaningful structure: swing, compression box
//   or order block, with a buffer. Minimum reward to risk 3.0. Break even
//   moves the stop to entry once price reaches 1R.
//
// NON REPAINT
//   Every bar is evaluated once, after it closes. Arm levels are built only
//   from closed bars. A trigger latches and never un-fires. A printed marker
//   never moves.
// =============================================================================

using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo
{
    public enum EntryPriceMode { ConfirmedPrice, TriggerLevel, BarOpen, BarMid }

    public enum RecolourMode { BarColor, Overlay }

    public enum ElephantColourMode { DarkerShadeOfChart, CustomColours }

    public enum MarkerShape { Arrow, Triangle, Circle, Dot, Square, Diamond, Star }

    public enum MarkerRender { Resizable, NativeIcon }

    public enum LabelStyle { Full, Short, Compact, None }

    public enum HistoryDisplay { AllHistory, LastNDays, TodayOnly }

    public enum ZoneSource { LastOpposingCandle, ImpulseCandle }

    public enum ZoneTrigger { RejectionClose, FirstTouch }

    public enum StopSource { NearestStructure, SwingPoint, CompressionBox, OrderBlock, Furthest }

    public enum DashboardDetail { Compact, Full }

    public enum PanelLayout { Essential, Standard, Full }

    public enum PanelCorner { TopRight, TopLeft, BottomRight, BottomLeft }

    public enum ConsoMode { MarkGrey, Block, Allow }

    public enum RangeDetect { InternalStructure, RangeAndEfficiency }

    public enum TablePeriod { Today, Week }

    public enum VolTest { Percentile, SurgeMultiple }

    public enum ZoneRefine { WholeCandle, Imbalance, Body }

    public enum ZoneEntryAt { CloseOfBar, ZoneEdge }

    public enum TpbEntryAt { BarClose, TriggerLevel }

    public enum FillModel { CloseOfSignalBar, LevelPrice }

    public enum CostSource { FromBroker, Manual }

    public enum EleStopAt { Midpoint, Quarter, Third, FarExtreme }

    public enum PbStopAt { StructureDefault, ZoneEdge, CandleExtreme }

    public enum TpbTrig { LevelReclaim, BreakPrevHigh }

    public enum AnalysisWindow { Days, Bars }

    public enum EngineFilter
    {
        All,
        PreExpansion,
        BreakStructure,
        Pullback,
        Elephant,
        ZoneRetest,
        TrendPullback,
        RangeFakeout,
        PriorDaySweep,
        FvgRetest,
        SmcStructure
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SuperQuantX_Alpha : Indicator
    {
        // ===================== SAFETY =======================================
        // cTrader keeps saved parameter values per instance, so a default
        // changed in code never reaches an instance that already exists.
        // Leave this on and the shipped values are used regardless.
        // A signal is only known once the bar closes, so the close is the
        // first price that can actually be traded. LevelPrice records a fill
        // at a price that occurred earlier in that same bar, which no order
        // placed on the close could have obtained.
        [Parameter("Fill Model", Group = "General", DefaultValue = FillModel.CloseOfSignalBar)]
        public FillModel Fills { get; set; }

        // Read from whichever broker the chart is attached to, so the same
        // file measures correctly on Deriv, FundedNext, FTMO or anything else.
        [Parameter("Dealing Cost", Group = "General", DefaultValue = CostSource.FromBroker)]
        public CostSource CostFrom { get; set; }

        [Parameter("Manual Spread (price)", Group = "General", DefaultValue = 0.30, MinValue = 0.00, MaxValue = 50.00)]
        public double ManualSpread { get; set; }

        // Off means one engine can never block another. Spacing is applied
        // per engine instead, inside the signal path.
        // The last shared path. With this off, an engine's numbers depend on
        // nothing but its own rules and its own per-engine cap.
        [Parameter("Use Global Daily Cap", Group = "General", DefaultValue = false)]
        public bool UseGlobalCap { get; set; }

        [Parameter("Global Bar Spacing", Group = "General", DefaultValue = false)]
        public bool GlobalSpacing { get; set; }

        // Off means the engine still prints on the chart and still counts
        // toward A+ agreement, but is excluded from every dashboard number.
        [Parameter("Pullback In Dashboard", Group = "General", DefaultValue = false)]
        public bool PbInStats { get; set; }

        [Parameter("Trend Pullback In Dashboard", Group = "General", DefaultValue = false)]
        public bool TpbInStats { get; set; }

        // An engine earns its place in the dashboard by clearing the 25%
        // breakeven at 1:3. Below that it prints on the chart for confluence
        // and stays out of every number.
        [Parameter("FVG In Dashboard", Group = "General", DefaultValue = false)]
        public bool FvgInStats { get; set; }

        [Parameter("SMC In Dashboard", Group = "General", DefaultValue = false)]
        public bool SmcInStats { get; set; }

        // A signal on a bar that has not closed is not a signal yet. This
        // draws a provisional mark only, and nothing enters the numbers.
        [Parameter("Live Preview Mark", Group = "General", DefaultValue = true)]
        public bool LivePreview { get; set; }

        [Parameter("Shipped Visuals", Group = "General", DefaultValue = true)]
        public bool UseShipped { get; set; }

        // ===================== GENERAL ======================================
        [Parameter("History Preload Bars", Group = "General", DefaultValue = 30000, MinValue = 500, MaxValue = 200000)]
        public int PreloadBars { get; set; }

        // Days makes every timeframe cover the same calendar period, which is
        // the only way the numbers can be compared across timeframes.
        [Parameter("Analysis Window", Group = "General", DefaultValue = AnalysisWindow.Days)]
        public AnalysisWindow AnaMode { get; set; }

        [Parameter("Analyse Last N Days", Group = "General", DefaultValue = 180, MinValue = 5, MaxValue = 3000)]
        public int AnalyseDays { get; set; }

        // Slides the whole analysis window back in time. Every setting in this
        // file was chosen on the most recent 180 days. Set this to 180 and the
        // engines are scored on data they were never tuned against.
        [Parameter("Window Offset Days", Group = "General", DefaultValue = 0, MinValue = 0, MaxValue = 2000)]
        public int WindowOffsetDays { get; set; }

        // Every lookback is in bars, so 150 bars is 6 days on h1 and 1.5 days
        // on m15. Scaling converts them to comparable spans of time.
        [Parameter("Scale Lookbacks By Timeframe", Group = "General", DefaultValue = true)]
        public bool ScaleLookbacks { get; set; }

        [Parameter("Reference Minutes", Group = "General", DefaultValue = 60, MinValue = 1, MaxValue = 1440)]
        public int RefMinutes { get; set; }

        [Parameter("ATR Period", Group = "General", DefaultValue = 14, MinValue = 2, MaxValue = 200)]
        public int AtrPeriod { get; set; }

        [Parameter("Arrow Price", Group = "General", DefaultValue = EntryPriceMode.ConfirmedPrice)]
        public EntryPriceMode EntryMode { get; set; }

        [Parameter("Signals All Engines Per Day", Group = "General", DefaultValue = 32, MinValue = 1, MaxValue = 200)]
        public int InDailyTotal { get; set; }

        [Parameter("Min Bar Gap", Group = "General", DefaultValue = 2, MinValue = 0, MaxValue = 50)]
        public int InBarGap { get; set; }

        // Each engine gets its own daily allowance. Without this the engine
        // that fires first eats the whole cap and the best engine gets nothing.
        [Parameter("Signals Per Engine Per Day", Group = "General", DefaultValue = 4, MinValue = 1, MaxValue = 50)]
        public int PerEngineDaily { get; set; }

        [Parameter("Streak Counted Per Engine", Group = "Signal Limits", DefaultValue = false)]
        public bool StreakPerEngine { get; set; }

        // Spacing applied inside a single engine. A busy engine can no longer
        // crowd out a quiet one, because each keeps its own clock.
        [Parameter("Min Bars Between Same Engine (0 = off)", Group = "General", DefaultValue = 0, MinValue = 0, MaxValue = 200)]
        public int MinGapSameEngine { get; set; }

        // ===================== SIGNAL LIMITS ================================
        // First three sells of the day are the signal. The fourth, fifth and
        // sixth are noise. The streak resets when direction flips and at the
        // start of each new day.
        [Parameter("Limit Consecutive Signals", Group = "Signal Limits", DefaultValue = true)]
        public bool LimitConsecutive { get; set; }

        [Parameter("Max Consecutive Same Direction", Group = "Signal Limits", DefaultValue = 3, MinValue = 1, MaxValue = 20)]
        public int MaxConsecutive { get; set; }

        [Parameter("Reset Streak Each Day", Group = "Signal Limits", DefaultValue = true)]
        public bool ResetStreakDaily { get; set; }

        // After three in the same direction the rest are greyed out and kept
        // out of the numbers until a counter signal appears, rather than
        // deleted. Set to Block to refuse them outright.
        [Parameter("Streak Signal Mode", Group = "Signal Limits", DefaultValue = ConsoMode.Allow)]
        public ConsoMode StreakMode { get; set; }

        // ===================== TRADING WINDOW ===============================
        // Day trading hours, 01:00 to 20:00 South African time, which is
        // 23:00 to 18:00 UTC. Ignored on swing timeframes.
        [Parameter("Use Trading Window", Group = "Trading Window", DefaultValue = true)]
        public bool UseSession { get; set; }

        [Parameter("Start Hour (UTC)", Group = "Trading Window", DefaultValue = 23, MinValue = 0, MaxValue = 23)]
        public int SessionStart { get; set; }

        [Parameter("End Hour (UTC)", Group = "Trading Window", DefaultValue = 18, MinValue = 0, MaxValue = 23)]
        public int SessionEnd { get; set; }

        [Parameter("Swing TF From (minutes)", Group = "Trading Window", DefaultValue = 60, MinValue = 1, MaxValue = 10080)]
        public int SwingFromMinutes { get; set; }

        // ===================== PRE-EXPANSION ================================
        [Parameter("Pre-Expansion Signals", Group = "Pre-Expansion", DefaultValue = true)]
        public bool UsePreExpansion { get; set; }

        // Nothing may block this engine: not session hours, not the daily caps,
        // not spacing, not the streak, not any filter, not the stop size cap.
        [Parameter("Pre-Expansion Never Blocked", Group = "Pre-Expansion", DefaultValue = true)]
        public bool PreExpNeverBlocked { get; set; }

        [Parameter("Compression Window", Group = "Pre-Expansion", DefaultValue = 12, MinValue = 3, MaxValue = 100)]
        public int InCompWindow { get; set; }

        [Parameter("Rank Lookback", Group = "Pre-Expansion", DefaultValue = 150, MinValue = 20, MaxValue = 1000)]
        public int InRankLookback { get; set; }

        [Parameter("Tightness Percentile", Group = "Pre-Expansion", DefaultValue = 28, MinValue = 1, MaxValue = 60)]
        public int TightPctIn { get; set; }

        [Parameter("Max Window vs ATR", Group = "Pre-Expansion", DefaultValue = 3.0, MinValue = 0.5, MaxValue = 20.0)]
        public double InMaxWindowAtr { get; set; }

        [Parameter("Require Falling ATR", Group = "Pre-Expansion", DefaultValue = true)]
        public bool RequireFallingAtr { get; set; }

        [Parameter("Falling ATR Lookback", Group = "Pre-Expansion", DefaultValue = 10, MinValue = 2, MaxValue = 100)]
        public int AtrFallLookback { get; set; }

        [Parameter("Require Edge Sweep", Group = "Pre-Expansion", DefaultValue = false)]
        public bool RequireSweep { get; set; }

        [Parameter("Trigger Buffer (ATR)", Group = "Pre-Expansion", DefaultValue = 0.08, MinValue = 0.00, MaxValue = 1.00)]
        public double TriggerBufferAtr { get; set; }

        [Parameter("Arm Validity Bars", Group = "Pre-Expansion", DefaultValue = 18, MinValue = 1, MaxValue = 80)]
        public int ArmBars { get; set; }

        // Only one zone could ever be armed at a time, so a second compression
        // forming while the first was still live was never seen. Each zone must
        // still pass the same score, so nothing about quality changes.
        [Parameter("Max Concurrent Arms", Group = "Pre-Expansion", DefaultValue = 4, MinValue = 1, MaxValue = 12)]
        public int MaxArms { get; set; }

        [Parameter("Arm Separation (ATR)", Group = "Pre-Expansion", DefaultValue = 0.40, MinValue = 0.00, MaxValue = 5.00)]
        public double ArmSeparationAtr { get; set; }

        [Parameter("Score Floor (0-100)", Group = "Pre-Expansion", DefaultValue = 55, MinValue = 0, MaxValue = 100)]
        public int InScoreFloor { get; set; }

        // ===================== VOLUME =======================================
        [Parameter("Volume Dry-Up Scoring", Group = "Volume", DefaultValue = true)]
        public bool UseVolumeDryUp { get; set; }

        [Parameter("Dry-Up Baseline", Group = "Volume", DefaultValue = 60, MinValue = 10, MaxValue = 500)]
        public int InDryUpLookback { get; set; }

        [Parameter("Require Volume Surge", Group = "Volume", DefaultValue = true)]
        public bool RequireVolumeSurge { get; set; }

        [Parameter("Volume Surge Multiple", Group = "Volume", DefaultValue = 1.30, MinValue = 1.00, MaxValue = 10.00)]
        public double InVolSurge { get; set; }

        // A multiple of average tick volume is broker-specific: every feed has
        // its own tick magnitude, so the same gate behaves differently on each.
        // A rank against this chart's own recent bars is broker-neutral.
        [Parameter("Volume Test Mode", Group = "Pre-Expansion", DefaultValue = VolTest.SurgeMultiple)]
        public VolTest VolTestMode { get; set; }

        [Parameter("Volume Percentile", Group = "Pre-Expansion", DefaultValue = 78, MinValue = 0, MaxValue = 95)]
        public int VolPct { get; set; }

        [Parameter("Volume Rate Lookback", Group = "Volume", DefaultValue = 50, MinValue = 5, MaxValue = 500)]
        public int InVolRateLookback { get; set; }

        // ===================== VOLUME PROFILE ===============================
        [Parameter("Use Volume Profile Nodes", Group = "Volume Profile", DefaultValue = true)]
        public bool UseProfile { get; set; }

        [Parameter("Profile Lookback", Group = "Volume Profile", DefaultValue = 200, MinValue = 30, MaxValue = 2000)]
        public int InProfileLookback { get; set; }

        [Parameter("Profile Bins", Group = "Volume Profile", DefaultValue = 50, MinValue = 10, MaxValue = 200)]
        public int ProfileBins { get; set; }

        [Parameter("Low Volume Node Threshold", Group = "Volume Profile", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 1.00)]
        public double LvnThreshold { get; set; }

        // ===================== MOMENTUM =====================================
        [Parameter("Require Expansion At Trigger", Group = "Momentum", DefaultValue = true)]
        public bool RequireExpansion { get; set; }

        [Parameter("Min Trigger Bar Range (ATR)", Group = "Momentum", DefaultValue = 0.55, MinValue = 0.00, MaxValue = 5.00)]
        public double MinTriggerRangeAtr { get; set; }

        [Parameter("Max Chase From Level (ATR)", Group = "Momentum", DefaultValue = 0.90, MinValue = 0.05, MaxValue = 5.00)]
        public double InMaxChase { get; set; }

        // ===================== ELEPHANT =====================================
        // BOS, Pullback and Elephant all read the same elephant list, so
        // qualifying the candle fixes all three at once. Same tests that turned
        // a large candle into an order block for Zone retest.
        // The engine buys the close of an already large candle. The measured
        // failure is entering extended, so this rejects elephants that fire
        // when price has already travelled far from the trend mean.
        [Parameter("Elephant Signal Min Size ATR", Group = "Elephant", DefaultValue = 3.50, MinValue = 1.00, MaxValue = 10.00)]
        public double EleSigMinSize { get; set; }

        [Parameter("Elephant Extension Cap", Group = "Elephant", DefaultValue = 20.00, MinValue = 0.20, MaxValue = 20.00)]
        public double EleExtCap { get; set; }

        // Where the stop sits inside the candle. Midpoint is the current
        // behaviour. Quarter and Third are tighter, which brings the 3R target
        // nearer in price terms.
        [Parameter("Elephant Stop At", Group = "Elephant", DefaultValue = EleStopAt.Midpoint)]
        public EleStopAt EleStopWhere { get; set; }

        // Requires the elephant to expand out of a quiet stretch rather than
        // continue a move already under way.
        [Parameter("Elephant Needs Base", Group = "Elephant", DefaultValue = false)]
        public bool EleNeedsBase { get; set; }

        [Parameter("Elephant Base Bars", Group = "Elephant", DefaultValue = 6, MinValue = 2, MaxValue = 40)]
        public int EleBaseBars { get; set; }

        [Parameter("Elephant Base Max ATR", Group = "Elephant", DefaultValue = 2.00, MinValue = 0.20, MaxValue = 10.00)]
        public double EleBaseMax { get; set; }

        [Parameter("Elephant BOS Gate", Group = "Elephant", DefaultValue = false)]
        public bool EleBosGate { get; set; }

        [Parameter("Elephant Gap Gate", Group = "Elephant", DefaultValue = false)]
        public bool EleGapGate { get; set; }

        [Parameter("Elephant Run Gate", Group = "Elephant", DefaultValue = false)]
        public bool EleRunGate { get; set; }

        [Parameter("Elephant Signals", Group = "Elephant", DefaultValue = true)]
        public bool SignalOnElephantBar { get; set; }

        [Parameter("Elephant ATR Multiple", Group = "Elephant", DefaultValue = 2.0, MinValue = 1.0, MaxValue = 10.0)]
        public double EleAtrMult { get; set; }

        [Parameter("Elephant Min Ticks (0 = off)", Group = "Elephant", DefaultValue = 0, MinValue = 0, MaxValue = 1000000)]
        public int EleMinTicks { get; set; }

        [Parameter("Use Top-Percentile Gate", Group = "Elephant", DefaultValue = true)]
        public bool UseTopPctGate { get; set; }

        [Parameter("Elephant Top Percentile %", Group = "Elephant", DefaultValue = 10, MinValue = 1, MaxValue = 50)]
        public int EleTopPct { get; set; }

        [Parameter("Elephant Lookback", Group = "Elephant", DefaultValue = 120, MinValue = 20, MaxValue = 2000)]
        public int InEleLookback { get; set; }

        [Parameter("Elephant Min Body %", Group = "Elephant", DefaultValue = 60, MinValue = 1, MaxValue = 99)]
        public int EleMinBodyPct { get; set; }

        [Parameter("Min Close Location", Group = "Elephant", DefaultValue = 0.70, MinValue = 0.50, MaxValue = 0.98)]
        public double MinCloseLoc { get; set; }

        [Parameter("Max Opposite Wick %", Group = "Elephant", DefaultValue = 35, MinValue = 0, MaxValue = 100)]
        public int MaxOppWickPct { get; set; }

        // ===================== CONTINUATIONS ================================
        // Break structure and Pullback used to read the Elephant engine's
        // candle list, so any Elephant setting moved them. They now detect
        // their own source candle with their own parameters. Defaults match
        // the old Elephant values, so today's behaviour is unchanged.
        [Parameter("Cont Source Min ATR", Group = "Continuations", DefaultValue = 2.00, MinValue = 1.0, MaxValue = 10.0)]
        public double ContListMinAtr { get; set; }

        // Break structure measured +21.63 R at 2.0 and -0.30 R at 2.5, while
        // Pullback measured 12.9% below 2.5 and 30% above it. They need
        // different thresholds, so each filters the shared list itself.
        [Parameter("BOS Min Source ATR", Group = "Continuations", DefaultValue = 2.00, MinValue = 1.0, MaxValue = 10.0)]
        public double BosMinSrcAtr { get; set; }

        [Parameter("PB Min Source ATR", Group = "Continuations", DefaultValue = 2.00, MinValue = 1.0, MaxValue = 10.0)]
        public double PbSrcMinAtr { get; set; }

        [Parameter("Cont Min Body %", Group = "Continuations", DefaultValue = 60, MinValue = 1, MaxValue = 99)]
        public int ContMinBodyPct { get; set; }

        [Parameter("Cont Min Close Location", Group = "Continuations", DefaultValue = 0.70, MinValue = 0.50, MaxValue = 0.98)]
        public double ContMinCloseLoc { get; set; }

        [Parameter("Cont Max Opposite Wick %", Group = "Continuations", DefaultValue = 35, MinValue = 0, MaxValue = 100)]
        public int ContMaxOppWick { get; set; }

        [Parameter("Cont Top Percentile % (0 = off)", Group = "Continuations", DefaultValue = 10, MinValue = 0, MaxValue = 50)]
        public int ContTopPct { get; set; }

        [Parameter("Cont Lookback", Group = "Continuations", DefaultValue = 120, MinValue = 20, MaxValue = 2000)]
        public int InContLookback { get; set; }

        [Parameter("Breakout Signals", Group = "Continuations", DefaultValue = true)]
        public bool UseBreakout { get; set; }

        [Parameter("Breakout Window", Group = "Continuations", DefaultValue = 8, MinValue = 1, MaxValue = 50)]
        public int BreakWindow { get; set; }

        [Parameter("Breakout Buffer (ATR)", Group = "Continuations", DefaultValue = 0.10, MinValue = 0.00, MaxValue = 2.00)]
        public double BreakBufferAtr { get; set; }

        // Pullback never set its own stop, so it fell through to the generic
        // structure stop, often far below the source candle. Risk that wide
        // makes a 3R target unreachable. ZoneEdge puts the stop just past the
        // retest zone it is actually trading.
        [Parameter("Pullback Stop Source", Group = "Continuations", DefaultValue = PbStopAt.StructureDefault)]
        public PbStopAt PbStopFrom { get; set; }

        [Parameter("Pullback Stop Buffer ATR", Group = "Continuations", DefaultValue = 0.20, MinValue = 0.00, MaxValue = 2.00)]
        public double PbStopBuf { get; set; }

        [Parameter("Pullback Signals", Group = "Continuations", DefaultValue = true)]
        public bool UsePullback { get; set; }

        [Parameter("Pullback Window", Group = "Continuations", DefaultValue = 10, MinValue = 1, MaxValue = 50)]
        public int PullWindow { get; set; }

        [Parameter("Pullback Zone Low %", Group = "Continuations", DefaultValue = 0.38, MinValue = 0.10, MaxValue = 0.60)]
        public double PullLowPct { get; set; }

        [Parameter("Pullback Zone High %", Group = "Continuations", DefaultValue = 0.62, MinValue = 0.20, MaxValue = 0.90)]
        public double PullHighPct { get; set; }

        [Parameter("Re-entry Past Mid %", Group = "Continuations", DefaultValue = 0.50, MinValue = 0.30, MaxValue = 0.80)]
        public double ReentryPct { get; set; }

        // ===================== ZONE RETEST ==================================
        // An impulse leaves behind a supply or demand zone. Price coming back
        // into that zone and being rejected is the trade. The stop is the zone
        // edge, which is why this can carry a far tighter stop than anything
        // anchored to the impulse candle itself.
        [Parameter("Zone Retest Signals", Group = "Zone Retest", DefaultValue = true)]
        public bool UseZones { get; set; }

        [Parameter("Zone Source", Group = "Zone Retest", DefaultValue = ZoneSource.LastOpposingCandle)]
        public ZoneSource ZoneFrom { get; set; }

        [Parameter("Origin Lookback (bars)", Group = "Zone Retest", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int ZoneOriginBack { get; set; }

        [Parameter("Min Impulse (ATR)", Group = "Zone Retest", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 10.0)]
        public double ZoneMinImpulseAtr { get; set; }

        [Parameter("Zone Life (bars)", Group = "Zone Retest", DefaultValue = 40, MinValue = 3, MaxValue = 500)]
        public int ZoneLifeBars { get; set; }

        // Measured on 218 zone trades: zones retested within 10 bars won
        // 31.8%, older ones 23.2%. Wide zones were the worst bucket at 23.1%.
        // An order block is the origin of a move that broke structure and
        // left an imbalance behind it. Without these two tests any large
        // candle becomes a zone, which is what the engine was doing.
        // Refine to the part of the origin candle that actually carries the
        // imbalance. A tighter zone means a tighter stop and a better R:R.
        [Parameter("Zone Refine", Group = "Zone Retest", DefaultValue = ZoneRefine.Imbalance)]
        public ZoneRefine ZoneRefineMode { get; set; }

        // An order block that formed after taking out a high or low is a
        // reversal from liquidity, not just a large candle.
        [Parameter("Zone Needs Sweep", Group = "Zone Retest", DefaultValue = true)]
        public bool ZoneNeedsSweep { get; set; }

        [Parameter("Zone Sweep Lookback", Group = "Zone Retest", DefaultValue = 8, MinValue = 2, MaxValue = 40)]
        public int ZoneSweepBack { get; set; }

        // Enter at the edge of the zone rather than wherever the bar closed.
        [Parameter("Zone Entry Price", Group = "Zone Retest", DefaultValue = ZoneEntryAt.ZoneEdge)]
        public ZoneEntryAt ZoneEntryMode { get; set; }

        // Built and shown on the label. Floor stays at 0 so it records without
        // filtering; three new tests at once already changes the population.
        [Parameter("Zone Min Score", Group = "Zone Retest", DefaultValue = 0, MinValue = 0, MaxValue = 100)]
        public int ZoneMinScore { get; set; }

        [Parameter("Zone Needs Break Of Structure", Group = "Zone Retest", DefaultValue = true)]
        public bool ZoneNeedsBos { get; set; }

        [Parameter("Zone Needs Imbalance", Group = "Zone Retest", DefaultValue = true)]
        public bool ZoneNeedsGap { get; set; }

        [Parameter("Zone Max Age", Group = "Zone Retest", DefaultValue = 40, MinValue = 1, MaxValue = 500)]
        public int ZoneAgeCap { get; set; }

        [Parameter("Zone Size Cap ATR", Group = "Zone Retest", DefaultValue = 2.00, MinValue = 0.10, MaxValue = 10.00)]
        public double ZoneSizeCap { get; set; }

        [Parameter("Max Touches", Group = "Zone Retest", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int ZoneMaxTouches { get; set; }

        [Parameter("Entry Trigger", Group = "Zone Retest", DefaultValue = ZoneTrigger.RejectionClose)]
        public ZoneTrigger ZoneTriggerMode { get; set; }

        [Parameter("Zone Stop Buffer x ATR", Group = "Zone Retest", DefaultValue = 0.20, MinValue = 0.00, MaxValue = 2.00)]
        public double InZoneBuf { get; set; }

        [Parameter("Zone Min Stop x ATR", Group = "Zone Retest", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 3.00)]
        public double InZoneMin { get; set; }

        [Parameter("Kill Zone If Closed Through", Group = "Zone Retest", DefaultValue = true)]
        public bool ZoneKillOnBreak { get; set; }

        [Parameter("Draw Zones", Group = "Zone Retest", DefaultValue = false)]
        public bool DrawZoneBoxes { get; set; }

        [Parameter("Supply Colour", Group = "Zone Retest", DefaultValue = "IndianRed")]
        public Color SupplyColor { get; set; }

        [Parameter("Demand Colour", Group = "Zone Retest", DefaultValue = "MediumSeaGreen")]
        public Color DemandColor { get; set; }

        [Parameter("Zone Opacity", Group = "Zone Retest", DefaultValue = 0.14, MinValue = 0.00, MaxValue = 1.00)]
        public double ZoneOpacity { get; set; }

        // ===================== RISK =========================================
        [Parameter("Show Stop And Target", Group = "Risk", DefaultValue = true)]
        public bool ShowRisk { get; set; }

        [Parameter("Stop Source", Group = "Risk", DefaultValue = StopSource.NearestStructure)]
        public StopSource InStopFrom { get; set; }

        [Parameter("Swing Strength", Group = "Risk", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int InSwingStrength { get; set; }

        [Parameter("Structure Lookback", Group = "Risk", DefaultValue = 30, MinValue = 5, MaxValue = 300)]
        public int InStructureLookback { get; set; }

        [Parameter("Min Structure Distance (ATR)", Group = "Risk", DefaultValue = 0.75, MinValue = 0.00, MaxValue = 3.00)]
        public double MinStructDistAtr { get; set; }

        [Parameter("Stop Buffer x ATR", Group = "Risk", DefaultValue = 0.40, MinValue = 0.00, MaxValue = 2.00)]
        public double InStopBuf { get; set; }

        // A fixed ATR fraction ignores how long the wicks actually are on this
        // instrument. This pushes the stop past typical wick noise instead.
        [Parameter("Wick Buffer Lookback", Group = "Risk", DefaultValue = 20, MinValue = 5, MaxValue = 200)]
        public int WickLookback { get; set; }

        [Parameter("Wick Buffer Multiple", Group = "Risk", DefaultValue = 1.30, MinValue = 0.00, MaxValue = 5.00)]
        public double WickBufferMult { get; set; }

        [Parameter("Min Stop x ATR", Group = "Risk", DefaultValue = 0.75, MinValue = 0.05, MaxValue = 3.00)]
        public double InStopMin { get; set; }

        [Parameter("Max Stop x ATR", Group = "Risk", DefaultValue = 2.50, MinValue = 0.20, MaxValue = 10.00)]
        public double InStopMax { get; set; }

        [Parameter("Min Reward To Risk", Group = "Risk", DefaultValue = 3.0, MinValue = 1.0, MaxValue = 20.0)]
        public double MinR { get; set; }

        [Parameter("Extend Target To Liquidity", Group = "Risk", DefaultValue = false)]
        public bool ExtendTpToLiquidity { get; set; }

        [Parameter("Use Break Even", Group = "Risk", DefaultValue = true)]
        public bool UseBreakEven { get; set; }

        [Parameter("Break Even At R", Group = "Risk", DefaultValue = 1.0, MinValue = 0.2, MaxValue = 5.0)]
        public double BreakEvenAtR { get; set; }

        // 90% of losses reach 0.5R then reverse for a full stop. This halves
        // the risk at that point instead of waiting for break even at 1R.
        [Parameter("Use Risk Reduction", Group = "Risk", DefaultValue = false)]
        public bool RiskCutOn { get; set; }

        [Parameter("Reduce Risk At R", Group = "Risk", DefaultValue = 0.50, MinValue = 0.10, MaxValue = 2.00)]
        public double CutAtR { get; set; }

        [Parameter("Reduce Risk To R", Group = "Risk", DefaultValue = 0.50, MinValue = 0.05, MaxValue = 1.00)]
        public double CutToR { get; set; }

        [Parameter("Drop Stop At Break Even", Group = "Risk", DefaultValue = true)]
        public bool DropStopAtBe { get; set; }

        // A stop is only "too tight" if price barely went past it before
        // turning. If it ran well beyond, the stop was not the problem.
        [Parameter("Tight Stop Overshoot (R)", Group = "Risk", DefaultValue = 0.30, MinValue = 0.05, MaxValue = 2.00)]
        public double TightOvershootR { get; set; }

        // Held in days, not bars, so an h1 swing can run for weeks while an
        // m5 scalp is closed out the same session.
        [Parameter("Trade Window (days)", Group = "Risk", DefaultValue = 10, MinValue = 1, MaxValue = 400)]
        public int TradeWindowDays { get; set; }

        [Parameter("Stop Colour", Group = "Risk", DefaultValue = "OrangeRed")]
        public Color StopColor { get; set; }

        [Parameter("Target Colour", Group = "Risk", DefaultValue = "LimeGreen")]
        public Color TargetColor { get; set; }

        [Parameter("Risk Label Size", Group = "Risk", DefaultValue = 8, MinValue = 5, MaxValue = 30)]
        public int RiskLabelSize { get; set; }

        [Parameter("Outcome Marker Size", Group = "Risk", DefaultValue = 12, MinValue = 5, MaxValue = 40)]
        public int OutcomeInMarkerSize { get; set; }

        [Parameter("Show R On Target", Group = "Risk", DefaultValue = true)]
        public bool ShowRonTarget { get; set; }

        // 0 keeps every stop and target label on the chart. Anything above 0
        // deletes the oldest pair once that many are on screen, which is what
        // makes labels appear then vanish while history is being drawn.
        [Parameter("Risk Labels For Last N (0 = all)", Group = "Risk", DefaultValue = 0, MinValue = 0, MaxValue = 500)]
        public int RiskRecentN { get; set; }

        // The SL and TP text sits clear of its own price line, so the tick or
        // cross that lands on that line later cannot print on top of it.
        [Parameter("Risk Label Offset (ATR)", Group = "Risk", DefaultValue = 0.20, MinValue = 0.00, MaxValue = 2.00)]
        public double RiskLabelOffsetAtr { get; set; }

        // ===================== TIMEFRAME FOCUS ==============================
        // m5, m15 and m30 are the working timeframes. h1 is allowed for swing.
        // Anything else still prints but is greyed and kept out of the numbers.
        [Parameter("Primary Timeframes Only", Group = "Timeframe Focus", DefaultValue = true)]
        public bool PrimaryTfOnly { get; set; }

        [Parameter("Allow Swing Timeframe", Group = "Timeframe Focus", DefaultValue = true)]
        public bool AllowSwingTf { get; set; }

        [Parameter("Off-Timeframe Signals", Group = "Timeframe Focus", DefaultValue = ConsoMode.MarkGrey)]
        public ConsoMode OffTfMode { get; set; }

        // ===================== CONFLUENCE ===================================
        // When several engines call the same direction inside a short window,
        // that is the strongest read the indicator can give. Marked A+.
        [Parameter("Confluence Window (bars)", Group = "Confluence", DefaultValue = 10, MinValue = 1, MaxValue = 100)]
        public int ConfluenceWindow { get; set; }

        [Parameter("A+ Minimum Engines", Group = "Confluence", DefaultValue = 3, MinValue = 2, MaxValue = 8)]
        public int AplusMinEngines { get; set; }

        [Parameter("A+ Colour", Group = "Confluence", DefaultValue = "Magenta")]
        public Color InAplusColor { get; set; }

        [Parameter("A+ Marker Size", Group = "Confluence", DefaultValue = 18, MinValue = 5, MaxValue = 44)]
        public int InAplusSize { get; set; }

        [Parameter("A+ Only", Group = "Confluence", DefaultValue = false)]
        public bool AplusOnly { get; set; }

        // ===================== SIGNAL QUALITY ===============================
        // The shared layer every engine is judged against: with the trend, on
        // the right side of equilibrium, and optionally at a supply or demand
        // zone. Tuned engine by engine from here.
        [Parameter("Require Pro Trend", Group = "Signal Quality", DefaultValue = true)]
        public bool RequireProTrend { get; set; }

        [Parameter("Require Premium or Discount", Group = "Signal Quality", DefaultValue = false)]
        public bool RequirePremiumDiscount { get; set; }

        [Parameter("Dealing Range Pivots", Group = "Signal Quality", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int DealingPivots { get; set; }

        [Parameter("Equilibrium %", Group = "Signal Quality", DefaultValue = 50, MinValue = 20, MaxValue = 80)]
        public int EquilibriumPct { get; set; }

        [Parameter("Require Zone Confluence", Group = "Signal Quality", DefaultValue = false)]
        public bool RequireZoneConf { get; set; }

        [Parameter("Zone Confluence (ATR)", Group = "Signal Quality", DefaultValue = 0.75, MinValue = 0.05, MaxValue = 5.00)]
        public double ZoneConfAtr { get; set; }

        [Parameter("Signal Quality Mode", Group = "Signal Quality", DefaultValue = ConsoMode.Block)]
        public ConsoMode QualityGate { get; set; }

        [Parameter("Quality On Pre-Expansion", Group = "Signal Quality", DefaultValue = true)]
        public bool QualityCoversPreExp { get; set; }

        // ===================== RANGE FAKEOUT ================================
        // Price pokes out of a range then closes back inside. The break failed,
        // so the move is back across the range. None of the other engines can
        // see this: they all trade continuation.
        // ===================== FVG RETEST ===================================
        // A three bar imbalance is unfilled orderflow. Price returning into it
        // and rejecting is the trade. Like Zone retest it enters AT the level,
        // which is the single property separating the winning engines from the
        // losing ones in this file: 34.6% against 18.2%.
        [Parameter("FVG Signals", Group = "FVG Retest", DefaultValue = true)]
        public bool UseFvg { get; set; }

        // The middle candle must be the displacement that created the gap,
        // and it must point the same way as the gap. Without this any three
        // bar sequence with a sliver of space between bar one and bar three
        // counts, which is why 2242 were found.
        [Parameter("FVG Min Displacement ATR", Group = "FVG Retest", DefaultValue = 1.00, MinValue = 0.00, MaxValue = 6.00)]
        public double FvgMinDisp { get; set; }

        [Parameter("FVG Min Body %", Group = "FVG Retest", DefaultValue = 50, MinValue = 0, MaxValue = 99)]
        public int FvgMinBodyPct { get; set; }

        [Parameter("FVG Min Gap ATR", Group = "FVG Retest", DefaultValue = 0.25, MinValue = 0.02, MaxValue = 5.00)]
        public double FvgMinGap { get; set; }

        [Parameter("FVG Life Bars", Group = "FVG Retest", DefaultValue = 40, MinValue = 3, MaxValue = 500)]
        public int FvgLife { get; set; }

        [Parameter("FVG Stop Buffer ATR", Group = "FVG Retest", DefaultValue = 0.20, MinValue = 0.00, MaxValue = 2.00)]
        public double FvgStopBuf { get; set; }

        [Parameter("FVG Kill If Closed Through", Group = "FVG Retest", DefaultValue = true)]
        public bool FvgKillOnBreak { get; set; }

        [Parameter("Draw FVG", Group = "FVG Retest", DefaultValue = false)]
        public bool DrawFvgBoxes { get; set; }

        // ===================== SMC STRUCTURE ZONE ===========================
        // One engine built from the four files you sent, using only the parts
        // that hold up. Its own pivots, its own break of structure, its own
        // imbalance pool, its own zones. It reads nothing from any other
        // engine and nothing else reads it.
        [Parameter("SMC Signals", Group = "SMC Structure", DefaultValue = true)]
        public bool UseSmc { get; set; }

        [Parameter("SMC Swing Length", Group = "SMC Structure", DefaultValue = 5, MinValue = 1, MaxValue = 50)]
        public int SmcSwing { get; set; }

        // A one tick poke through a swing is not a break of structure.
        [Parameter("SMC Displacement ATR", Group = "SMC Structure", DefaultValue = 0.25, MinValue = 0.00, MaxValue = 3.00)]
        public double SmcDisp { get; set; }

        [Parameter("SMC Min Gap ATR", Group = "SMC Structure", DefaultValue = 0.30, MinValue = 0.02, MaxValue = 5.00)]
        public double SmcMinGap { get; set; }

        [Parameter("SMC Max Zone ATR (0 = off)", Group = "SMC Structure", DefaultValue = 1.50, MinValue = 0.00, MaxValue = 10.00)]
        public double SmcMaxZone { get; set; }

        [Parameter("SMC Zone Age (0 = never)", Group = "SMC Structure", DefaultValue = 0, MinValue = 0, MaxValue = 5000)]
        public int SmcZoneAge { get; set; }

        [Parameter("SMC Live Zones Kept", Group = "SMC Structure", DefaultValue = 40, MinValue = 1, MaxValue = 200)]
        public int SmcMaxZones { get; set; }

        // A demand zone dies the moment structure breaks down. Price has
        // traded through the level that created it.
        [Parameter("SMC Purge On Flip", Group = "SMC Structure", DefaultValue = true)]
        public bool SmcPurge { get; set; }

        [Parameter("SMC Trend Gate", Group = "SMC Structure", DefaultValue = true)]
        public bool SmcTrendGate { get; set; }

        [Parameter("SMC Premium / Discount Gate", Group = "SMC Structure", DefaultValue = false)]
        public bool SmcPdGate { get; set; }

        [Parameter("SMC Equilibrium %", Group = "SMC Structure", DefaultValue = 50, MinValue = 10, MaxValue = 90)]
        public int SmcEqPct { get; set; }

        // Dead markets do not reach 3R. Skip when volatility is below its own
        // recent average.
        [Parameter("SMC Volatility Gate", Group = "SMC Structure", DefaultValue = true)]
        public bool SmcVolGate { get; set; }

        [Parameter("SMC Min ATR vs Average", Group = "SMC Structure", DefaultValue = 0.80, MinValue = 0.10, MaxValue = 3.00)]
        public double SmcMinAtrMult { get; set; }

        [Parameter("SMC ATR Average Bars", Group = "SMC Structure", DefaultValue = 200, MinValue = 20, MaxValue = 2000)]
        public int SmcAtrAvgBars { get; set; }

        [Parameter("SMC Stop Pad ATR", Group = "SMC Structure", DefaultValue = 0.10, MinValue = 0.00, MaxValue = 2.00)]
        public double SmcStopPad { get; set; }

        [Parameter("Range Fakeout Signals", Group = "Range Fakeout", DefaultValue = true)]
        public bool UseFakeout { get; set; }

        [Parameter("Min Poke (ATR)", Group = "Range Fakeout", DefaultValue = 0.10, MinValue = 0.00, MaxValue = 3.00)]
        public double FakeMinPoke { get; set; }

        [Parameter("Max Poke (ATR)", Group = "Range Fakeout", DefaultValue = 1.20, MinValue = 0.20, MaxValue = 6.00)]
        public double FakeMaxPoke { get; set; }

        [Parameter("Min Reclaim Close Location", Group = "Range Fakeout", DefaultValue = 0.60, MinValue = 0.30, MaxValue = 0.95)]
        public double FakeReclaimClv { get; set; }

        [Parameter("Fakeout Valid For (bars)", Group = "Range Fakeout", DefaultValue = 6, MinValue = 1, MaxValue = 50)]
        public int FakeValidBars { get; set; }

        // ===================== PRIOR DAY SWEEP ==============================
        // Yesterday's high and low are the levels everyone else is watching.
        // Taking one and closing back is a stop run, not a breakout.
        [Parameter("Prior Day Sweep Signals", Group = "Prior Day Sweep", DefaultValue = false)]
        public bool DaySweepOn { get; set; }

        [Parameter("Min Pierce (ATR)", Group = "Prior Day Sweep", DefaultValue = 0.15, MinValue = 0.00, MaxValue = 3.00)]
        public double SweepMinPierce { get; set; }

        [Parameter("Max Pierce (ATR)", Group = "Prior Day Sweep", DefaultValue = 1.50, MinValue = 0.20, MaxValue = 8.00)]
        public double SweepMaxPierce { get; set; }

        [Parameter("Min Reclaim Bar (ATR)", Group = "Prior Day Sweep", DefaultValue = 0.60, MinValue = 0.00, MaxValue = 5.00)]
        public double SweepMinBar { get; set; }

        [Parameter("Show Prior Day Levels", Group = "Prior Day Sweep", DefaultValue = true)]
        public bool ShowPdLevels { get; set; }

        [Parameter("Prior Day Colour", Group = "Prior Day Sweep", DefaultValue = "SteelBlue")]
        public Color PdColor { get; set; }

        // ===================== TREND PULLBACK ===============================
        // Impulse, pullback onto a level, impulse again. A different pattern
        // from the other five, so it finds setups they structurally cannot.
                // BreakPrevHigh entered after a bar that had already travelled away
        // from the pullback, so risk was the whole retracement depth: 1.3 to
        // 2.5 ATR on a 4 ATR leg, putting 3R four to seven ATR away.
        // LevelReclaim enters on the bar that tags the pullback level and
        // closes back with trend, so risk is one bar plus the buffer.
        [Parameter("TPB Trigger", Group = "Trend Pullback", DefaultValue = TpbTrig.LevelReclaim)]
        public TpbTrig TpbTrigMode { get; set; }

        [Parameter("Trend Pullback Signals", Group = "Trend Pullback", DefaultValue = true)]
        public bool TpbDraws { get; set; }

        [Parameter("Trend Fast EMA", Group = "Trend Pullback", DefaultValue = 21, MinValue = 3, MaxValue = 200)]
        public int TrendFast { get; set; }

        [Parameter("Trend Slow EMA", Group = "Trend Pullback", DefaultValue = 55, MinValue = 5, MaxValue = 400)]
        public int TrendSlow { get; set; }

        [Parameter("Impulse Lookback", Group = "Trend Pullback", DefaultValue = 30, MinValue = 6, MaxValue = 200)]
        public int ImpulseLookback { get; set; }

        [Parameter("Min Impulse (ATR)", Group = "Trend Pullback", DefaultValue = 1.80, MinValue = 0.50, MaxValue = 15.00)]
        public double ImpulseMinAtr { get; set; }

        [Parameter("Pullback Depth Min %", Group = "Trend Pullback", DefaultValue = 33, MinValue = 5, MaxValue = 60)]
        public int PullDepthMin { get; set; }

        [Parameter("Pullback Depth Max %", Group = "Trend Pullback", DefaultValue = 62, MinValue = 30, MaxValue = 95)]
        public int PullDepthMax { get; set; }

        // The same ten stage shape that took Zone retest from +0.12 to
        // +0.78 R per trade.
        [Parameter("TPB One Signal Per Leg", Group = "Trend Pullback", DefaultValue = true)]
        public bool TpbOnePerLeg { get; set; }

        [Parameter("TPB Needs Break Of Structure", Group = "Trend Pullback", DefaultValue = true)]
        public bool TpbNeedsBos { get; set; }

        [Parameter("TPB Sweep Gate", Group = "Trend Pullback", DefaultValue = true)]
        public bool TpbSweepGate { get; set; }

        [Parameter("TPB Needs Imbalance", Group = "Trend Pullback", DefaultValue = true)]
        public bool TpbNeedsGap { get; set; }

        [Parameter("TPB Trigger ATR", Group = "Trend Pullback", DefaultValue = 0.55, MinValue = 0.00, MaxValue = 3.00)]
        public double TpbTrigGate { get; set; }

        [Parameter("TPB Stop Buffer ATR", Group = "Trend Pullback", DefaultValue = 0.20, MinValue = 0.00, MaxValue = 2.00)]
        public double TpbStopBuf { get; set; }

        [Parameter("TPB Entry Price", Group = "Trend Pullback", DefaultValue = TpbEntryAt.TriggerLevel)]
        public TpbEntryAt TpbEntryMode { get; set; }

        [Parameter("TPB Min Score", Group = "Trend Pullback", DefaultValue = 0, MinValue = 0, MaxValue = 100)]
        public int TpbMinScore { get; set; }

        // Off means Trend pullback never reads Zone retest's zones, so a
        // change to that engine cannot move this one.
        [Parameter("TPB Use Zones As Levels", Group = "Trend Pullback", DefaultValue = false)]
        public bool TpbUseZones { get; set; }

        [Parameter("Require Level Touch", Group = "Trend Pullback", DefaultValue = true)]
        public bool RequireLevelTouch { get; set; }

        [Parameter("Level Tolerance (ATR)", Group = "Trend Pullback", DefaultValue = 0.60, MinValue = 0.05, MaxValue = 3.00)]
        public double LevelTolAtr { get; set; }

        // ===================== HIGHER TIMEFRAME BIAS ========================
        // Structure is nested. A leg on the daily is a trend on h1 and a full
        // impulse on m15. A signal that fights the higher timeframe is greyed
        // out rather than treated as equal to one that agrees with it.
        [Parameter("Use Higher Timeframe Bias", Group = "HTF Bias", DefaultValue = false)]
        public bool UseHtfBias { get; set; }

        [Parameter("Bias TF 1", Group = "HTF Bias", DefaultValue = "Hour4")]
        public TimeFrame BiasTf1 { get; set; }

        [Parameter("Bias TF 2", Group = "HTF Bias", DefaultValue = "Daily")]
        public TimeFrame BiasTf2 { get; set; }

        [Parameter("Bias EMA", Group = "HTF Bias", DefaultValue = 21, MinValue = 3, MaxValue = 200)]
        public int BiasEma { get; set; }

        [Parameter("Bias Slope Lookback", Group = "HTF Bias", DefaultValue = 3, MinValue = 1, MaxValue = 50)]
        public int BiasSlope { get; set; }

        [Parameter("Require Both Timeframes", Group = "HTF Bias", DefaultValue = false)]
        public bool BiasRequireBoth { get; set; }

        [Parameter("Bias Signal Mode", Group = "HTF Bias", DefaultValue = ConsoMode.Allow)]
        public ConsoMode BiasMode { get; set; }

        [Parameter("Show Bias On Panel", Group = "HTF Bias", DefaultValue = true)]
        public bool ShowBias { get; set; }

        // ===================== CONSOLIDATION ================================
        // Price going nowhere. Signals whose entry sits inside the range are
        // refused. A signal that breaks outside it is allowed through.
        [Parameter("Range Filter", Group = "Consolidation", DefaultValue = false)]
        public bool EnableRangeFilter { get; set; }

        // Pre-expansion is the strongest engine and it fires on the break out
        // of compression by definition, so no filter is allowed to touch it.
        [Parameter("Exempt Pre-Expansion From Filters", Group = "Consolidation", DefaultValue = true)]
        public bool ExemptPreExpansion { get; set; }

        // MarkGrey keeps the signal on the chart in grey and reports it in its
        // own table, out of the headline numbers. Block refuses it outright.
        [Parameter("Range Signal Mode", Group = "Consolidation", DefaultValue = ConsoMode.Allow)]
        public ConsoMode RangeSignalMode { get; set; }

        [Parameter("Grey Signal Colour", Group = "Consolidation", DefaultValue = "Gray")]
        public Color GreyColor { get; set; }

        [Parameter("Consolidation Lookback", Group = "Consolidation", DefaultValue = 20, MinValue = 5, MaxValue = 200)]
        public int InConsoLookback { get; set; }

        // InternalStructure reads this timeframe's own swings: a range exists
        // while highs stop rising and lows stop falling, and it stays anchored
        // until structure breaks. Nothing about it depends on a higher chart.
        [Parameter("Range Method", Group = "Consolidation", DefaultValue = RangeDetect.InternalStructure)]
        public RangeDetect RangeMethod { get; set; }

        [Parameter("Range Pivot Strength", Group = "Consolidation", DefaultValue = 2, MinValue = 1, MaxValue = 6)]
        public int RangePivot { get; set; }

        [Parameter("Structural Tolerance (ATR)", Group = "Consolidation", DefaultValue = 0.30, MinValue = 0.05, MaxValue = 2.00)]
        public double StructTolAtr { get; set; }

        [Parameter("Range Break Buffer (ATR)", Group = "Consolidation", DefaultValue = 0.15, MinValue = 0.00, MaxValue = 2.00)]
        public double RangeBreakAtr { get; set; }

        [Parameter("Range Min Bars", Group = "Consolidation", DefaultValue = 8, MinValue = 2, MaxValue = 200)]
        public int RangeMinBars { get; set; }

        [Parameter("Max Range vs ATR", Group = "Consolidation", DefaultValue = 2.20, MinValue = 0.50, MaxValue = 10.00)]
        public double ConsoMaxRangeAtr { get; set; }

        [Parameter("Max Efficiency", Group = "Consolidation", DefaultValue = 0.30, MinValue = 0.05, MaxValue = 0.90)]
        public double ConsoMaxEff { get; set; }

        // A range is equal highs and equal lows. Two swing highs within
        // tolerance of each other and two swing lows likewise is enough.
        [Parameter("Use Equal Highs / Lows", Group = "Consolidation", DefaultValue = true)]
        public bool UseEqualLevels { get; set; }

        [Parameter("Equal Level Tolerance (ATR)", Group = "Consolidation", DefaultValue = 0.35, MinValue = 0.05, MaxValue = 2.00)]
        public double EqualTolAtr { get; set; }

        [Parameter("Min Equal Touches", Group = "Consolidation", DefaultValue = 2, MinValue = 2, MaxValue = 6)]
        public int MinEqualTouches { get; set; }

        [Parameter("Equal Level Pivot Strength", Group = "Consolidation", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int EqualPivot { get; set; }

        [Parameter("Edge Tolerance (ATR)", Group = "Consolidation", DefaultValue = 0.10, MinValue = 0.00, MaxValue = 2.00)]
        public double ConsoEdgeTol { get; set; }

        [Parameter("Draw Range Boxes", Group = "Consolidation", DefaultValue = false)]
        public bool DrawRangeBoxes { get; set; }

        [Parameter("Consolidation Colour", Group = "Consolidation", DefaultValue = "SlateGray")]
        public Color ConsoColor { get; set; }

        [Parameter("Consolidation Opacity", Group = "Consolidation", DefaultValue = 0.10, MinValue = 0.00, MaxValue = 1.00)]
        public double ConsoOpacity { get; set; }

        // ===================== RUNNER =======================================
        // The minimum target is 1:3. Once that is reached the trade is not
        // closed, it trails until structure, momentum or volume give out.
        [Parameter("Let Winners Run", Group = "Runner", DefaultValue = true)]
        public bool LetWinnersRun { get; set; }

        // Scale out. Half the position comes off at the partial, the stop goes
        // to entry, the rest runs to 3R and beyond. A trade that reaches the
        // partial can no longer be a loss, which is what lifts the win rate.
        // The trade-off: blended reward on those trades is under 1:3.
        [Parameter("Use Partial Target", Group = "Runner", DefaultValue = false)]
        public bool UsePartial { get; set; }

        [Parameter("Partial At R", Group = "Runner", DefaultValue = 1.50, MinValue = 0.50, MaxValue = 5.00)]
        public double PartialAtR { get; set; }

        [Parameter("Partial Size %", Group = "Runner", DefaultValue = 50, MinValue = 10, MaxValue = 90)]
        public int PartialPct { get; set; }

        [Parameter("Trail Distance (ATR)", Group = "Runner", DefaultValue = 1.50, MinValue = 0.20, MaxValue = 10.00)]
        public double TrailAtr { get; set; }

        [Parameter("Exit On Momentum Stall", Group = "Runner", DefaultValue = true)]
        public bool ExitOnStall { get; set; }

        [Parameter("Stall Bars", Group = "Runner", DefaultValue = 4, MinValue = 1, MaxValue = 20)]
        public int StallBars { get; set; }

        [Parameter("Stall Body (ATR)", Group = "Runner", DefaultValue = 0.25, MinValue = 0.05, MaxValue = 2.00)]
        public double StallBodyAtr { get; set; }

        [Parameter("Exit On Volume Fade", Group = "Runner", DefaultValue = false)]
        public bool ExitOnVolFade { get; set; }

        [Parameter("Volume Fade Multiple", Group = "Runner", DefaultValue = 0.70, MinValue = 0.10, MaxValue = 1.50)]
        public double VolFadeMult { get; set; }

        [Parameter("Exit On Structure Break", Group = "Runner", DefaultValue = true)]
        public bool ExitOnStructBreak { get; set; }

        [Parameter("Runner Window (days)", Group = "Runner", DefaultValue = 45, MinValue = 1, MaxValue = 400)]
        public int RunnerDays { get; set; }

        // ===================== VISUALS ======================================
        [Parameter("Show Armed Zone", Group = "Visuals", DefaultValue = true)]
        public bool ShowArmZone { get; set; }

        [Parameter("Show Arm Labels", Group = "Visuals", DefaultValue = false)]
        public bool ShowArmLabels { get; set; }

        [Parameter("Recolour Elephant Candles", Group = "Visuals", DefaultValue = true)]
        public bool RecolourElephants { get; set; }

        [Parameter("Recolour Method", Group = "Visuals", DefaultValue = RecolourMode.BarColor)]
        public RecolourMode RecolourMethod { get; set; }

        [Parameter("Elephant Colour Source", Group = "Visuals", DefaultValue = ElephantColourMode.DarkerShadeOfChart)]
        public ElephantColourMode ElephantColourStyle { get; set; }

        [Parameter("Darken %", Group = "Visuals", DefaultValue = 45, MinValue = 5, MaxValue = 90)]
        public int DarkenPct { get; set; }

        [Parameter("Elephant Buy Colour", Group = "Visuals", DefaultValue = "LightSkyBlue")]
        public Color ElephantBuyColor { get; set; }

        [Parameter("Elephant Sell Colour", Group = "Visuals", DefaultValue = "LightPink")]
        public Color ElephantSellColor { get; set; }

        [Parameter("Overlay Body Width %", Group = "Visuals", DefaultValue = 70, MinValue = 20, MaxValue = 100)]
        public int OverlayBodyPct { get; set; }

        [Parameter("Overlay Wick Thickness", Group = "Visuals", DefaultValue = 2, MinValue = 1, MaxValue = 6)]
        public int OverlayWickThickness { get; set; }

        [Parameter("Marker Shape", Group = "Visuals", DefaultValue = MarkerShape.Triangle)]
        public MarkerShape InMarkerShapeStyle { get; set; }

        [Parameter("Marker Render", Group = "Visuals", DefaultValue = MarkerRender.Resizable)]
        public MarkerRender InMarkerRenderMode { get; set; }

        [Parameter("Marker Size", Group = "Visuals", DefaultValue = 10, MinValue = 5, MaxValue = 40)]
        public int InMarkerSize { get; set; }

        [Parameter("Label Style", Group = "Visuals", DefaultValue = LabelStyle.Short)]
        public LabelStyle LabelStyleMode { get; set; }

        [Parameter("Show Engine On Label", Group = "Visuals", DefaultValue = true)]
        public bool ShowEngineOnLabel { get; set; }

        // Pick an engine and it is drawn in its own colour at its own size.
        // Turn on Show Only Highlighted and everything else stops drawing.
        [Parameter("Highlight Engine", Group = "Visuals", DefaultValue = EngineFilter.All)]
        public EngineFilter HighlightEngine { get; set; }

        [Parameter("Show Only Highlighted", Group = "Visuals", DefaultValue = false)]
        public bool ShowOnlyHighlighted { get; set; }

        [Parameter("Highlight Colour", Group = "Visuals", DefaultValue = "Orange")]
        public Color InHighlightColor { get; set; }

        [Parameter("Highlight Marker Size", Group = "Visuals", DefaultValue = 16, MinValue = 5, MaxValue = 40)]
        public int InHighlightSize { get; set; }

        [Parameter("Label Font Size", Group = "Visuals", DefaultValue = 9, MinValue = 5, MaxValue = 24)]
        public int InLabelFontSize { get; set; }

        [Parameter("Marker Offset (ATR)", Group = "Visuals", DefaultValue = 0.35, MinValue = 0.00, MaxValue = 3.00)]
        public double MarkerOffsetAtr { get; set; }

        [Parameter("Label Offset (ATR)", Group = "Visuals", DefaultValue = 0.95, MinValue = 0.00, MaxValue = 5.00)]
        public double LabelOffsetAtr { get; set; }

        // Signals that land close together get pushed out in steps so their
        // labels stack instead of printing on top of each other.
        [Parameter("Stagger Within (bars)", Group = "Visuals", DefaultValue = 5, MinValue = 0, MaxValue = 50)]
        public int StaggerWithin { get; set; }

        [Parameter("Stagger Step (ATR)", Group = "Visuals", DefaultValue = 0.50, MinValue = 0.00, MaxValue = 3.00)]
        public double StaggerStep { get; set; }

        [Parameter("Stagger Tiers", Group = "Visuals", DefaultValue = 3, MinValue = 1, MaxValue = 8)]
        public int StaggerTiers { get; set; }

        // A close-confirmed signal is entered on the next candle's open, so the
        // marker belongs on that candle, not on the one that confirmed it.
        [Parameter("Marker On Entry Candle", Group = "Visuals", DefaultValue = true)]
        public bool MarkerOnEntryCandle { get; set; }

        [Parameter("Buy Colour", Group = "Visuals", DefaultValue = "DeepSkyBlue")]
        public Color InBuyArrowColor { get; set; }

        [Parameter("Sell Colour", Group = "Visuals", DefaultValue = "DeepPink")]
        public Color InSellArrowColor { get; set; }

        [Parameter("Label Colour", Group = "Visuals", DefaultValue = "White")]
        public Color InLabelColor { get; set; }

        [Parameter("Arm Zone Colour", Group = "Visuals", DefaultValue = "MediumPurple")]
        public Color InArmFillColor { get; set; }

        [Parameter("Arm Zone Opacity", Group = "Visuals", DefaultValue = 0.12, MinValue = 0.00, MaxValue = 1.00)]
        public double InArmFillOpacity { get; set; }

        [Parameter("Arm Line Thickness", Group = "Visuals", DefaultValue = 1, MinValue = 1, MaxValue = 5)]
        public int ArmLineThickness { get; set; }

        [Parameter("History Display", Group = "Visuals", DefaultValue = HistoryDisplay.AllHistory)]
        public HistoryDisplay HistoryView { get; set; }

        [Parameter("Days To Show", Group = "Visuals", DefaultValue = 5, MinValue = 1, MaxValue = 365)]
        public int DaysToShow { get; set; }

        // ===================== DASHBOARD ====================================
        [Parameter("Show Dashboard", Group = "Dashboard", DefaultValue = true)]
        public bool ShowPanel { get; set; }

        // Essential is the trading view: what the engines are doing and
        // nothing else. Standard adds today and this week. Full adds the
        // measurement rows used while tuning.
        [Parameter("Dashboard Layout", Group = "Dashboard", DefaultValue = PanelLayout.Standard)]
        public PanelLayout Layout { get; set; }

        [Parameter("Dashboard Detail", Group = "Dashboard", DefaultValue = DashboardDetail.Full)]
        public DashboardDetail InDashDetail { get; set; }

        [Parameter("Panel Font Size", Group = "Dashboard", DefaultValue = 7, MinValue = 5, MaxValue = 16)]
        public int InPanelFontSize { get; set; }

        [Parameter("Panel Text Colour", Group = "Dashboard", DefaultValue = "Black")]
        public Color InPanelColor { get; set; }

        [Parameter("Panel Header Colour", Group = "Dashboard", DefaultValue = "DarkViolet")]
        public Color InHeaderColor { get; set; }

        [Parameter("Panel Background", Group = "Dashboard", DefaultValue = "White")]
        public Color InPanelBackColor { get; set; }

        [Parameter("Panel Opacity", Group = "Dashboard", DefaultValue = 0.95, MinValue = 0.10, MaxValue = 1.00)]
        public double PanelOpacity { get; set; }

        [Parameter("Panel Corner", Group = "Dashboard", DefaultValue = PanelCorner.TopRight)]
        public PanelCorner PanelWhere { get; set; }

        [Parameter("Section Bar Colour", Group = "Dashboard", DefaultValue = "RebeccaPurple")]
        public Color SectionBack { get; set; }

        [Parameter("Section Text Colour", Group = "Dashboard", DefaultValue = "White")]
        public Color SectionText { get; set; }

        [Parameter("Show Greyed Panel", Group = "Dashboard", DefaultValue = false)]
        public bool ShowGreyPanel { get; set; }

        [Parameter("Show Period Engine Table", Group = "Dashboard", DefaultValue = true)]
        public bool ShowPeriodEngines { get; set; }

        [Parameter("Engine Table Period", Group = "Dashboard", DefaultValue = TablePeriod.Today)]
        public TablePeriod EnginePeriod { get; set; }

        // Weekday breakdown. One engine at a time, Monday to Friday, then the
        // week, then the same for every engine combined.
        [Parameter("Show Day Table", Group = "Day Table", DefaultValue = true)]
        public bool ShowDayTable { get; set; }

        [Parameter("Day Table Engine", Group = "Day Table", DefaultValue = EngineFilter.PreExpansion)]
        public EngineFilter DayEngine { get; set; }

        [Parameter("Day Table Corner", Group = "Day Table", DefaultValue = PanelCorner.BottomLeft)]
        public PanelCorner DayWhere { get; set; }

        [Parameter("Greyed Panel Corner", Group = "Dashboard", DefaultValue = PanelCorner.BottomLeft)]
        public PanelCorner GreyPanelWhere { get; set; }

        [Parameter("Panel Positive Colour", Group = "Dashboard", DefaultValue = "SeaGreen")]
        public Color PanelPos { get; set; }

        [Parameter("Panel Negative Colour", Group = "Dashboard", DefaultValue = "Crimson")]
        public Color PanelNeg { get; set; }

        // ===================== SHIPPED DEFAULT RESOLVER =====================
        private bool Ship { get { return UseShipped; } }

        private bool InStats(int src)
        {
            if (src == 2 && !PbInStats) return false;
            if (src == 5 && !TpbInStats) return false;
            if (src == 8 && !FvgInStats) return false;
            if (src == 9 && !SmcInStats) return false;
            return true;
        }

        // Live spread from the attached account. Falls back to the manual
        // value when the feed reports nothing, such as a closed market.
        private double DealCost()
        {
            if (CostFrom == CostSource.Manual) return ManualSpread;
            double sp = Symbol.Spread;
            if (double.IsNaN(sp) || sp <= 0.0) return ManualSpread;
            return sp;
        }
        private int MaxPerDay { get { return InDailyTotal; } }
        private int MinGapBars { get { return InBarGap; } }
        private double TfMinutes() { return BarDuration().TotalMinutes; }

        private int Scaled(int bars)
        {
            if (!ScaleLookbacks) return bars;
            double m = TfMinutes();
            if (m <= 0.0) return bars;
            int v = (int)Math.Round(bars * (RefMinutes / m));
            if (v < 5) v = 5;
            if (v > 5000) v = 5000;
            return v;
        }

        private int RankLookback { get { return Scaled(InRankLookback); } }
        private int CompWindow { get { return InCompWindow; } }   // shape, not context: never scaled
        private int DryUpLookback { get { return Scaled(InDryUpLookback); } }
        private int ProfileLookback { get { return Scaled(InProfileLookback); } }
        private int VolRateLookback { get { return Scaled(InVolRateLookback); } }
        private int EleLookback { get { return Scaled(InEleLookback); } }
        private int ContLookback { get { return Scaled(InContLookback); } }
        private int ConsoLookback { get { return InConsoLookback; } }

        private int DaysToBars(int days)
        {
            double m = TfMinutes();
            if (m <= 0.0) m = 60.0;
            int v = (int)Math.Round(days * 1440.0 / m);
            if (v < 10) v = 10;
            if (v > 20000) v = 20000;
            return v;
        }

        private int TradeWindowBars { get { return DaysToBars(TradeWindowDays); } }
        private int RunnerWindow { get { return DaysToBars(RunnerDays); } }
        private int TightPct { get { return TightPctIn; } }
        private double MaxWindowAtr { get { return InMaxWindowAtr; } }
        private int ArmValidityBars { get { return ArmBars; } }
        private int ScoreFloor { get { return InScoreFloor; } }
        private double VolSurgeMult { get { return InVolSurge; } }
        private double MaxChaseAtr { get { return InMaxChase; } }
        private StopSource StopFrom { get { return InStopFrom; } }
        private int SwingStrength { get { return InSwingStrength; } }
        private int StructureLookback { get { return InStructureLookback; } }
        private double StopBufferAtr { get { return InStopBuf; } }
        private double MinStopAtr { get { return InStopMin; } }
        private double MaxStopAtr { get { return InStopMax; } }
        private bool ShowZones { get { return DrawZoneBoxes; } }
        private DashboardDetail DashDetail
        {
            get { return Layout == PanelLayout.Full ? InDashDetail : DashboardDetail.Compact; }
        }
        private bool ShowPeriodRows { get { return Layout != PanelLayout.Essential; } }
        private double ZoneStopBuffer { get { return InZoneBuf; } }
        private double ZoneMinStopAtr { get { return InZoneMin; } }
        private int PanelFontSize { get { return Ship ? 7 : InPanelFontSize; } }

        // Every visual the chart draws, resolved in one place. With Shipped
        // Visuals on, the look is fixed regardless of what an instance saved.
        private MarkerShape MarkerShapeStyle { get { return Ship ? MarkerShape.Arrow : InMarkerShapeStyle; } }
        private MarkerRender MarkerRenderMode { get { return Ship ? MarkerRender.Resizable : InMarkerRenderMode; } }
        private int MarkerSize { get { return Ship ? 10 : InMarkerSize; } }
        private int LabelFontSize { get { return Ship ? 9 : InLabelFontSize; } }
        private Color BuyArrowColor { get { return Ship ? Color.FromArgb(255, 30, 144, 255) : InBuyArrowColor; } }
        private Color SellArrowColor { get { return Ship ? Color.FromArgb(255, 255, 20, 147) : InSellArrowColor; } }
        private Color LabelColor { get { return Ship ? Color.FromArgb(255, 30, 144, 255) : InLabelColor; } }
        private Color HighlightColor { get { return Ship ? Color.FromArgb(255, 255, 165, 0) : InHighlightColor; } }
        private int HighlightSize { get { return Ship ? 16 : InHighlightSize; } }
        private Color AplusColor { get { return Ship ? Color.FromArgb(255, 255, 0, 255) : InAplusColor; } }
        private int AplusSize { get { return Ship ? 18 : InAplusSize; } }
        private Color ArmFillColor { get { return Ship ? Color.FromArgb(255, 147, 112, 219) : InArmFillColor; } }
        private double ArmFillOpacity { get { return Ship ? 0.12 : InArmFillOpacity; } }
        private Color PanelColor { get { return Ship ? Color.FromArgb(255, 20, 20, 20) : InPanelColor; } }
        private Color HeaderColor { get { return Ship ? Color.FromArgb(255, 124, 58, 237) : InHeaderColor; } }
        private Color PanelBackColor { get { return Ship ? Color.FromArgb(255, 255, 255, 255) : InPanelBackColor; } }

        // ===================== STATE ========================================
        private AverageTrueRange _atr;
        private IndicatorDataSeries _winHigh;
        private IndicatorDataSeries _winLow;
        private IndicatorDataSeries _winRange;

        private int _lastClosed = -1;
        private int _histBars;
        private double _histDays;
        private bool _histShort;
        private DateTime WindowStart()
        {
            if (Bars.Count < 2) return DateTime.MinValue;
            return Bars.OpenTimes[Bars.Count - 1].AddDays(-AnalyseDays - WindowOffsetDays);
        }

        private DateTime WindowEnd()
        {
            if (Bars.Count < 2) return DateTime.MaxValue;
            return Bars.OpenTimes[Bars.Count - 1].AddDays(-WindowOffsetDays);
        }
        private int _lastSignalIndex = -100000;
        private readonly int[] _lastBarBySrc = new int[12];
        private long _barDurTicks;
        private double _reqDist;
        private double _stopOverride = double.NaN;
        private bool _noRefuse;
        private double _sumStopAtr;
        private int _sumStopN;
        private double _minStopOverride = double.NaN;

        private readonly Dictionary<DateTime, int> _dailyCount = new Dictionary<DateTime, int>();
        private readonly Dictionary<DateTime, int> _dailySeq = new Dictionary<DateTime, int>();
        private readonly List<string> _riskIds = new List<string>();

        private bool _chartColoursRead;
        private Color _bullFill, _bearFill, _bullLine, _bearLine;

        private int _statArmed, _statFired, _statElephants, _statAbandoned;
        private int _statBlockedVol, _statBlockedRange, _statSkippedRisk;
        private int _lastBlockVolBar = -1, _lastBlockRangeBar = -1;
        private int _streakDir, _streakCount, _statBlockedStreak;
        private int _signalUid;
        private int _lblBarUp = -100000, _lblBarDn = -100000, _tierUp, _tierDn;
        private int _statBlockedGap, _statBlockedConso, _statBlockedCap;
        private DateTime _capDay = DateTime.MinValue;
        private readonly int[] _capUsed = new int[12];
        private readonly int[] _srcStreakDir = new int[12];
        private readonly int[] _srcStreakCount = new int[12];
        private ExponentialMovingAverage _emaFast, _emaSlow;
        private Bars _biasBarsA, _biasBarsB;
        private ExponentialMovingAverage _biasEmaA, _biasEmaB;
        private int _statBlockedBias, _statBlockedQuality, _statOffTf;
        private readonly List<int> _recBar = new List<int>();
        private readonly List<int> _recSrc = new List<int>();
        private readonly List<int> _recDir = new List<int>();
        private int _fakeBar = -100000;
        private readonly List<int> _phIdx = new List<int>();
        private readonly List<double> _phVal = new List<double>();
        private readonly List<int> _plIdx = new List<int>();
        private readonly List<double> _plVal = new List<double>();
        private int _consoEnd = -100000;
        private double _lastConsoHi, _lastConsoLo;
        private bool _inConso;
        private int _consoStart = -1;
        private double _consoHi, _consoLo;
        private readonly int[] _lastSrcBar = new int[12];
        private int _worstRun, _curRun;
        private double _minR = 0.0, _maxR = 0.0;
        private string _lsId = "";
        private DateTime _lsTime;
        private bool _lsLong;
        private double _lsEntry, _lsStop, _lsTarget, _lsRR;
        private int _lsResult = -1;
        private DateTime _streakDay = DateTime.MinValue;
        private int _lossTight, _lossFlat, _lossTurn;

        private class ArmedZone
        {
            public int Bar;
            public double High, Low, TriggerUp, TriggerDown;
            public int Score, Bias;
            public bool Fired;
        }
        private int _rjFull, _rjSess, _rjTight, _rjAtr, _rjFall, _rjScore, _rjDup, _armOk;
        private int _expExpire, _expQual;
        private int _zDead, _zLife, _zTouch, _zGap, _zNoFire, _zFired, _zStale, _zWide;
        private int _zNoBos, _zNoGap, _zBuilt, _zNoSweep, _zLowScore;
        private int _tDup, _tNoBos, _tNoSweep, _tNoGap, _tWeak, _tLowScore, _tFired, _tScoreSum;
        private int _eNoBos, _eNoGap, _eRun, _eOk, _eExt, _eNoBase, _eSmall;
        private int _tpbLegIdx = -1;
        private bool _tpbLegUp;
        private int _zScoreSum;
        private int _touchNow, _zAgeNow;
        private double _zSizeNow, _eleExtNow, _eleSzNow, _mxNow, _myNow;
        private ArmedZone _arm;
        private readonly List<ArmedZone> _arms = new List<ArmedZone>();

        private class ElephantInfo
        {
            public int Bar;
            public bool Bullish;
            public double High, Low, Range, AtrMult;
            public bool BreakoutFired, PullbackFired;
        }
        private readonly List<ElephantInfo> _elephants = new List<ElephantInfo>();
        private readonly List<ElephantInfo> _conts = new List<ElephantInfo>();

        private class Gap
        {
            public int Bar;
            public bool Demand;
            public double Top, Bottom;
            public bool Dead, InsideLast;
        }
        private readonly List<Gap> _gaps = new List<Gap>();
        private int _fvgBuilt, _fvgFired, _fvgLife, _fvgBroke, _fvgSmall, _fvgWeak;

        // ---- SMC engine state, entirely its own ----
        private double _smSwHi = double.NaN, _smSwLo = double.NaN;
        private int _smSwHiBar, _smSwLoBar;
        private double _smLiveHi = double.NaN, _smLiveLo = double.NaN;
        private int _smDir;
        private readonly List<double> _smKTop = new List<double>();
        private readonly List<double> _smKBot = new List<double>();
        private readonly List<int> _smKDir = new List<int>();
        private readonly List<int> _smKBar = new List<int>();
        private readonly List<double> _smZTop = new List<double>();
        private readonly List<double> _smZBot = new List<double>();
        private readonly List<int> _smZDir = new List<int>();
        private readonly List<int> _smZBar = new List<int>();
        private int _smBos, _smPromoted, _smPurged, _smFired, _smVolSkip, _smPdSkip, _smTall;

        private class Zone
        {
            public int Bar;
            public bool Supply;
            public double Top, Bottom, LegTarget;
            public int Touches, Score;
            public bool Dead;
            public bool InsideLast;
        }
        private readonly List<Zone> _zones = new List<Zone>();

        private class TradeRec
        {
            public int Bar, Src, Seq, Touch, ZAge;
            public double ZSize, EExt, ESz, Mx, My;
            public bool Muted;
            public int Reason;
            public string Id;
            public bool IsLong;
            public double Entry, Stop, Target, Risk, MaxFav, Cost;
            public double InitStop, Overshoot;
            public bool TargetSeen;
            public bool BeMoved, Closed, Watching, Running, PartialHit, CutDone;
            public double Peak;
        }
        private readonly List<TradeRec> _trades = new List<TradeRec>();

        private class Result
        {
            public DateTime Day;
            public int Src, Outcome, Reason, Touch, ZAge;
            public double ZSize, EExt, ESz, Mx, My;
            public bool Muted;
            public double R;
        }
        private readonly List<Result> _log = new List<Result>();

        private const int PanelRows = 50;
        private const int PanelCols = 4;
        private bool _panelBuilt;
        private TextBlock[,] _cells;
        private Grid[,] _bg;
        private const int Panel2Rows = 11;
        private const int Panel3Rows = 19;
        private const int Panel3Cols = 5;
        private bool _panel3Built;
        private TextBlock[,] _cells3;
        private Grid[,] _bg3;
        private bool _panel2Built;
        private TextBlock[,] _cells2;
        private Grid[,] _bg2;

        // ===================== INIT =========================================
        protected override void Initialize()
        {
            int guard = 0;
            while (guard++ < 400)
            {
                int need = PreloadBars;
                if (AnaMode == AnalysisWindow.Days && Bars.Count > 80)
                {
                    // warmup on top of the window itself, so the first bar
                    // inside the window already has full lookbacks behind it
                    int want = DaysToBars(AnalyseDays + WindowOffsetDays) + 1200;
                    if (want > need) need = want;
                }
                if (Bars.Count >= need) break;
                if (Bars.LoadMoreHistory() == 0) break;
            }

            for (int k = 0; k < _lastSrcBar.Length; k++) _lastSrcBar[k] = -1000000;

            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            _emaFast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendFast);
            _emaSlow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendSlow);

            _biasBarsA = MarketData.GetBars(BiasTf1);
            _biasBarsB = MarketData.GetBars(BiasTf2);
            _biasEmaA = Indicators.ExponentialMovingAverage(_biasBarsA.ClosePrices, BiasEma);
            _biasEmaB = Indicators.ExponentialMovingAverage(_biasBarsB.ClosePrices, BiasEma);
            _winHigh = CreateDataSeries();
            _winLow = CreateDataSeries();
            _winRange = CreateDataSeries();
        }

        // ===================== MAIN LOOP ====================================
        public override void Calculate(int index)
        {
            int lastClosed = index - 1;
            while (_lastClosed < lastClosed)
            {
                _lastClosed++;
                ProcessClosedBar(_lastClosed);
            }

            if (IsLastBar)
            {
                _histBars = Bars.Count;
                _histDays = Bars.Count > 1
                    ? (Bars.OpenTimes[Bars.Count - 1] - Bars.OpenTimes[0]).TotalDays : 0.0;
                _histShort = AnaMode == AnalysisWindow.Days &&
                             _histDays < (AnalyseDays + WindowOffsetDays) - 1.0;

                CheckLiveTrigger(index);
                DrawPanel(index);
                DrawConsoPanel();
                DrawDayPanel();
            }
        }

        private void ProcessClosedBar(int i)
        {
            ComputeSeries(i);

            int warmup = Math.Max(AtrPeriod + 2, Math.Max(CompWindow + 5, AtrFallLookback + 2));
            // Always analyse exactly the last PreloadBars bars. LoadMoreHistory
            // returns a different amount each session, which was shifting the
            // window and changing the counts between reloads.
            // Recomputed every bar. Caching it at bar 0 was wrong because the
            // loaded bar count is not final that early, so the cutoff resolved
            // to the start of history and the window never applied.
            if (AnaMode == AnalysisWindow.Days)
            {
                if (Bars.OpenTimes[i] < WindowStart() || Bars.OpenTimes[i] > WindowEnd()) return;
            }
            else if (i < Bars.Count - PreloadBars) return;

            if (i < warmup) return;

            TrackPivots(i);
            UpdateConsolidation(i);
            CheckBarTrigger(i);
            UpdateTrades(i);
            UpdateElephants(i);
            UpdateZones(i);
            CheckTrendPullback(i);
            UpdateFvg(i);
            UpdateSmc(i);
            CheckRangeFakeout(i);
            CheckDaySweep(i);
            UpdateArm(i);
        }

        private void ComputeSeries(int i)
        {
            int start = Math.Max(0, i - CompWindow + 1);
            double hi = Bars.HighPrices[i];
            double lo = Bars.LowPrices[i];
            for (int k = start; k <= i; k++)
            {
                if (Bars.HighPrices[k] > hi) hi = Bars.HighPrices[k];
                if (Bars.LowPrices[k] < lo) lo = Bars.LowPrices[k];
            }
            _winHigh[i] = hi;
            _winLow[i] = lo;
            _winRange[i] = hi - lo;
        }

        // ===================== ARMING =======================================
        // Kaufman efficiency: net distance divided by the path walked. Near
        // zero means price is thrashing and getting nowhere.
        private double Efficiency(int i)
        {
            int start = Math.Max(1, i - ConsoLookback);
            if (i - start < 3) return 1.0;

            double net = Math.Abs(Bars.ClosePrices[i] - Bars.ClosePrices[start]);
            double path = 0.0;
            for (int k = start + 1; k <= i; k++)
                path += Math.Abs(Bars.ClosePrices[k] - Bars.ClosePrices[k - 1]);

            return path <= 0 ? 0.0 : net / path;
        }

        // Only higher timeframe bars that had already closed are read, so a
        // bias can never appear retroactively.
        private int BiasOf(Bars b, ExponentialMovingAverage e, DateTime now)
        {
            if (b == null || e == null || b.Count < BiasEma + BiasSlope + 3) return 0;

            int idx = -1;
            for (int k = b.Count - 1; k >= 0; k--)
            {
                if (b.OpenTimes[k] < now) { idx = k - 1; break; }
            }
            if (idx < BiasSlope + 1) return 0;

            double c = b.ClosePrices[idx];
            double m = e.Result[idx];
            double mPast = e.Result[idx - BiasSlope];
            if (double.IsNaN(m) || double.IsNaN(mPast)) return 0;

            if (c > m && m > mPast) return 1;
            if (c < m && m < mPast) return -1;
            return 0;
        }

        private int HtfBias(DateTime now, out int a, out int b)
        {
            a = BiasOf(_biasBarsA, _biasEmaA, now);
            b = BiasOf(_biasBarsB, _biasEmaB, now);
            if (a == b) return a;
            if (BiasRequireBoth) return 0;
            if (a == 0) return b;
            if (b == 0) return a;
            return 0;   // the two timeframes disagree
        }

        // true when the trade fights the higher timeframe
        private bool AgainstBias(DateTime now, bool isLong)
        {
            if (!UseHtfBias) return false;
            int a, b;
            int bias = HtfBias(now, out a, out b);
            if (BiasRequireBoth && (a != b || a == 0)) return true;
            if (bias == 0) return false;
            return (isLong ? 1 : -1) != bias;
        }

        // 1 trend established, 2 an impulse leg, 3 a pullback into it,
        // 4 the pullback lands on a level, 5 the reclaim bar closes with trend
        // A break that failed. Poke outside the range, close back inside, take
        // the move back across it.
        // Trend, equilibrium and zone confluence. Returns true when the setup
        // fails the shared quality bar.
        // 0 = m5 to m30, 1 = h1 swing, 2 = everything else
        private int TfClass()
        {
            double m = TfMinutes();
            if (m >= 4.5 && m <= 31.0) return 0;
            if (m > 31.0 && m <= 65.0) return 1;
            return 2;
        }

        private bool OffTimeframe()
        {
            if (!PrimaryTfOnly) return false;
            int c = TfClass();
            if (c == 0) return false;
            if (c == 1 && AllowSwingTf) return false;
            return true;
        }

        // How many different engines have called this direction recently.
        private int AgreeingEngines(int i, int dir)
        {
            bool[] seen = new bool[12];
            int n = 0;
            for (int k = 0; k < _recBar.Count; k++)
            {
                if (i - _recBar[k] > ConfluenceWindow) continue;
                if (_recDir[k] != dir) continue;
                int sx = _recSrc[k];
                if (sx < 0 || sx > 11 || seen[sx]) continue;
                seen[sx] = true;
                n++;
            }
            return n;
        }

        private void RememberSignal(int i, int src, int dir)
        {
            _recBar.Add(i); _recSrc.Add(src); _recDir.Add(dir);
            while (_recBar.Count > 200)
            {
                _recBar.RemoveAt(0); _recSrc.RemoveAt(0); _recDir.RemoveAt(0);
            }
        }

        private bool FailsQuality(int i, bool isLong, double entry, int src)
        {
            if (src == 0 && !QualityCoversPreExp) return false;
            if (QualityGate == ConsoMode.Allow) return false;

            if (RequireProTrend)
            {
                double f = _emaFast.Result[i];
                double sw = _emaSlow.Result[i];
                double swPast = _emaSlow.Result[Math.Max(0, i - 5)];
                if (!double.IsNaN(f) && !double.IsNaN(sw) && !double.IsNaN(swPast))
                {
                    bool up = f > sw && sw >= swPast;
                    bool dn = f < sw && sw <= swPast;
                    if (isLong && !up) return true;
                    if (!isLong && !dn) return true;
                }
            }

            if (RequirePremiumDiscount)
            {
                double hi, lo;
                if (DealingRange(out hi, out lo))
                {
                    double eq = lo + (hi - lo) * (EquilibriumPct / 100.0);
                    if (isLong && entry > eq) return true;    // buying at premium
                    if (!isLong && entry < eq) return true;   // selling at discount
                }
            }

            if (RequireZoneConf)
            {
                double atr = _atr.Result[Math.Max(0, i - 1)];
                if (double.IsNaN(atr) || atr <= 0) return false;
                double tol = ZoneConfAtr * atr;

                bool found = false;
                for (int k = _zones.Count - 1; k >= 0; k--)
                {
                    Zone z = _zones[k];
                    if (z.Supply == isLong) continue;       // buys need demand, sells need supply
                    if (entry >= z.Bottom - tol && entry <= z.Top + tol) { found = true; break; }
                }
                if (!found) return true;
            }

            return false;
        }

        // The swing range price is currently working inside.
        private bool DealingRange(out double hi, out double lo)
        {
            hi = 0.0; lo = 0.0;
            int n = _phVal.Count, m = _plVal.Count;
            if (n < 1 || m < 1) return false;

            int takeH = Math.Min(DealingPivots, n);
            int takeL = Math.Min(DealingPivots, m);

            hi = _phVal[n - 1];
            for (int k = n - takeH; k < n; k++) if (_phVal[k] > hi) hi = _phVal[k];

            lo = _plVal[m - 1];
            for (int k = m - takeL; k < m; k++) if (_plVal[k] < lo) lo = _plVal[k];

            return hi > lo;
        }

        // Bullish gap: low of this bar sits above the high two bars back, so
        // the middle bar left unfilled space behind it.
        private void UpdateFvg(int i)
        {
            if (!UseFvg || i < 3) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            for (int k = _gaps.Count - 1; k >= 0; k--)
            {
                Gap g = _gaps[k];

                if (g.Dead) { _gaps.RemoveAt(k); continue; }
                if (i - g.Bar > FvgLife) { _fvgLife++; _gaps.RemoveAt(k); continue; }
                if (i <= g.Bar) continue;

                if (FvgKillOnBreak)
                {
                    if (g.Demand && Bars.ClosePrices[i] < g.Bottom) { g.Dead = true; _fvgBroke++; continue; }
                    if (!g.Demand && Bars.ClosePrices[i] > g.Top) { g.Dead = true; _fvgBroke++; continue; }
                }

                bool inside = Bars.HighPrices[i] >= g.Bottom && Bars.LowPrices[i] <= g.Top;
                if (!inside) { g.InsideLast = false; continue; }
                if (g.InsideLast) continue;
                g.InsideLast = true;

                bool ok = g.Demand
                    ? Bars.ClosePrices[i] > Bars.OpenPrices[i] && Bars.ClosePrices[i] > g.Bottom
                    : Bars.ClosePrices[i] < Bars.OpenPrices[i] && Bars.ClosePrices[i] < g.Top;
                if (!ok) continue;

                _mxNow = (g.Top - g.Bottom) / atr;
                _stopOverride = g.Demand ? g.Bottom - FvgStopBuf * atr : g.Top + FvgStopBuf * atr;
                _minStopOverride = ZoneMinStopAtr;

                if (PlotSignal(i, g.Demand, Bars.ClosePrices[i], "FVG", 0))
                {
                    _fvgFired++;
                    BumpDaily(i);
                    g.Dead = true;              // one trade per imbalance
                }

                _mxNow = 0.0;
                _stopOverride = double.NaN;
                _minStopOverride = double.NaN;
            }

            double up = Bars.LowPrices[i] - Bars.HighPrices[i - 2];
            double dn = Bars.LowPrices[i - 2] - Bars.HighPrices[i];

            bool isUp = up > 0;
            double size = isUp ? up : dn;
            if (size <= 0) return;
            if (size < FvgMinGap * atr) { _fvgSmall++; return; }

            // the middle bar has to be the move that created the gap
            int m = i - 1;
            double mRange = Bars.HighPrices[m] - Bars.LowPrices[m];
            if (mRange <= 0) { _fvgWeak++; return; }
            double mBody = Math.Abs(Bars.ClosePrices[m] - Bars.OpenPrices[m]);

            if (mRange < FvgMinDisp * atr) { _fvgWeak++; return; }
            if (mBody / mRange * 100.0 < FvgMinBodyPct) { _fvgWeak++; return; }

            bool mUp = Bars.ClosePrices[m] > Bars.OpenPrices[m];
            if (mUp != isUp) { _fvgWeak++; return; }

            Gap ng = new Gap
            {
                Bar = i,
                Demand = isUp,
                Top = isUp ? Bars.LowPrices[i] : Bars.LowPrices[i - 2],
                Bottom = isUp ? Bars.HighPrices[i - 2] : Bars.HighPrices[i],
                Dead = false,
                InsideLast = true
            };
            _gaps.Add(ng);
            _fvgBuilt++;
            if (_gaps.Count > 80) _gaps.RemoveAt(0);

            if (DrawFvgBoxes && ShouldDraw(i))
            {
                TimeSpan dur = BarDuration();
                DateTime t0 = Bars.OpenTimes[i];
                Color bc = isUp ? DemandColor : SupplyColor;
                byte al = (byte)Clamp(ZoneOpacity * 255.0, 0.0, 255.0);
                ChartRectangle r = Chart.DrawRectangle("SQXF_" + t0.Ticks.ToString(),
                    t0, ng.Bottom, t0.AddTicks(dur.Ticks * FvgLife), ng.Top,
                    Color.FromArgb(al, bc.R, bc.G, bc.B));
                r.IsFilled = true; r.Thickness = 0; r.IsInteractive = false;
            }
        }

        // ===================== SMC STRUCTURE ZONE ENGINE ====================
        // swings, break of structure with displacement, imbalances left inside
        // the breaking leg, zones held until tapped, entry at the near edge.
        private void UpdateSmc(int i)
        {
            if (!UseSmc || i < SmcSwing * 2 + 4) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            // ---- own pivots, confirmed, never repainting ----
            int k = i - SmcSwing;
            if (k - SmcSwing >= 0)
            {
                bool ph = true, pl = true;
                for (int d = 1; d <= SmcSwing; d++)
                {
                    if (Bars.HighPrices[k] < Bars.HighPrices[k - d] || Bars.HighPrices[k] < Bars.HighPrices[k + d]) ph = false;
                    if (Bars.LowPrices[k] > Bars.LowPrices[k - d] || Bars.LowPrices[k] > Bars.LowPrices[k + d]) pl = false;
                }
                if (ph) { _smSwHi = Bars.HighPrices[k]; _smSwHiBar = k; _smLiveHi = Bars.HighPrices[k]; }
                if (pl) { _smSwLo = Bars.LowPrices[k]; _smSwLoBar = k; _smLiveLo = Bars.LowPrices[k]; }
            }

            // ---- imbalance pool: every three bar gap, held until a break ----
            if (i >= 2)
            {
                if (Bars.LowPrices[i] > Bars.HighPrices[i - 2])
                {
                    _smKTop.Add(Bars.HighPrices[i - 2]);
                    _smKBot.Add(Math.Min(Bars.LowPrices[i - 2], Bars.LowPrices[i - 1]));
                    _smKDir.Add(1); _smKBar.Add(i - 2);
                }
                else if (Bars.HighPrices[i] < Bars.LowPrices[i - 2])
                {
                    _smKTop.Add(Math.Max(Bars.HighPrices[i - 2], Bars.HighPrices[i - 1]));
                    _smKBot.Add(Bars.LowPrices[i]);
                    _smKDir.Add(-1); _smKBar.Add(i - 2);
                }
                while (_smKDir.Count > 300)
                {
                    _smKTop.RemoveAt(0); _smKBot.RemoveAt(0); _smKDir.RemoveAt(0); _smKBar.RemoveAt(0);
                }
            }

            // ---- break of structure, close beyond the swing by a real distance ----
            double push = SmcDisp * atr;
            bool upBos = !double.IsNaN(_smLiveHi) && Bars.ClosePrices[i] > _smLiveHi + push;
            bool dnBos = !double.IsNaN(_smLiveLo) && Bars.ClosePrices[i] < _smLiveLo - push;
            if (upBos) _smLiveHi = double.NaN;
            if (dnBos) _smLiveLo = double.NaN;

            if (upBos || dnBos)
            {
                _smBos++;
                int dir = upBos ? 1 : -1;
                _smDir = dir;

                if (SmcPurge)
                {
                    for (int q = _smZDir.Count - 1; q >= 0; q--)
                        if (_smZDir[q] != dir)
                        {
                            _smZTop.RemoveAt(q); _smZBot.RemoveAt(q);
                            _smZDir.RemoveAt(q); _smZBar.RemoveAt(q);
                            _smPurged++;
                        }
                }

                int legStart = upBos ? _smSwLoBar : _smSwHiBar;
                for (int q = 0; q < _smKDir.Count; q++)
                {
                    if (_smKDir[q] != dir || _smKBar[q] < legStart) continue;
                    double h = _smKTop[q] - _smKBot[q];
                    if (h < SmcMinGap * atr) continue;
                    if (SmcMaxZone > 0 && h > SmcMaxZone * atr) { _smTall++; continue; }
                    _smZTop.Add(_smKTop[q]); _smZBot.Add(_smKBot[q]);
                    _smZDir.Add(dir); _smZBar.Add(_smKBar[q]);
                    _smPromoted++;
                }

                _smKTop.Clear(); _smKBot.Clear(); _smKDir.Clear(); _smKBar.Clear();

                while (_smZDir.Count > SmcMaxZones)
                {
                    _smZTop.RemoveAt(0); _smZBot.RemoveAt(0); _smZDir.RemoveAt(0); _smZBar.RemoveAt(0);
                }
            }

            if (SmcZoneAge > 0)
                for (int q = _smZDir.Count - 1; q >= 0; q--)
                    if (i - _smZBar[q] > SmcZoneAge)
                    {
                        _smZTop.RemoveAt(q); _smZBot.RemoveAt(q);
                        _smZDir.RemoveAt(q); _smZBar.RemoveAt(q);
                    }

            if (_smZDir.Count == 0) return;

            // ---- volatility regime: dead markets do not reach 3R ----
            if (SmcVolGate)
            {
                int a0 = Math.Max(1, i - SmcAtrAvgBars);
                double sum = 0.0; int n = 0;
                for (int q = a0; q < i; q++)
                {
                    double v = _atr.Result[q];
                    if (!double.IsNaN(v) && v > 0) { sum += v; n++; }
                }
                if (n > 20 && atr < SmcMinAtrMult * (sum / n)) { _smVolSkip++; return; }
            }

            // ---- the tap: nearest zone wins ----
            int bestD = -1, bestS = -1;
            double bestDTop = double.NaN, bestSBot = double.NaN;
            for (int q = 0; q < _smZDir.Count; q++)
            {
                double zt = _smZTop[q], zb = _smZBot[q];
                if (_smZDir[q] == 1 && Bars.LowPrices[i] <= zt && Bars.ClosePrices[i] > zb)
                {
                    if (SmcTrendGate && _smDir != 1) continue;
                    if (double.IsNaN(bestDTop) || zt > bestDTop) { bestDTop = zt; bestD = q; }
                }
                if (_smZDir[q] == -1 && Bars.HighPrices[i] >= zb && Bars.ClosePrices[i] < zt)
                {
                    if (SmcTrendGate && _smDir != -1) continue;
                    if (double.IsNaN(bestSBot) || zb < bestSBot) { bestSBot = zb; bestS = q; }
                }
            }
            if (bestD < 0 && bestS < 0) return;

            bool useD = bestD >= 0 && (bestS < 0 ||
                        Math.Abs(Bars.ClosePrices[i] - bestDTop) <= Math.Abs(Bars.ClosePrices[i] - bestSBot));
            int hit = useD ? bestD : bestS;

            // ---- premium and discount, from its own swing range ----
            if (SmcPdGate && !double.IsNaN(_smSwHi) && !double.IsNaN(_smSwLo))
            {
                double rHi = Math.Max(_smSwHi, _smSwLo), rLo = Math.Min(_smSwHi, _smSwLo);
                double span = rHi - rLo;
                if (span > 0)
                {
                    double eq = rLo + span * (SmcEqPct / 100.0);
                    if (useD && Bars.ClosePrices[i] > eq) { _smPdSkip++; return; }
                    if (!useD && Bars.ClosePrices[i] < eq) { _smPdSkip++; return; }
                }
            }

            double entry = useD ? _smZTop[hit] : _smZBot[hit];
            _stopOverride = useD ? _smZBot[hit] - SmcStopPad * atr : _smZTop[hit] + SmcStopPad * atr;
            _minStopOverride = ZoneMinStopAtr;

            if (PlotSignal(i, useD, entry, "SMC", 0))
            {
                _smFired++;
                BumpDaily(i);
            }

            _stopOverride = double.NaN;
            _minStopOverride = double.NaN;

            // first touch only, the zone is spent
            _smZTop.RemoveAt(hit); _smZBot.RemoveAt(hit);
            _smZDir.RemoveAt(hit); _smZBar.RemoveAt(hit);
        }

        private void CheckRangeFakeout(int i)
        {
            if (!UseFakeout) return;
            if (i - _fakeBar < FakeValidBars) return;

            // the live range while one is forming, otherwise the last one for
            // a short window after it ended, which is when fakeouts happen
            double boxHi, boxLo;
            if (_inConso) { boxHi = _consoHi; boxLo = _consoLo; }
            else if (i - _consoEnd <= FakeValidBars * 3 && _lastConsoHi > _lastConsoLo)
            { boxHi = _lastConsoHi; boxLo = _lastConsoLo; }
            else return;

            if (TooManyToday(i) || !SpacedEnough(i)) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            double h = Bars.HighPrices[i], l = Bars.LowPrices[i];
            double c = Bars.ClosePrices[i], o = Bars.OpenPrices[i];
            double rng = h - l;
            if (rng <= 0) return;
            double clv = (c - l) / rng;

            // poked above the range and closed back under it
            double up = h - boxHi;
            if (up >= FakeMinPoke * atr && up <= FakeMaxPoke * atr && c < boxHi && c < o &&
                (1.0 - clv) >= FakeReclaimClv)
            {
                _fakeBar = i;
                _stopOverride = h;
                if (PlotSignal(i, false, c, "FBO SELL", 0)) BumpDaily(i);
                _stopOverride = double.NaN;
                return;
            }

            double dn = boxLo - l;
            if (dn >= FakeMinPoke * atr && dn <= FakeMaxPoke * atr && c > boxLo && c > o &&
                clv >= FakeReclaimClv)
            {
                _fakeBar = i;
                _stopOverride = l;
                if (PlotSignal(i, true, c, "FBO BUY", 0)) BumpDaily(i);
                _stopOverride = double.NaN;
            }
        }

        // Yesterday's high and low, taken then reclaimed.
        private void CheckDaySweep(int i)
        {
            if (!DaySweepOn) return;
            if (_biasBarsB == null || _biasBarsB.Count < 3) return;
            if (TooManyToday(i) || !SpacedEnough(i)) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            int d = -1;
            for (int k = _biasBarsB.Count - 1; k >= 0; k--)
                if (_biasBarsB.OpenTimes[k] < Bars.OpenTimes[i]) { d = k - 1; break; }
            if (d < 0) return;

            double pdh = _biasBarsB.HighPrices[d];
            double pdl = _biasBarsB.LowPrices[d];

            if (ShowPdLevels && ShouldDraw(i))
            {
                TimeSpan dur = BarDuration();
                DateTime t1 = Bars.OpenTimes[i].AddTicks(dur.Ticks * 3);
                ChartTrendLine a = Chart.DrawTrendLine("SQXPDH_" + _biasBarsB.OpenTimes[d].Ticks.ToString(),
                    Bars.OpenTimes[Math.Max(0, i - 60)], pdh, t1, pdh, PdColor);
                a.LineStyle = LineStyle.DotsRare; a.Thickness = 1; a.IsInteractive = false;
                ChartTrendLine b = Chart.DrawTrendLine("SQXPDL_" + _biasBarsB.OpenTimes[d].Ticks.ToString(),
                    Bars.OpenTimes[Math.Max(0, i - 60)], pdl, t1, pdl, PdColor);
                b.LineStyle = LineStyle.DotsRare; b.Thickness = 1; b.IsInteractive = false;
            }

            double h = Bars.HighPrices[i], l = Bars.LowPrices[i];
            double c = Bars.ClosePrices[i], o = Bars.OpenPrices[i];
            if (h - l < SweepMinBar * atr) return;

            double under = pdl - l;
            if (under >= SweepMinPierce * atr && under <= SweepMaxPierce * atr && c > pdl && c > o)
            {
                _stopOverride = l;
                if (PlotSignal(i, true, c, "SWP BUY", 0)) BumpDaily(i);
                _stopOverride = double.NaN;
                return;
            }

            double over = h - pdh;
            if (over >= SweepMinPierce * atr && over <= SweepMaxPierce * atr && c < pdh && c < o)
            {
                _stopOverride = h;
                if (PlotSignal(i, false, c, "SWP SELL", 0)) BumpDaily(i);
                _stopOverride = double.NaN;
            }
        }

        // The impulse extreme must have taken out the previous swing in the
        // same direction, otherwise the leg is just a zigzag.
        private bool TpbBos(bool up, int extremeIdx)
        {
            if (!TpbNeedsBos) return true;
            if (up)
            {
                if (_phVal.Count < 2) return false;
                return Bars.HighPrices[extremeIdx] > _phVal[_phVal.Count - 2];
            }
            if (_plVal.Count < 2) return false;
            return Bars.LowPrices[extremeIdx] < _plVal[_plVal.Count - 2];
        }

        // The pullback must have taken out a minor swing made during the leg.
        private bool TpbSwept(bool up, int fromIdx, int i, double pullExtreme)
        {
            if (!TpbSweepGate) return true;
            if (up)
            {
                for (int k = _plIdx.Count - 1; k >= 0; k--)
                {
                    if (_plIdx[k] <= fromIdx || _plIdx[k] >= i) continue;
                    return pullExtreme < _plVal[k];
                }
                return false;
            }
            for (int k = _phIdx.Count - 1; k >= 0; k--)
            {
                if (_phIdx[k] <= fromIdx || _phIdx[k] >= i) continue;
                return pullExtreme > _phVal[k];
            }
            return false;
        }

        // The impulse must have left a gap somewhere inside it.
        private bool TpbGap(bool up, int a, int b)
        {
            if (!TpbNeedsGap) return true;
            for (int k = Math.Max(a + 2, 2); k <= b; k++)
            {
                if (up && Bars.LowPrices[k] > Bars.HighPrices[k - 2]) return true;
                if (!up && Bars.HighPrices[k] < Bars.LowPrices[k - 2]) return true;
            }
            return false;
        }

        private int TpbScore(int i, double leg, double atr, double depth, bool up)
        {
            double barRange = Bars.HighPrices[i] - Bars.LowPrices[i];
            double closePos = barRange <= 0 ? 0.5
                            : (up ? (Bars.ClosePrices[i] - Bars.LowPrices[i]) / barRange
                                  : (Bars.HighPrices[i] - Bars.ClosePrices[i]) / barRange);

            int sc = 0;
            sc += (int)Math.Round(Clamp(leg / atr / 4.0, 0.0, 1.0) * 30.0);
            sc += (int)Math.Round(Clamp(1.0 - Math.Abs(depth - 50.0) / 50.0, 0.0, 1.0) * 25.0);
            sc += (int)Math.Round(Clamp(barRange / atr / 1.5, 0.0, 1.0) * 25.0);
            sc += (int)Math.Round(Clamp(closePos, 0.0, 1.0) * 20.0);
            return sc;
        }

        private void CheckTrendPullback(int i)
        {
            if (!TpbDraws) return;
            if (TooManyToday(i) || !SpacedEnough(i)) return;
            if (i < ImpulseLookback + 5) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            double f = _emaFast.Result[i];
            double sw = _emaSlow.Result[i];
            double swPast = _emaSlow.Result[Math.Max(0, i - 5)];
            if (double.IsNaN(f) || double.IsNaN(sw) || double.IsNaN(swPast)) return;

            bool up = f > sw;
            if (up && sw <= swPast) return;
            if (!up && sw >= swPast) return;

            int start = Math.Max(1, i - ImpulseLookback);

            if (up)
            {
                int lowIdx = start;
                for (int k = start; k <= i; k++)
                    if (Bars.LowPrices[k] < Bars.LowPrices[lowIdx]) lowIdx = k;

                int highIdx = lowIdx;
                for (int k = lowIdx; k <= i; k++)
                    if (Bars.HighPrices[k] > Bars.HighPrices[highIdx]) highIdx = k;

                if (highIdx <= lowIdx || i - highIdx < 1) return;

                double leg = Bars.HighPrices[highIdx] - Bars.LowPrices[lowIdx];
                if (leg < ImpulseMinAtr * atr) return;

                double pullLow = Bars.LowPrices[highIdx];
                for (int k = highIdx; k <= i; k++)
                    if (Bars.LowPrices[k] < pullLow) pullLow = Bars.LowPrices[k];

                double depth = (Bars.HighPrices[highIdx] - pullLow) / leg * 100.0;
                if (depth < PullDepthMin || depth > PullDepthMax) return;
                if (RequireLevelTouch && !OnLevel(i, pullLow, true, atr)) return;

                if (Bars.ClosePrices[i] <= Bars.OpenPrices[i]) return;
                if (TpbTrigMode == TpbTrig.BreakPrevHigh)
                {
                    if (Bars.ClosePrices[i] <= Bars.HighPrices[i - 1]) return;
                }
                else
                {
                    // the bar must have tagged the pullback level itself
                    if (Bars.LowPrices[i] > pullLow + LevelTolAtr * atr) return;
                }

                if (TpbOnePerLeg && _tpbLegUp && _tpbLegIdx == highIdx) { _tDup++; return; }
                if (!TpbBos(true, highIdx)) { _tNoBos++; return; }
                if (!TpbSwept(true, lowIdx, i, pullLow)) { _tNoSweep++; return; }
                if (!TpbGap(true, lowIdx, highIdx)) { _tNoGap++; return; }
                if (Bars.HighPrices[i] - Bars.LowPrices[i] < TpbTrigGate * atr) { _tWeak++; return; }

                int scBuy = TpbScore(i, leg, atr, depth, true);
                if (scBuy < TpbMinScore) { _tLowScore++; return; }

                double entBuy = (Fills == FillModel.LevelPrice && TpbEntryMode == TpbEntryAt.TriggerLevel)
                                ? Bars.HighPrices[i - 1] : Bars.ClosePrices[i];

                _mxNow = depth;
                _myNow = leg / atr;
                _stopOverride = pullLow - TpbStopBuf * atr;
                if (PlotSignal(i, true, entBuy, "TPB BUY", scBuy))
                {
                    _tFired++; _tScoreSum += scBuy;
                    _tpbLegIdx = highIdx; _tpbLegUp = true;
                    BumpDaily(i);
                }
                _stopOverride = double.NaN;
                _mxNow = 0.0; _myNow = 0.0;
            }
            else
            {
                int highIdx = start;
                for (int k = start; k <= i; k++)
                    if (Bars.HighPrices[k] > Bars.HighPrices[highIdx]) highIdx = k;

                int lowIdx = highIdx;
                for (int k = highIdx; k <= i; k++)
                    if (Bars.LowPrices[k] < Bars.LowPrices[lowIdx]) lowIdx = k;

                if (lowIdx <= highIdx || i - lowIdx < 1) return;

                double leg = Bars.HighPrices[highIdx] - Bars.LowPrices[lowIdx];
                if (leg < ImpulseMinAtr * atr) return;

                double pullHigh = Bars.HighPrices[lowIdx];
                for (int k = lowIdx; k <= i; k++)
                    if (Bars.HighPrices[k] > pullHigh) pullHigh = Bars.HighPrices[k];

                double depth = (pullHigh - Bars.LowPrices[lowIdx]) / leg * 100.0;
                if (depth < PullDepthMin || depth > PullDepthMax) return;
                if (RequireLevelTouch && !OnLevel(i, pullHigh, false, atr)) return;

                if (Bars.ClosePrices[i] >= Bars.OpenPrices[i]) return;
                if (TpbTrigMode == TpbTrig.BreakPrevHigh)
                {
                    if (Bars.ClosePrices[i] >= Bars.LowPrices[i - 1]) return;
                }
                else
                {
                    if (Bars.HighPrices[i] < pullHigh - LevelTolAtr * atr) return;
                }

                if (TpbOnePerLeg && !_tpbLegUp && _tpbLegIdx == lowIdx) { _tDup++; return; }
                if (!TpbBos(false, lowIdx)) { _tNoBos++; return; }
                if (!TpbSwept(false, highIdx, i, pullHigh)) { _tNoSweep++; return; }
                if (!TpbGap(false, highIdx, lowIdx)) { _tNoGap++; return; }
                if (Bars.HighPrices[i] - Bars.LowPrices[i] < TpbTrigGate * atr) { _tWeak++; return; }

                int scSell = TpbScore(i, leg, atr, depth, false);
                if (scSell < TpbMinScore) { _tLowScore++; return; }

                double entSell = (Fills == FillModel.LevelPrice && TpbEntryMode == TpbEntryAt.TriggerLevel)
                                 ? Bars.LowPrices[i - 1] : Bars.ClosePrices[i];

                _mxNow = depth;
                _myNow = leg / atr;
                _stopOverride = pullHigh + TpbStopBuf * atr;
                if (PlotSignal(i, false, entSell, "TPB SELL", scSell))
                {
                    _tFired++; _tScoreSum += scSell;
                    _tpbLegIdx = lowIdx; _tpbLegUp = false;
                    BumpDaily(i);
                }
                _stopOverride = double.NaN;
                _mxNow = 0.0; _myNow = 0.0;
            }
        }

        // did the pullback land on something that matters
        private bool OnLevel(int i, double px, bool isLong, double atr)
        {
            double tol = LevelTolAtr * atr;

            double ema = _emaFast.Result[i];
            if (!double.IsNaN(ema) && Math.Abs(px - ema) <= tol) return true;

            if (TpbUseZones)
            {
                for (int k = _zones.Count - 1; k >= 0; k--)
                {
                    Zone z = _zones[k];
                    if (z.Supply == isLong) continue;
                    if (px >= z.Bottom - tol && px <= z.Top + tol) return true;
                }
            }

            double ob = isLong ? RawSwingLow(i) : RawSwingHigh(i);
            if (!double.IsNaN(ob) && Math.Abs(px - ob) <= tol) return true;

            return false;
        }

        // Confirmed swing points on THIS timeframe. A pivot is only known once
        // it has RangePivot bars either side of it, so nothing repaints.
        private void TrackPivots(int i)
        {
            int sN = RangePivot;
            int k = i - sN;
            if (k - sN < 0) return;

            bool ph = true, pl = true;
            for (int d = 1; d <= sN; d++)
            {
                if (Bars.HighPrices[k] < Bars.HighPrices[k - d] || Bars.HighPrices[k] < Bars.HighPrices[k + d]) ph = false;
                if (Bars.LowPrices[k] > Bars.LowPrices[k - d] || Bars.LowPrices[k] > Bars.LowPrices[k + d]) pl = false;
            }

            if (ph)
            {
                _phIdx.Add(k); _phVal.Add(Bars.HighPrices[k]);
                if (_phIdx.Count > 30) { _phIdx.RemoveAt(0); _phVal.RemoveAt(0); }
            }
            if (pl)
            {
                _plIdx.Add(k); _plVal.Add(Bars.LowPrices[k]);
                if (_plIdx.Count > 30) { _plIdx.RemoveAt(0); _plVal.RemoveAt(0); }
            }
        }

        // A range is a structural state, not a window measurement. It begins
        // when the last two swing highs stop rising and the last two swing lows
        // stop falling, and it ends when price closes through the box.
        private void StructuralRange(int i, double atr)
        {
            double tol = StructTolAtr * atr;
            double brk = RangeBreakAtr * atr;

            if (!_inConso)
            {
                int n = _phVal.Count, m = _plVal.Count;
                if (n < 2 || m < 2) return;

                bool noHigherHigh = _phVal[n - 1] <= _phVal[n - 2] + tol;
                bool noLowerLow = _plVal[m - 1] >= _plVal[m - 2] - tol;
                if (!noHigherHigh || !noLowerLow) return;

                _consoHi = Math.Max(_phVal[n - 1], _phVal[n - 2]);
                _consoLo = Math.Min(_plVal[m - 1], _plVal[m - 2]);
                if (_consoHi <= _consoLo) return;

                _consoStart = Math.Min(Math.Min(_phIdx[n - 1], _phIdx[n - 2]),
                                       Math.Min(_plIdx[m - 1], _plIdx[m - 2]));
                _inConso = true;
                return;
            }

            // still ranging: absorb new swings that sit at the same levels
            int a = _phVal.Count, b = _plVal.Count;
            if (a > 0 && _phVal[a - 1] > _consoHi && _phVal[a - 1] <= _consoHi + tol) _consoHi = _phVal[a - 1];
            if (b > 0 && _plVal[b - 1] < _consoLo && _plVal[b - 1] >= _consoLo - tol) _consoLo = _plVal[b - 1];

            bool broken = Bars.ClosePrices[i] > _consoHi + brk || Bars.ClosePrices[i] < _consoLo - brk;
            bool longEnough = i - _consoStart >= RangeMinBars;

            if (broken)
            {
                if (DrawRangeBoxes && longEnough) DrawConso(i);
                _lastConsoHi = _consoHi;
                _lastConsoLo = _consoLo;
                _consoEnd = i;
                _inConso = false;
                _consoStart = -1;
                return;
            }

            if (DrawRangeBoxes && longEnough) DrawConso(i);
        }

        private void UpdateConsolidation(int i)
        {
            if (!EnableRangeFilter && !UseFakeout) { _inConso = false; return; }

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) { _inConso = false; return; }

            if (RangeMethod == RangeDetect.InternalStructure)
            {
                StructuralRange(i, atr);
                return;
            }

            int start = Math.Max(0, i - ConsoLookback + 1);
            double hi = Bars.HighPrices[start];
            double lo = Bars.LowPrices[start];
            for (int k = start + 1; k <= i; k++)
            {
                if (Bars.HighPrices[k] > hi) hi = Bars.HighPrices[k];
                if (Bars.LowPrices[k] < lo) lo = Bars.LowPrices[k];
            }

            bool tight = (hi - lo) <= ConsoMaxRangeAtr * atr;
            bool aimless = Efficiency(i) <= ConsoMaxEff;
            bool equal = UseEqualLevels && EqualLevels(i, hi, lo, atr);
            bool now = equal || (tight && aimless);

            if (now)
            {
                if (!_inConso) { _inConso = true; _consoStart = start; _consoHi = hi; _consoLo = lo; }
                else
                {
                    if (hi > _consoHi) _consoHi = hi;
                    if (lo < _consoLo) _consoLo = lo;
                }

                // drawn while it is forming, not only once it ends, so a range
                // in progress is always visible on every timeframe
                if (DrawRangeBoxes) DrawConso(i);
            }
            else if (_inConso)
            {
                if (DrawRangeBoxes) DrawConso(i);
                _lastConsoHi = _consoHi;
                _lastConsoLo = _consoLo;
                _consoEnd = i;
                _inConso = false;
                _consoStart = -1;
            }
        }

        // Equal highs and equal lows. The moment price puts in a second swing
        // high at the same level and a second swing low at the same level, that
        // is a range, whatever the efficiency reading says.
        private bool EqualLevels(int i, double hi, double lo, double atr)
        {
            double tol = EqualTolAtr * atr;
            int sN = EqualPivot;
            int start = Math.Max(sN, i - ConsoLookback + 1);
            int touchHi = 0, touchLo = 0;

            for (int k = start; k <= i - sN; k++)
            {
                bool ph = true, pl = true;
                for (int d = 1; d <= sN; d++)
                {
                    if (k - d < 0 || k + d > i) { ph = false; pl = false; break; }
                    if (Bars.HighPrices[k] < Bars.HighPrices[k - d] || Bars.HighPrices[k] < Bars.HighPrices[k + d]) ph = false;
                    if (Bars.LowPrices[k] > Bars.LowPrices[k - d] || Bars.LowPrices[k] > Bars.LowPrices[k + d]) pl = false;
                }
                if (ph && Math.Abs(Bars.HighPrices[k] - hi) <= tol) touchHi++;
                if (pl && Math.Abs(Bars.LowPrices[k] - lo) <= tol) touchLo++;
            }

            return touchHi >= MinEqualTouches && touchLo >= MinEqualTouches;
        }

        private void DrawConso(int i)
        {
            if (!ShouldDraw(i)) return;

            byte a = (byte)Clamp(ConsoOpacity * 255.0, 0.0, 255.0);
            Color fill = Color.FromArgb(a, ConsoColor.R, ConsoColor.G, ConsoColor.B);

            ChartRectangle r = Chart.DrawRectangle("SQXC_" + Bars.OpenTimes[_consoStart].Ticks.ToString(),
                Bars.OpenTimes[_consoStart], _consoLo, Bars.OpenTimes[i], _consoHi, fill);
            r.IsFilled = true;
            r.Thickness = 0;
            r.IsInteractive = false;
        }

        // Inside the range, refuse. Breaking out of it, allow.
        private bool InsideConsolidation(int i, double entry)
        {
            if (!EnableRangeFilter || !_inConso) return false;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return false;

            double tol = ConsoEdgeTol * atr;
            return entry < _consoHi - tol && entry > _consoLo + tol;
        }

        private void UpdateArm(int i)
        {
            if (!UsePreExpansion) return;

            for (int k = _arms.Count - 1; k >= 0; k--)
            {
                if (_arms[k].Fired) { _arms.RemoveAt(k); continue; }
                if ((i - _arms[k].Bar) >= ArmValidityBars) { _expExpire++; _arms.RemoveAt(k); }
            }

            if (_arms.Count >= MaxArms) { _rjFull++; return; }

            if (UseSession && !PreExpNeverBlocked && !InWindow(Bars.OpenTimes[i])) { _rjSess++; return; }

            double atr = _atr.Result[i];
            if (double.IsNaN(atr) || atr <= 0) return;

            double range = _winRange[i];
            if (double.IsNaN(range) || range <= 0) return;

            int start = Math.Max(CompWindow, i - RankLookback);
            int n = 0, wider = 0;
            for (int k = start; k < i; k++)
            {
                double r = _winRange[k];
                if (double.IsNaN(r)) continue;
                n++;
                if (r >= range) wider++;
            }
            if (n < 20) return;

            double pctRank = (double)wider / n;
            if (pctRank < 1.0 - (TightPct / 100.0)) { _rjTight++; return; }
            if (range > MaxWindowAtr * atr) { _rjAtr++; return; }

            double atrPast = _atr.Result[Math.Max(0, i - AtrFallLookback)];
            if (double.IsNaN(atrPast) || atrPast <= 0) return;
            double atrRatio = atr / atrPast;
            if (RequireFallingAtr && atrRatio >= 1.0) { _rjFall++; return; }

            int sweepBias = EdgeSweepBias(i);
            if (RequireSweep && sweepBias == 0) return;

            double buffer = TriggerBufferAtr * atr;
            double trigUp = _winHigh[i] + buffer;
            double trigDn = _winLow[i] - buffer;

            double nodeUp = 1.0, nodeDown = 1.0, vpoc = 0.0;
            if (UseProfile) ProfileAt(i, trigUp, trigDn, out nodeUp, out nodeDown, out vpoc);

            int ptsComp = (int)Math.Round(20.0 * pctRank);
            int ptsContract = (int)Math.Round(15.0 * Clamp((1.0 - atrRatio) / 0.5, 0.0, 1.0));
            int ptsBody = (int)Math.Round(10.0 * Clamp(1.0 - (AvgBody(i) / atr), 0.0, 1.0));
            int ptsSweep = sweepBias == 0 ? 0 : 15;

            double clv = (Bars.ClosePrices[i] - _winLow[i]) / Math.Max(1e-12, range);
            int leanBias = clv >= 0.5 ? 1 : -1;
            int ptsLean = (int)Math.Round(10.0 * Clamp(Math.Abs(clv - 0.5) * 2.0, 0.0, 1.0));

            int ptsDry = 0;
            if (UseVolumeDryUp)
                ptsDry = (int)Math.Round(15.0 * Clamp((1.0 - VolumeDryRatio(i)) / 0.4, 0.0, 1.0));

            int ptsNode = 0;
            if (UseProfile)
            {
                double thin = Math.Min(nodeUp, nodeDown);
                ptsNode = (int)Math.Round(15.0 * Clamp((LvnThreshold - thin) / Math.Max(1e-9, LvnThreshold), 0.0, 1.0));
            }

            int score = ptsComp + ptsContract + ptsBody + ptsSweep + ptsLean + ptsDry + ptsNode;
            if (score < ScoreFloor) { _rjScore++; return; }

            double sep = ArmSeparationAtr * atr;
            for (int k = 0; k < _arms.Count; k++)
            {
                if (Math.Abs(_arms[k].TriggerUp - trigUp) < sep &&
                    Math.Abs(_arms[k].TriggerDown - trigDn) < sep) { _rjDup++; return; }
            }

            _arm = new ArmedZone
            {
                Bar = i,
                High = _winHigh[i],
                Low = _winLow[i],
                TriggerUp = trigUp,
                TriggerDown = trigDn,
                Score = score,
                Bias = sweepBias != 0 ? sweepBias : leanBias,
                Fired = false
            };
            _statArmed++;

            _armOk++;
            _arms.Add(_arm);

            if (ShowArmZone) DrawArmZone(i, _arm);
        }

        private double AvgBody(int i)
        {
            int start = Math.Max(0, i - CompWindow + 1);
            double sum = 0.0;
            int n = 0;
            for (int k = start; k <= i; k++) { sum += Math.Abs(Bars.ClosePrices[k] - Bars.OpenPrices[k]); n++; }
            return n == 0 ? 0.0 : sum / n;
        }

        // Reference range excludes the bar under test.
        private int EdgeSweepBias(int i)
        {
            int start = Math.Max(0, i - CompWindow);
            if (i - start < 2) return 0;

            double hi = Bars.HighPrices[start];
            double lo = Bars.LowPrices[start];
            for (int k = start + 1; k <= i - 1; k++)
            {
                if (Bars.HighPrices[k] > hi) hi = Bars.HighPrices[k];
                if (Bars.LowPrices[k] < lo) lo = Bars.LowPrices[k];
            }

            bool sweptHigh = Bars.HighPrices[i] > hi && Bars.ClosePrices[i] < hi;
            bool sweptLow = Bars.LowPrices[i] < lo && Bars.ClosePrices[i] > lo;
            if (sweptHigh && !sweptLow) return -1;
            if (sweptLow && !sweptHigh) return 1;
            return 0;
        }

        private double VolumeDryRatio(int i)
        {
            int cs = Math.Max(0, i - CompWindow + 1);
            double cSum = 0.0; int cN = 0;
            for (int k = cs; k <= i; k++) { cSum += Bars.TickVolumes[k]; cN++; }
            if (cN == 0) return 1.0;

            int bs = Math.Max(0, i - DryUpLookback + 1);
            double bSum = 0.0; int bN = 0;
            for (int k = bs; k <= i; k++) { bSum += Bars.TickVolumes[k]; bN++; }
            if (bN == 0) return 1.0;

            double baseAvg = bSum / bN;
            if (baseAvg <= 0) return 1.0;
            return (cSum / cN) / baseAvg;
        }

        private double VolumeRate(int i, bool forming)
        {
            double full = BarDuration().TotalSeconds;
            if (full <= 0) full = 60.0;
            double secs = full;
            if (forming)
            {
                secs = (Server.Time - Bars.OpenTimes[i]).TotalSeconds;
                if (secs < 1.0) secs = 1.0;
                if (secs > full) secs = full;
            }
            return Bars.TickVolumes[i] / secs;
        }

        // Share of recent bars this bar's volume rate beats. Independent of
        // the feed's absolute tick counts, so it reads the same on any broker.
        private double VolumeRank(int i, bool forming)
        {
            int start = Math.Max(1, i - VolRateLookback);
            if (i - start < 5) return 1.0;

            double d = BarDuration().TotalSeconds;
            if (d <= 0) d = 60.0;

            double now = VolumeRate(i, forming);
            int below = 0, n = 0;
            for (int k = start; k < i; k++)
            {
                if (Bars.TickVolumes[k] / d <= now) below++;
                n++;
            }
            return n == 0 ? 1.0 : (double)below / n;
        }

        private double AvgVolumeRate(int i)
        {
            int start = Math.Max(1, i - VolRateLookback);
            double sum = 0.0; int n = 0;
            double d = BarDuration().TotalSeconds;
            if (d <= 0) d = 60.0;
            for (int k = start; k < i; k++) { sum += Bars.TickVolumes[k] / d; n++; }
            return n == 0 ? 0.0 : sum / n;
        }

        private void ProfileAt(int i, double up, double down, out double sUp, out double sDown, out double vpoc)
        {
            sUp = 1.0; sDown = 1.0; vpoc = 0.0;

            int start = Math.Max(0, i - ProfileLookback + 1);
            if (i - start < 10) return;

            double hi = Bars.HighPrices[start];
            double lo = Bars.LowPrices[start];
            for (int k = start + 1; k <= i; k++)
            {
                if (Bars.HighPrices[k] > hi) hi = Bars.HighPrices[k];
                if (Bars.LowPrices[k] < lo) lo = Bars.LowPrices[k];
            }
            if (hi <= lo) return;

            double bin = (hi - lo) / ProfileBins;
            if (bin <= 0) return;

            double[] bins = new double[ProfileBins];
            for (int k = start; k <= i; k++)
            {
                int b0 = BinOf(Bars.LowPrices[k], lo, bin);
                int b1 = BinOf(Bars.HighPrices[k], lo, bin);
                int span = b1 - b0 + 1;
                if (span < 1) span = 1;
                double per = Bars.TickVolumes[k] / span;
                for (int b = b0; b <= b1; b++) bins[b] += per;
            }

            double max = 0.0; int mi = 0;
            for (int b = 0; b < ProfileBins; b++) if (bins[b] > max) { max = bins[b]; mi = b; }
            if (max <= 0) return;

            vpoc = lo + (mi + 0.5) * bin;
            sUp = bins[BinOf(up, lo, bin)] / max;
            sDown = bins[BinOf(down, lo, bin)] / max;
        }

        private int BinOf(double price, double lo, double bin)
        {
            int b = (int)((price - lo) / bin);
            if (b < 0) b = 0;
            if (b >= ProfileBins) b = ProfileBins - 1;
            return b;
        }

        // ===================== TRIGGERS =====================================
        private void CheckBarTrigger(int i)
        {
            for (int k = 0; k < _arms.Count; k++)
            {
                _arm = _arms[k];
                BarTriggerOne(i);
            }
            _arm = null;                      // no stale box for the other engines
        }

        private void BarTriggerOne(int i)
        {
            if (_arm == null || _arm.Fired) return;
            if (i <= _arm.Bar || i - _arm.Bar > ArmValidityBars) return;

            bool up = Bars.HighPrices[i] >= _arm.TriggerUp;
            bool dn = Bars.LowPrices[i] <= _arm.TriggerDown;
            if (!up && !dn) return;

            bool isLong = (up && dn) ? Bars.ClosePrices[i] >= Bars.OpenPrices[i] : up;
            double level = isLong ? _arm.TriggerUp : _arm.TriggerDown;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (!double.IsNaN(atr) && atr > 0)
            {
                double gap = isLong ? Bars.OpenPrices[i] - level : level - Bars.OpenPrices[i];
                if (gap > MaxChaseAtr * atr) { _arm.Fired = true; _statAbandoned++; return; }
            }

            if (!Confirmed(i, false)) return;
            Fire(i, isLong, level, level);
        }

        private void CheckLiveTrigger(int i)
        {
            for (int k = 0; k < _arms.Count; k++)
            {
                _arm = _arms[k];
                LiveTriggerOne(i);
            }
            _arm = null;
        }

        private void LiveTriggerOne(int i)
        {
            if (_arm == null || _arm.Fired) return;
            if (i <= _arm.Bar || i - _arm.Bar > ArmValidityBars) return;

            double px = Bars.ClosePrices[i];
            bool isLong;
            double level;
            if (px >= _arm.TriggerUp) { isLong = true; level = _arm.TriggerUp; }
            else if (px <= _arm.TriggerDown) { isLong = false; level = _arm.TriggerDown; }
            else return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (!double.IsNaN(atr) && atr > 0 && Math.Abs(px - level) > MaxChaseAtr * atr)
            {
                _arm.Fired = true; _statAbandoned++; return;
            }

            if (!Confirmed(i, true)) return;

            // Preview only. Nothing is logged and the arm is not consumed, so
            // the closed bar decides. Historical bars never used this path, so
            // no measured number changes.
            if (!LivePreview) return;
            ChartText pv = Chart.DrawText("SQXLIVE", isLong ? "\u2191" : "\u2193",
                Bars.OpenTimes[i], isLong ? Bars.LowPrices[i] : Bars.HighPrices[i],
                isLong ? BuyArrowColor : SellArrowColor);
            pv.FontSize = MarkerSize;
            pv.IsInteractive = false;
        }

        // Hard filters. Neither latches the arm.
        private bool Confirmed(int i, bool forming)
        {
            double atr = _atr.Result[Math.Max(0, i - 1)];

            if (RequireExpansion && !double.IsNaN(atr) && atr > 0)
            {
                if (Bars.HighPrices[i] - Bars.LowPrices[i] < MinTriggerRangeAtr * atr)
                {
                    if (_lastBlockRangeBar != i) { _statBlockedRange++; _lastBlockRangeBar = i; }
                    return false;
                }
            }

            if (RequireVolumeSurge)
            {
                bool weak;
                if (VolTestMode == VolTest.Percentile)
                    weak = VolumeRank(i, forming) < VolPct / 100.0;
                else
                {
                    double avg = AvgVolumeRate(i);
                    weak = avg > 0 && VolumeRate(i, forming) < VolSurgeMult * avg;
                }

                if (weak)
                {
                    if (_lastBlockVolBar != i) { _statBlockedVol++; _lastBlockVolBar = i; }
                    return false;
                }
            }

            return true;
        }

        private void Fire(int i, bool isLong, double level, double confirmed)
        {
            _arm.Fired = true;
            if (!PreExpNeverBlocked && (TooManyToday(i) || !SpacedEnough(i))) return;

            double entry = ResolveEntry(i, level, confirmed);
            if (!PlotSignal(i, isLong, entry, "EXP", 0)) return;
            BumpDaily(i);
            _statFired++;
        }

        // ===================== ELEPHANT ENGINE ==============================
        // Close strength is already covered by Min Close Location, so this
        // adds only what was missing: structure, imbalance and de-duplication.
        private bool EleQualifies(int i, ElephantInfo ele)
        {
            if (EleBosGate)
            {
                if (ele.Bullish)
                {
                    if (_phVal.Count == 0) { _eNoBos++; return false; }
                    if (Bars.ClosePrices[i] <= _phVal[_phVal.Count - 1]) { _eNoBos++; return false; }
                }
                else
                {
                    if (_plVal.Count == 0) { _eNoBos++; return false; }
                    if (Bars.ClosePrices[i] >= _plVal[_plVal.Count - 1]) { _eNoBos++; return false; }
                }
            }

            if (EleGapGate && i >= 2)
            {
                bool gap = ele.Bullish ? Bars.LowPrices[i] > Bars.HighPrices[i - 2]
                                       : Bars.HighPrices[i] < Bars.LowPrices[i - 2];
                if (!gap) { _eNoGap++; return false; }
            }

            if (EleRunGate && _elephants.Count > 0 &&
                _elephants[_elephants.Count - 1].Bar >= i - 1) { _eRun++; return false; }

            double eAtr = _atr.Result[Math.Max(0, i - 1)];
            if (!double.IsNaN(eAtr) && eAtr > 0)
            {
                if ((ele.High - ele.Low) / eAtr < EleSigMinSize) { _eSmall++; return false; }

                double mean = _emaSlow.Result[i];
                if (!double.IsNaN(mean) &&
                    Math.Abs(Bars.ClosePrices[i] - mean) / eAtr > EleExtCap) { _eExt++; return false; }

                if (EleNeedsBase)
                {
                    int b0 = Math.Max(0, i - EleBaseBars);
                    double bh = Bars.HighPrices[b0], bl = Bars.LowPrices[b0];
                    for (int k = b0; k < i; k++)
                    {
                        if (Bars.HighPrices[k] > bh) bh = Bars.HighPrices[k];
                        if (Bars.LowPrices[k] < bl) bl = Bars.LowPrices[k];
                    }
                    if ((bh - bl) > EleBaseMax * eAtr) { _eNoBase++; return false; }
                }
            }

            _eOk++;
            return true;
        }

        private void UpdateElephants(int i)
        {
            ElephantInfo ele;
            if (IsElephantBar(i, out ele))
            {
                _elephants.Add(ele);
                TrimElephants(i);
                _statElephants++;

                if (RecolourElephants) RecolourCandle(i, ele.Bullish);

                // gates here only, so BOS and Pullback keep the full list
                if (SignalOnElephantBar && EleQualifies(i, ele) &&
                    !TooManyToday(i) && SpacedEnough(i))
                {
                    double eRg = ele.High - ele.Low;
                    double eA = _atr.Result[Math.Max(0, i - 1)];
                    double eM = _emaSlow.Result[i];
                    _eleExtNow = (double.IsNaN(eA) || eA <= 0 || double.IsNaN(eM))
                                 ? 0.0 : Math.Abs(Bars.ClosePrices[i] - eM) / eA;
                    _eleSzNow = (double.IsNaN(eA) || eA <= 0) ? 0.0 : eRg / eA;
                    if (EleStopWhere == EleStopAt.Quarter)
                        _stopOverride = ele.Bullish ? ele.High - 0.25 * eRg : ele.Low + 0.25 * eRg;
                    else if (EleStopWhere == EleStopAt.Third)
                        _stopOverride = ele.Bullish ? ele.High - 0.33 * eRg : ele.Low + 0.33 * eRg;
                    else if (EleStopWhere == EleStopAt.FarExtreme)
                        _stopOverride = ele.Bullish ? ele.Low : ele.High;
                    else
                        _stopOverride = (ele.High + ele.Low) / 2.0;
                    if (PlotSignal(i, ele.Bullish, Bars.ClosePrices[i], "ELE", 0)) BumpDaily(i);
                    _stopOverride = double.NaN;
                    _eleExtNow = 0.0;
                    _eleSzNow = 0.0;
                }
            }

            // the continuation engines build their own list from their own
            // settings, so nothing in the Elephant group can reach them
            ElephantInfo cb;
            if (IsBigBar(i, ContListMinAtr, ContMinBodyPct, ContMinCloseLoc, ContMaxOppWick,
                         ContTopPct, ContLookback, out cb))
            {
                _conts.Add(cb);
                int back = Math.Max(BreakWindow, PullWindow) + 5;
                int min = Math.Max(0, i - back);
                _conts.RemoveAll(e => e.Bar < min);
            }

            RunContinuations(i);
        }

        private bool IsElephantBar(int i, out ElephantInfo ele)
        {
            return IsBigBar(i, EleAtrMult, EleMinBodyPct, MinCloseLoc, MaxOppWickPct,
                            UseTopPctGate ? EleTopPct : 0, EleLookback, out ele);
        }

        // Same shape test, called once with the Elephant engine's settings and
        // once with the continuation engines' own settings. Neither can move
        // the other.
        private bool IsBigBar(int i, double atrMult, int minBodyPct, double minCloseLoc,
                              int maxOppWickPct, int topPct, int lookback, out ElephantInfo ele)
        {
            ele = null;

            double H = Bars.HighPrices[i], L = Bars.LowPrices[i];
            double O = Bars.OpenPrices[i], C = Bars.ClosePrices[i];
            double range = H - L;
            if (range <= 0) return false;

            double atrPrev = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atrPrev) || atrPrev <= 0) return false;

            bool sizeAtr = range >= atrMult * atrPrev;
            bool sizeTicks = EleMinTicks > 0 && range >= EleMinTicks * Symbol.TickSize;
            if (!sizeAtr && !sizeTicks) return false;

            if (topPct > 0)
            {
                int start = Math.Max(0, i - lookback);
                int n = i - start;
                if (n < 1) return false;
                int le = 0;
                for (int k = start; k < i; k++)
                    if ((Bars.HighPrices[k] - Bars.LowPrices[k]) <= range) le++;
                if ((double)le / n < 1.0 - (topPct / 100.0)) return false;
            }

            if (Math.Abs(C - O) / range * 100.0 < minBodyPct) return false;

            double clv = (C - L) / range;
            double upWick = H - Math.Max(C, O);
            double loWick = Math.Min(C, O) - L;

            bool bull = C >= O && clv >= minCloseLoc && (loWick / range) * 100.0 <= maxOppWickPct;
            bool bear = C < O && clv <= 1.0 - minCloseLoc && (upWick / range) * 100.0 <= maxOppWickPct;
            if (!bull && !bear) return false;

            ele = new ElephantInfo
            {
                Bar = i,
                Bullish = bull,
                High = H,
                Low = L,
                Range = range,
                AtrMult = range / atrPrev,
                BreakoutFired = false,
                PullbackFired = false
            };
            return true;
        }

        // ===================== ZONES ========================================
        private void UpdateZones(int i)
        {
            if (!UseZones) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return;

            // 1. retire, invalidate, then test existing zones
            for (int k = _zones.Count - 1; k >= 0; k--)
            {
                Zone z = _zones[k];

                if (z.Dead || i - z.Bar > ZoneLifeBars)
                {
                    if (z.Dead) _zDead++; else _zLife++;
                    _zones.RemoveAt(k); continue;
                }
                if (i <= z.Bar) continue;

                if (ZoneKillOnBreak)
                {
                    if (z.Supply && Bars.ClosePrices[i] > z.Top) { z.Dead = true; continue; }
                    if (!z.Supply && Bars.ClosePrices[i] < z.Bottom) { z.Dead = true; continue; }
                }

                bool inside = Bars.HighPrices[i] >= z.Bottom && Bars.LowPrices[i] <= z.Top;
                if (!inside) { z.InsideLast = false; continue; }

                if (!z.InsideLast)
                {
                    z.Touches++;
                    z.InsideLast = true;
                }
                if (i - z.Bar > ZoneAgeCap) { _zStale++; continue; }
                if ((z.Top - z.Bottom) / atr > ZoneSizeCap) { _zWide++; continue; }
                if (z.Touches > ZoneMaxTouches) { _zTouch++; continue; }
                if (TooManyToday(i) || !SpacedEnough(i)) { _zGap++; continue; }

                double c = Bars.ClosePrices[i];
                double o = Bars.OpenPrices[i];
                double entryPx = (Fills == FillModel.LevelPrice && ZoneEntryMode == ZoneEntryAt.ZoneEdge)
                                 ? (z.Supply ? z.Bottom : z.Top)
                                 : c;
                bool fire;

                if (ZoneTriggerMode == ZoneTrigger.FirstTouch)
                    fire = true;
                else
                    fire = z.Supply ? (c < z.Bottom && c < o) : (c > z.Top && c > o);

                if (!fire) { _zNoFire++; continue; }

                _touchNow = z.Touches;
                _zAgeNow = i - z.Bar;
                _zSizeNow = (z.Top - z.Bottom) / atr;
                _stopOverride = z.Supply ? z.Top + ZoneStopBuffer * atr : z.Bottom - ZoneStopBuffer * atr;
                _minStopOverride = ZoneMinStopAtr;

                if (PlotSignal(i, !z.Supply, entryPx, "ZONE", z.Score))
                {
                    _zFired++;
                    BumpDaily(i);
                    z.Dead = z.Touches >= ZoneMaxTouches;
                }

                _touchNow = 0;
                _zAgeNow = 0;
                _zSizeNow = 0.0;
                _stopOverride = double.NaN;
                _minStopOverride = double.NaN;
            }

            // 2. did this bar leave a new zone behind?
            double range = Bars.HighPrices[i] - Bars.LowPrices[i];
            if (range < ZoneMinImpulseAtr * atr) return;

            double body = Math.Abs(Bars.ClosePrices[i] - Bars.OpenPrices[i]);
            if (body / range < 0.5) return;

            bool bear = Bars.ClosePrices[i] < Bars.OpenPrices[i];

            // did this impulse actually break structure?
            if (ZoneNeedsBos)
            {
                if (bear)
                {
                    if (_plVal.Count == 0) { _zNoBos++; return; }
                    if (Bars.ClosePrices[i] >= _plVal[_plVal.Count - 1]) { _zNoBos++; return; }
                }
                else
                {
                    if (_phVal.Count == 0) { _zNoBos++; return; }
                    if (Bars.ClosePrices[i] <= _phVal[_phVal.Count - 1]) { _zNoBos++; return; }
                }
            }

            // did it reverse out of liquidity?
            if (ZoneNeedsSweep)
            {
                int from = Math.Max(0, i - ZoneSweepBack);
                bool swept = false;
                if (bear && _phVal.Count >= 2)
                {
                    double prior = _phVal[_phVal.Count - 2];
                    for (int k = from; k <= i; k++)
                        if (Bars.HighPrices[k] > prior) { swept = true; break; }
                }
                else if (!bear && _plVal.Count >= 2)
                {
                    double prior = _plVal[_plVal.Count - 2];
                    for (int k = from; k <= i; k++)
                        if (Bars.LowPrices[k] < prior) { swept = true; break; }
                }
                if (!swept) { _zNoSweep++; return; }
            }

            // did it leave a gap behind it?
            if (ZoneNeedsGap && i >= 2)
            {
                bool gap = bear ? Bars.HighPrices[i] < Bars.LowPrices[i - 2]
                                : Bars.LowPrices[i] > Bars.HighPrices[i - 2];
                if (!gap) { _zNoGap++; return; }
            }

            double top, bottom;

            if (ZoneFrom == ZoneSource.LastOpposingCandle)
            {
                int origin = -1;
                int start = Math.Max(0, i - ZoneOriginBack);
                for (int k = i - 1; k >= start; k--)
                {
                    if (bear && Bars.ClosePrices[k] > Bars.OpenPrices[k]) { origin = k; break; }
                    if (!bear && Bars.ClosePrices[k] < Bars.OpenPrices[k]) { origin = k; break; }
                }

                if (origin >= 0)
                {
                    top = bear ? Bars.HighPrices[origin] : Bars.OpenPrices[origin];
                    bottom = bear ? Bars.OpenPrices[origin] : Bars.LowPrices[origin];
                }
                else
                {
                    top = bear ? Bars.HighPrices[i] : Bars.OpenPrices[i];
                    bottom = bear ? Bars.OpenPrices[i] : Bars.LowPrices[i];
                }
            }
            else
            {
                top = bear ? Bars.HighPrices[i] : Bars.OpenPrices[i];
                bottom = bear ? Bars.OpenPrices[i] : Bars.LowPrices[i];
            }

            // refine the block to the part that carries the imbalance
            if (ZoneRefineMode != ZoneRefine.WholeCandle && i >= 2)
            {
                double rTop = top, rBot = bottom;

                if (ZoneRefineMode == ZoneRefine.Body)
                {
                    rTop = Math.Max(Bars.OpenPrices[i], Bars.ClosePrices[i]);
                    rBot = Math.Min(Bars.OpenPrices[i], Bars.ClosePrices[i]);
                }
                else
                {
                    double gapHi = bear ? Bars.LowPrices[i - 2] : Bars.HighPrices[i - 2];
                    double gapLo = bear ? Bars.HighPrices[i] : Bars.LowPrices[i];
                    if (gapLo > gapHi) { double t = gapHi; gapHi = gapLo; gapLo = t; }

                    double oTop = Math.Min(top, gapHi);
                    double oBot = Math.Max(bottom, gapLo);
                    if (oTop > oBot) { rTop = oTop; rBot = oBot; }
                    else
                    {
                        rTop = Math.Max(Bars.OpenPrices[i], Bars.ClosePrices[i]);
                        rBot = Math.Min(Bars.OpenPrices[i], Bars.ClosePrices[i]);
                    }
                }

                if (rTop > rBot) { top = rTop; bottom = rBot; }
            }

            if (top <= bottom)
            {
                double pad = 0.10 * atr;
                top = top + pad;
                bottom = bottom - pad;
            }

            // how good is this block, 0 to 100
            double impulse = range / atr;
            double gapSize = i >= 2
                ? Math.Abs(bear ? Bars.LowPrices[i - 2] - Bars.HighPrices[i]
                                : Bars.LowPrices[i] - Bars.HighPrices[i - 2]) / atr
                : 0.0;
            double tightness = (top - bottom) / atr;

            int zScore = 0;
            zScore += (int)Math.Round(Clamp(impulse / 3.0, 0.0, 1.0) * 30.0);
            zScore += (int)Math.Round(Clamp(gapSize / 1.0, 0.0, 1.0) * 25.0);
            zScore += (int)Math.Round(Clamp(body / range, 0.0, 1.0) * 20.0);
            zScore += (int)Math.Round(Clamp(1.0 - tightness, 0.0, 1.0) * 25.0);

            if (zScore < ZoneMinScore) { _zLowScore++; return; }

            Zone nz = new Zone
            {
                Score = zScore,
                Bar = i,
                Supply = bear,
                Top = top,
                Bottom = bottom,
                LegTarget = bear ? Bars.LowPrices[i] : Bars.HighPrices[i],
                Touches = 0,
                Dead = false,
                InsideLast = true
            };
            _zBuilt++;
            _zScoreSum += zScore;
            _zones.Add(nz);
            if (_zones.Count > 60) _zones.RemoveAt(0);

            if (ShowZones) DrawZone(i, nz);
        }

        private void DrawZone(int i, Zone z)
        {
            if (!ShouldDraw(i)) return;

            TimeSpan dur = BarDuration();
            DateTime t0 = Bars.OpenTimes[i];
            DateTime t1 = t0.AddTicks(dur.Ticks * ZoneLifeBars);

            Color baseC = z.Supply ? SupplyColor : DemandColor;
            byte a = (byte)Clamp(ZoneOpacity * 255.0, 0.0, 255.0);
            Color fill = Color.FromArgb(a, baseC.R, baseC.G, baseC.B);

            ChartRectangle r = Chart.DrawRectangle("SQXZ_" + t0.Ticks.ToString(), t0, z.Bottom, t1, z.Top, fill);
            r.IsFilled = true;
            r.Thickness = 0;
            r.IsInteractive = false;
        }

        private void TrimElephants(int i)
        {
            int back = Math.Max(BreakWindow, PullWindow) + 5;
            int min = Math.Max(0, i - back);
            _elephants.RemoveAll(e => e.Bar < min);
        }

        private void RunContinuations(int i)
        {
            if (_conts.Count == 0) return;

            double atr = _atr.Result[i];
            if (double.IsNaN(atr) || atr <= 0) return;

            for (int k = _conts.Count - 1; k >= 0; k--)
            {
                ElephantInfo e = _conts[k];
                if (e.Bar >= i) continue;

                int age = i - e.Bar;
                double c = Bars.ClosePrices[i];

                if (UseBreakout && !e.BreakoutFired && age <= BreakWindow &&
                    e.AtrMult >= BosMinSrcAtr &&
                    !TooManyToday(i) && SpacedEnough(i))
                {
                    double buf = BreakBufferAtr * atr;
                    if (e.Bullish && c >= e.High + buf)
                    {
                        e.BreakoutFired = true;
                        if (PlotSignal(i, true, c, "BOS", 0)) BumpDaily(i);
                        continue;
                    }
                    if (!e.Bullish && c <= e.Low - buf)
                    {
                        e.BreakoutFired = true;
                        if (PlotSignal(i, false, c, "BOS", 0)) BumpDaily(i);
                        continue;
                    }
                }

                if (UsePullback && !e.PullbackFired && age <= PullWindow &&
                    e.AtrMult >= PbSrcMinAtr &&
                    !TooManyToday(i) && SpacedEnough(i))
                {
                    double rg = Math.Max(1e-12, e.Range);

                    if (e.Bullish)
                    {
                        double zLo = e.Low + PullLowPct * rg;
                        double zHi = e.Low + PullHighPct * rg;
                        double mid = e.Low + ReentryPct * rg;
                        bool hit = Bars.LowPrices[i] <= zHi && Bars.HighPrices[i] >= zLo;
                        if (hit && c >= mid)
                        {
                            e.PullbackFired = true;
                            _mxNow = e.Range / atr;
                            _myNow = age;
                            if (PbStopFrom == PbStopAt.ZoneEdge) _stopOverride = zLo - PbStopBuf * atr;
                            else if (PbStopFrom == PbStopAt.CandleExtreme) _stopOverride = e.Low - PbStopBuf * atr;
                            if (PlotSignal(i, true, c, "PB", 0)) BumpDaily(i);
                            _stopOverride = double.NaN;
                            _mxNow = 0.0; _myNow = 0.0;
                        }
                    }
                    else
                    {
                        double zHi = e.High - PullLowPct * rg;
                        double zLo = e.High - PullHighPct * rg;
                        double mid = e.High - ReentryPct * rg;
                        bool hit = Bars.HighPrices[i] >= zLo && Bars.LowPrices[i] <= zHi;
                        if (hit && c <= mid)
                        {
                            e.PullbackFired = true;
                            _mxNow = e.Range / atr;
                            _myNow = age;
                            if (PbStopFrom == PbStopAt.ZoneEdge) _stopOverride = zHi + PbStopBuf * atr;
                            else if (PbStopFrom == PbStopAt.CandleExtreme) _stopOverride = e.High + PbStopBuf * atr;
                            if (PlotSignal(i, false, c, "PB", 0)) BumpDaily(i);
                            _stopOverride = double.NaN;
                            _mxNow = 0.0; _myNow = 0.0;
                        }
                    }
                }
            }
        }

        // ===================== SIGNAL =======================================
        private bool PlotSignal(int i, bool isLong, double entry, string tag, int score)
        {
            int dir = isLong ? 1 : -1;
            int srcEarly = SourceOf(tag);

            DateTime bd = Bars.OpenTimes[i].Date;
            if (bd != _capDay)
            {
                _capDay = bd;
                for (int k = 0; k < _capUsed.Length; k++) _capUsed[k] = 0;
                if (ResetStreakDaily)
                {
                    _streakDir = 0; _streakCount = 0; _streakDay = bd;
                    for (int k = 0; k < _srcStreakDir.Length; k++) { _srcStreakDir[k] = 0; _srcStreakCount[k] = 0; }
                }
            }

            bool free = PreExpNeverBlocked && srcEarly == 0;
            bool exempt = free || (ExemptPreExpansion && srcEarly == 0);

            if (!free && _capUsed[srcEarly] >= PerEngineDaily)
            {
                _statBlockedCap++;
                return false;
            }

            bool overStreak = false;
            if (LimitConsecutive && !exempt)
            {
                if (StreakPerEngine)
                    overStreak = dir == _srcStreakDir[srcEarly] && _srcStreakCount[srcEarly] >= MaxConsecutive;
                else
                    overStreak = dir == _streakDir && _streakCount >= MaxConsecutive;

                if (overStreak)
                {
                    _statBlockedStreak++;
                    if (StreakMode == ConsoMode.Block) return false;
                }
            }

            bool inRange = !exempt && InsideConsolidation(i, entry);
            if (inRange)
            {
                _statBlockedConso++;
                if (RangeSignalMode == ConsoMode.Block) return false;
            }

            if (!free && MinGapBars > 0 && i - _lastBarBySrc[srcEarly] < MinGapBars) return false;

            bool offTf = OffTimeframe();
            if (offTf)
            {
                _statOffTf++;
                if (OffTfMode == ConsoMode.Block) return false;
            }

            bool poor = FailsQuality(i, isLong, entry, srcEarly);
            if (poor)
            {
                _statBlockedQuality++;
                if (srcEarly == 0) _expQual++;
                if (QualityGate == ConsoMode.Block) return false;
            }

            bool counter = !exempt && AgainstBias(Bars.OpenTimes[i], isLong);
            if (counter)
            {
                _statBlockedBias++;
                if (BiasMode == ConsoMode.Block) return false;
            }

            // Allow means detect and report but never grey the signal out
            bool mRange = inRange && RangeSignalMode == ConsoMode.MarkGrey;
            bool mStreak = overStreak && StreakMode == ConsoMode.MarkGrey;
            bool mBias = counter && BiasMode == ConsoMode.MarkGrey;

            bool mQual = poor && QualityGate == ConsoMode.MarkGrey;

            bool mOff = offTf && OffTfMode == ConsoMode.MarkGrey;

            // 1 range, 2 streak, 3 higher timeframe, 4 quality, 5 off timeframe
            int reason = mRange ? 1 : (mStreak ? 2 : (mBias ? 3 : (mQual ? 4 : (mOff ? 5 : 0))));
            bool muted = reason != 0;
            if (mQual || mOff) muted = true;

            int src = srcEarly;
            if (!free && MinGapSameEngine > 0 && i - _lastSrcBar[src] < MinGapSameEngine)
            {
                _statBlockedGap++;
                return false;
            }

            _noRefuse = free;

            double stop, target, risk;
            if (!BuildRisk(i, isLong, entry, out stop, out target, out risk))
            {
                _statSkippedRisk++;
                return false;
            }

            if (dir == _streakDir) _streakCount++;
            else { _streakDir = dir; _streakCount = 1; }

            if (dir == _srcStreakDir[src]) _srcStreakCount[src]++;
            else { _srcStreakDir[src] = dir; _srcStreakCount[src] = 1; }

            _capUsed[src]++;
            _lastBarBySrc[src] = i;
            _lastSrcBar[src] = i;

            DateTime day = Bars.OpenTimes[i].Date;
            int seq;
            if (!_dailySeq.TryGetValue(day, out seq)) seq = 0;
            seq++;
            _dailySeq[day] = seq;

            _signalUid++;
            string id = "SQX_" + _signalUid.ToString() + "_" + tag + (isLong ? "_B" : "_S");

            int agree = AgreeingEngines(i, dir) + 1;      // this signal counts too
            bool aPlus = agree >= AplusMinEngines && !muted;
            RememberSignal(i, src, dir);

            bool isHi = HighlightEngine != EngineFilter.All && ((int)HighlightEngine - 1) == src;
            Color col = aPlus ? InAplusColor
                              : (isHi ? InHighlightColor : (muted ? GreyColor : (isLong ? InBuyArrowColor : InSellArrowColor)));

            // EXP fires intrabar on the candle that is already moving, so it
            // stays put. Everything else confirms on a close and is entered on
            // the candle after it.
            bool shift = MarkerOnEntryCandle && (tag == "ZONE" || tag == "PB" || tag == "BOS");
            DateTime mt = shift ? Bars.OpenTimes[i].Add(BarDuration()) : Bars.OpenTimes[i];

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) atr = risk;

            double markerY = isLong ? Bars.LowPrices[i] - MarkerOffsetAtr * atr
                                    : Bars.HighPrices[i] + MarkerOffsetAtr * atr;
            int tier = 0;
            if (StaggerWithin > 0)
            {
                if (isLong)
                {
                    _tierUp = (i - _lblBarUp <= StaggerWithin) ? (_tierUp + 1) % StaggerTiers : 0;
                    _lblBarUp = i;
                    tier = _tierUp;
                }
                else
                {
                    _tierDn = (i - _lblBarDn <= StaggerWithin) ? (_tierDn + 1) % StaggerTiers : 0;
                    _lblBarDn = i;
                    tier = _tierDn;
                }
            }

            double labOff = LabelOffsetAtr + tier * StaggerStep;
            double labelY = isLong ? Bars.LowPrices[i] - labOff * atr
                                   : Bars.HighPrices[i] + labOff * atr;

            bool paint = ShouldDraw(i);
            if (ShowOnlyHighlighted && HighlightEngine != EngineFilter.All && !isHi) paint = false;
            if (AplusOnly && !aPlus) paint = false;

            if (paint && InMarkerRenderMode == MarkerRender.NativeIcon)
            {
                ChartIcon ic = Chart.DrawIcon(id + "_A", NativeIconFor(isLong), mt, markerY, col);
                ic.IsInteractive = false;
            }
            else if (paint)
            {
                ChartText m = Chart.DrawText(id + "_A", GlyphFor(isLong), mt, markerY, col);
                m.FontSize = aPlus ? InAplusSize : (isHi ? InHighlightSize : InMarkerSize);
                m.IsInteractive = false;
            }

            if (ShowRisk) DrawRisk(i, mt, id, seq, entry, stop, target, risk);

            _lsId = id;
            _lsTime = Bars.OpenTimes[i];
            _lsLong = isLong;
            _lsEntry = entry;
            _lsStop = stop;
            _lsTarget = target;
            _lsRR = risk <= 0 ? 0.0 : Math.Abs(target - entry) / risk;
            _lsResult = -1;

            _trades.Add(new TradeRec
            {
                Bar = i,
                Src = src,
                Muted = muted,
                Reason = reason,
                Seq = seq,
                Touch = _touchNow,
                ZAge = _zAgeNow,
                ZSize = _zSizeNow,
                EExt = _eleExtNow,
                ESz = _eleSzNow,
                Mx = _mxNow,
                My = _myNow,
                Id = id,
                IsLong = isLong,
                Entry = entry,
                Stop = stop,
                InitStop = stop,
                Overshoot = 0.0,
                TargetSeen = false,
                Target = target,
                Risk = risk,
                Cost = DealCost(),
                MaxFav = 0.0,
                BeMoved = false,
                CutDone = false,
                PartialHit = false,
                Running = false,
                Peak = 0.0,
                Closed = false,
                Watching = false
            });

            if (!paint || LabelStyleMode == LabelStyle.None) return true;

            string text = LabelStyleMode == LabelStyle.Compact ? "E" + seq.ToString() : "E: " + seq.ToString();
            if (ShowEngineOnLabel) text = text + " " + TagOf(src);
            if (aPlus) text = text + " A+ x" + agree.ToString();

            ChartText t = Chart.DrawText(id + "_T", text, mt, labelY, aPlus ? InAplusColor : (isHi ? InHighlightColor : (muted ? GreyColor : InLabelColor)));
            t.FontSize = InLabelFontSize;
            t.IsInteractive = false;
            return true;
        }

        // ===================== RISK ENGINE ==================================
        private bool BuildRisk(int i, bool isLong, double entry, out double stop, out double target, out double risk)
        {
            stop = 0.0; target = 0.0; risk = 0.0;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) return false;

            _reqDist = double.IsNaN(_stopOverride) ? MinStructDistAtr * atr : 0.0;

            double buffer = StopBufferAtr * atr;
            double wick = WickBuffer(i, isLong) * WickBufferMult;
            if (wick > buffer) buffer = wick;

            double swing = isLong ? SwingLow(i, entry) : SwingHigh(i, entry);
            double block = isLong ? OrderBlockLow(i, entry) : OrderBlockHigh(i, entry);
            double box = double.NaN;
            if (_arm != null && i - _arm.Bar <= ArmValidityBars)
                box = isLong ? _arm.Low : _arm.High;

            double chosen = double.NaN;
            if (StopFrom == StopSource.SwingPoint) chosen = Protective(isLong, entry, swing);
            else if (StopFrom == StopSource.CompressionBox) chosen = Protective(isLong, entry, box);
            else if (StopFrom == StopSource.OrderBlock) chosen = Protective(isLong, entry, block);
            else if (StopFrom == StopSource.Furthest) chosen = PickFurthest(isLong, entry, swing, box, block);
            else chosen = PickNearest(isLong, entry, swing, box, block);

            if (double.IsNaN(chosen)) chosen = PickNearest(isLong, entry, swing, box, block);

            // an elephant candle defines its own level
            if (!double.IsNaN(_stopOverride)) chosen = _stopOverride;

            if (double.IsNaN(chosen))
                chosen = isLong ? entry - MinStopAtr * atr : entry + MinStopAtr * atr;

            stop = isLong ? chosen - buffer : chosen + buffer;
            risk = Math.Abs(entry - stop);

            double minRisk = (double.IsNaN(_minStopOverride) ? MinStopAtr : _minStopOverride) * atr;
            if (risk < minRisk)
            {
                risk = minRisk;
                stop = isLong ? entry - risk : entry + risk;
            }

            if (risk > MaxStopAtr * atr)
            {
                if (!_noRefuse) return false;
                risk = MaxStopAtr * atr;                       // clamp, never refuse
                stop = isLong ? entry - risk : entry + risk;
            }

            double rMult = MinR;

            if (ExtendTpToLiquidity)
            {
                double liq = isLong ? SwingHigh(i, entry) : SwingLow(i, entry);
                if (!double.IsNaN(liq))
                {
                    double liqR = Math.Abs(liq - entry) / Math.Max(1e-12, risk);
                    if (liqR > rMult) rMult = liqR;
                }
            }

            _sumStopAtr += risk / atr;
            _sumStopN++;

            target = isLong ? entry + rMult * risk : entry - rMult * risk;
            return true;
        }

        // Average wick against the trade over the recent window. A stop placed
        // inside this is a stop that gets taken by noise.
        private double WickBuffer(int i, bool isLong)
        {
            int start = Math.Max(0, i - WickLookback + 1);
            double sum = 0.0;
            int n = 0;
            for (int k = start; k <= i; k++)
            {
                double bodyHi = Math.Max(Bars.OpenPrices[k], Bars.ClosePrices[k]);
                double bodyLo = Math.Min(Bars.OpenPrices[k], Bars.ClosePrices[k]);
                double w = isLong ? bodyLo - Bars.LowPrices[k] : Bars.HighPrices[k] - bodyHi;
                if (w > 0) { sum += w; n++; }
            }
            return n == 0 ? 0.0 : sum / n;
        }

        private double Protective(bool isLong, double entry, double level)
        {
            if (double.IsNaN(level)) return double.NaN;
            if (isLong && level > entry - _reqDist) return double.NaN;
            if (!isLong && level < entry + _reqDist) return double.NaN;
            return level;
        }

        private double PickNearest(bool isLong, double entry, double a, double b, double c)
        {
            double best = double.NaN;
            best = Nearer(isLong, entry, best, a);
            best = Nearer(isLong, entry, best, b);
            best = Nearer(isLong, entry, best, c);
            return best;
        }

        private double Nearer(bool isLong, double entry, double cur, double cand)
        {
            double v = Protective(isLong, entry, cand);
            if (double.IsNaN(v)) return cur;
            if (double.IsNaN(cur)) return v;
            if (isLong) return v > cur ? v : cur;
            return v < cur ? v : cur;
        }

        private double PickFurthest(bool isLong, double entry, double a, double b, double c)
        {
            double best = double.NaN;
            best = Farther(isLong, entry, best, a);
            best = Farther(isLong, entry, best, b);
            best = Farther(isLong, entry, best, c);
            return best;
        }

        private double Farther(bool isLong, double entry, double cur, double cand)
        {
            double v = Protective(isLong, entry, cand);
            if (double.IsNaN(v)) return cur;
            if (double.IsNaN(cur)) return v;
            if (isLong) return v < cur ? v : cur;
            return v > cur ? v : cur;
        }

        private double SwingLow(int i, double entry)
        {
            int s = SwingStrength;
            int start = Math.Max(s, i - StructureLookback);
            for (int k = i - s; k >= start; k--)
            {
                bool piv = true;
                for (int d = 1; d <= s; d++)
                {
                    if (k - d < 0 || k + d > i) { piv = false; break; }
                    if (Bars.LowPrices[k] > Bars.LowPrices[k - d] || Bars.LowPrices[k] > Bars.LowPrices[k + d]) { piv = false; break; }
                }
                if (piv && Bars.LowPrices[k] <= entry - _reqDist) return Bars.LowPrices[k];
            }
            return double.NaN;
        }

        private double SwingHigh(int i, double entry)
        {
            int s = SwingStrength;
            int start = Math.Max(s, i - StructureLookback);
            for (int k = i - s; k >= start; k--)
            {
                bool piv = true;
                for (int d = 1; d <= s; d++)
                {
                    if (k - d < 0 || k + d > i) { piv = false; break; }
                    if (Bars.HighPrices[k] < Bars.HighPrices[k - d] || Bars.HighPrices[k] < Bars.HighPrices[k + d]) { piv = false; break; }
                }
                if (piv && Bars.HighPrices[k] >= entry + _reqDist) return Bars.HighPrices[k];
            }
            return double.NaN;
        }

        private double OrderBlockLow(int i, double entry)
        {
            int start = Math.Max(0, i - StructureLookback);
            for (int k = i - 1; k >= start; k--)
                if (Bars.ClosePrices[k] < Bars.OpenPrices[k] && Bars.LowPrices[k] <= entry - _reqDist)
                    return Bars.LowPrices[k];
            return double.NaN;
        }

        private double OrderBlockHigh(int i, double entry)
        {
            int start = Math.Max(0, i - StructureLookback);
            for (int k = i - 1; k >= start; k--)
                if (Bars.ClosePrices[k] > Bars.OpenPrices[k] && Bars.HighPrices[k] >= entry + _reqDist)
                    return Bars.HighPrices[k];
            return double.NaN;
        }

        // ===================== TRADE TRACKING ===============================
        private void UpdateTrades(int i)
        {
            for (int k = _trades.Count - 1; k >= 0; k--)
            {
                TradeRec t = _trades[k];
                if (i <= t.Bar) continue;

                double fav = t.IsLong ? Bars.HighPrices[i] - t.Entry : t.Entry - Bars.LowPrices[i];
                if (fav > t.MaxFav) t.MaxFav = fav;

                if (!t.Closed)
                {
                    // first protection step: cut the risk in half well before
                    // break even can arm
                    if (RiskCutOn && !t.CutDone && !t.BeMoved && t.Risk > 0 &&
                        t.MaxFav >= CutAtR * t.Risk)
                    {
                        double ns = t.IsLong ? t.Entry - CutToR * t.Risk : t.Entry + CutToR * t.Risk;
                        if (t.IsLong ? ns > t.Stop : ns < t.Stop) t.Stop = ns;
                        t.CutDone = true;
                    }

                    if (UsePartial && !t.PartialHit && t.Risk > 0 && t.MaxFav >= PartialAtR * t.Risk)
                    {
                        t.PartialHit = true;
                        t.Stop = t.Entry;
                        t.BeMoved = true;

                        if (ShowRisk)
                        {
                            Chart.RemoveObject(t.Id + "_SL");
                            if (ShouldDraw(i))
                            {
                                ChartText pt = Chart.DrawText(t.Id + "_P",
                                    "\u00B7\u00B7\u00B7 p" + Math.Round(PartialAtR, 1).ToString() + "R " + t.Seq.ToString(),
                                    Bars.OpenTimes[t.Bar], t.Entry + (t.IsLong ? 1 : -1) * PartialAtR * t.Risk, TargetColor);
                                pt.FontSize = RiskLabelSize;
                                pt.IsInteractive = false;
                            }
                        }
                    }

                    if (UseBreakEven && !t.BeMoved && t.Risk > 0 && t.MaxFav >= BreakEvenAtR * t.Risk)
                    {
                        t.Stop = t.Entry;
                        t.BeMoved = true;

                        if (DropStopAtBe && ShowRisk)
                        {
                            Chart.RemoveObject(t.Id + "_SL");
                            if (ShouldDraw(i))
                            {
                                ChartText be = Chart.DrawText(t.Id + "_BE",
                                    "\u00B7\u00B7\u00B7 b= " + t.Seq.ToString(),
                                    Bars.OpenTimes[t.Bar], t.Entry, InLabelColor);
                                be.FontSize = RiskLabelSize;
                                be.IsInteractive = false;
                            }
                        }
                    }

                    double atrNow = _atr.Result[Math.Max(0, i - 1)];
                    if (double.IsNaN(atrNow) || atrNow <= 0) atrNow = t.Risk;

                    if (!t.Running)
                    {
                        bool hitStop = t.IsLong ? Bars.LowPrices[i] <= t.Stop : Bars.HighPrices[i] >= t.Stop;
                        bool hitTgt = t.IsLong ? Bars.HighPrices[i] >= t.Target : Bars.LowPrices[i] <= t.Target;

                        if (hitStop) CloseTrade(i, t, t.BeMoved ? -t.Cost / t.Risk : RAt(t, t.Stop), t.BeMoved ? 2 : 0);
                        else if (hitTgt)
                        {
                            if (!LetWinnersRun)
                            {
                                CloseTrade(i, t, t.Risk <= 0 ? 0.0 : (Math.Abs(t.Target - t.Entry) - t.Cost) / t.Risk, 1);
                            }
                            else
                            {
                                // minimum reward banked, now let it work
                                t.Running = true;
                                t.Peak = t.IsLong ? Bars.HighPrices[i] : Bars.LowPrices[i];
                                t.Stop = t.Target;   // the banked minimum, never given back
                            }
                        }
                        else if (i - t.Bar >= TradeWindowBars)
                        {
                            double move = t.IsLong ? Bars.ClosePrices[i] - t.Entry : t.Entry - Bars.ClosePrices[i];
                            CloseTrade(i, t, t.Risk <= 0 ? 0.0 : (move - t.Cost) / t.Risk, 3);
                        }
                    }
                    else
                    {
                        double ext = t.IsLong ? Bars.HighPrices[i] : Bars.LowPrices[i];
                        if (t.IsLong ? ext > t.Peak : ext < t.Peak) t.Peak = ext;

                        double tr = t.IsLong ? t.Peak - TrailAtr * atrNow : t.Peak + TrailAtr * atrNow;
                        if (t.IsLong && tr < t.Target) tr = t.Target;
                        if (!t.IsLong && tr > t.Target) tr = t.Target;
                        if (t.IsLong ? tr > t.Stop : tr < t.Stop) t.Stop = tr;

                        bool stopped = t.IsLong ? Bars.LowPrices[i] <= t.Stop : Bars.HighPrices[i] >= t.Stop;
                        if (stopped)
                        {
                            CloseTrade(i, t, RAt(t, t.Stop), 1);
                        }
                        else if (Stalled(i, t.IsLong, atrNow))
                        {
                            CloseTrade(i, t, RAt(t, Bars.ClosePrices[i]), 1);
                        }
                        else if (i - t.Bar >= RunnerWindow)
                        {
                            CloseTrade(i, t, RAt(t, Bars.ClosePrices[i]), 1);
                        }
                    }
                }
                else if (t.Watching)
                {
                    // how far past the original stop did price actually run?
                    double past = t.IsLong ? t.InitStop - Bars.LowPrices[i] : Bars.HighPrices[i] - t.InitStop;
                    if (past > t.Overshoot) t.Overshoot = past;

                    if (t.IsLong ? Bars.HighPrices[i] >= t.Target : Bars.LowPrices[i] <= t.Target)
                        t.TargetSeen = true;

                    if (i - t.Bar >= TradeWindowBars)
                    {
                        bool barely = t.Risk > 0 && t.Overshoot <= TightOvershootR * t.Risk;
                        if (t.TargetSeen && barely) _lossTight++;
                        else if (t.Risk <= 0 || t.MaxFav < 0.5 * t.Risk) _lossFlat++;
                        else _lossTurn++;
                        t.Watching = false;
                    }
                }
            }

            _trades.RemoveAll(t => t.Closed && !t.Watching && i - t.Bar > TradeWindowBars + 5);
        }

        private double RAt(TradeRec t, double price)
        {
            if (t.Risk <= 0) return 0.0;
            double move = t.IsLong ? price - t.Entry : t.Entry - price;
            return (move - t.Cost) / t.Risk;      // dealing cost charged on every exit
        }

        // Has the move run out of steam? Structure, momentum or volume.
        private bool Stalled(int i, bool isLong, double atr)
        {
            if (ExitOnStructBreak)
            {
                double lvl = isLong ? RawSwingLow(i) : RawSwingHigh(i);
                if (!double.IsNaN(lvl))
                {
                    if (isLong && Bars.ClosePrices[i] < lvl) return true;
                    if (!isLong && Bars.ClosePrices[i] > lvl) return true;
                }
            }

            if (ExitOnStall)
            {
                int start = Math.Max(0, i - StallBars + 1);
                bool allSmall = true;
                for (int k = start; k <= i; k++)
                {
                    if (Math.Abs(Bars.ClosePrices[k] - Bars.OpenPrices[k]) >= StallBodyAtr * atr) { allSmall = false; break; }
                }
                if (allSmall && i - start + 1 >= StallBars) return true;
            }

            if (ExitOnVolFade)
            {
                double avg = AvgVolumeRate(i);
                if (avg > 0 && VolumeRate(i, false) < VolFadeMult * avg) return true;
            }

            return false;
        }

        private double RawSwingLow(int i)
        {
            int sN = SwingStrength;
            int start = Math.Max(sN, i - StructureLookback);
            for (int k = i - sN; k >= start; k--)
            {
                bool piv = true;
                for (int d = 1; d <= sN; d++)
                {
                    if (k - d < 0 || k + d > i) { piv = false; break; }
                    if (Bars.LowPrices[k] > Bars.LowPrices[k - d] || Bars.LowPrices[k] > Bars.LowPrices[k + d]) { piv = false; break; }
                }
                if (piv) return Bars.LowPrices[k];
            }
            return double.NaN;
        }

        private double RawSwingHigh(int i)
        {
            int sN = SwingStrength;
            int start = Math.Max(sN, i - StructureLookback);
            for (int k = i - sN; k >= start; k--)
            {
                bool piv = true;
                for (int d = 1; d <= sN; d++)
                {
                    if (k - d < 0 || k + d > i) { piv = false; break; }
                    if (Bars.HighPrices[k] < Bars.HighPrices[k - d] || Bars.HighPrices[k] < Bars.HighPrices[k + d]) { piv = false; break; }
                }
                if (piv) return Bars.HighPrices[k];
            }
            return double.NaN;
        }

        // kind: 0 loss, 1 win, 2 break even, 3 expired
        private void CloseTrade(int i, TradeRec t, double r, int kind)
        {
            // a scaled-out trade banks its partial regardless of how the rest ends
            if (UsePartial && t.PartialHit)
            {
                double share = PartialPct / 100.0;
                r = share * PartialAtR + (1.0 - share) * r;
                if (r > 0.0) kind = 1;
                else if (r == 0.0) kind = 2;
            }

            t.Closed = true;
            t.Watching = kind == 0;

            _log.Add(new Result { Day = Bars.OpenTimes[t.Bar].Date, Src = t.Src, Muted = t.Muted, Reason = t.Reason, Outcome = kind, R = r, Touch = t.Touch, ZAge = t.ZAge, ZSize = t.ZSize, EExt = t.EExt, ESz = t.ESz, Mx = t.Mx, My = t.My });
            if (_log.Count > 40000) _log.RemoveRange(0, 2000);   // room for m5 over 180 days

            if (r < _minR) _minR = r;
            if (r > _maxR) _maxR = r;

            if (kind == 0) { _curRun++; if (_curRun > _worstRun) _worstRun = _curRun; }
            else if (kind == 1) _curRun = 0;

            if (t.Id == _lsId) _lsResult = kind;

            if (ShowRisk && kind != 3) DrawOutcome(i, t, kind);
        }

        // ===================== DRAWING ======================================
        private void RecolourCandle(int i, bool bull)
        {
            if (!ShouldDraw(i)) return;

            Color fill, outline;

            if (ElephantColourStyle == ElephantColourMode.DarkerShadeOfChart)
            {
                EnsureChartColours();
                fill = Darken(bull ? _bullFill : _bearFill, DarkenPct);
                outline = Darken(bull ? _bullLine : _bearLine, DarkenPct);
            }
            else
            {
                fill = bull ? ElephantBuyColor : ElephantSellColor;
                outline = fill;
            }

            if (RecolourMethod == RecolourMode.BarColor)
            {
                Chart.SetBarFillColor(i, fill);
                Chart.SetBarOutlineColor(i, outline);
                return;
            }

            DrawCandleOverlay(i, fill);
        }

        private void EnsureChartColours()
        {
            if (_chartColoursRead) return;
            _bullFill = Chart.ColorSettings.BullFillColor;
            _bearFill = Chart.ColorSettings.BearFillColor;
            _bullLine = Chart.ColorSettings.BullOutlineColor;
            _bearLine = Chart.ColorSettings.BearOutlineColor;
            _chartColoursRead = true;
        }

        private Color Darken(Color c, int pct)
        {
            double f = 1.0 - Clamp(pct / 100.0, 0.0, 0.95);
            return Color.FromArgb(c.A, (int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        private void DrawCandleOverlay(int i, Color c)
        {
            DateTime t0 = Bars.OpenTimes[i];
            TimeSpan dur = BarDuration();
            DateTime mid = t0.AddTicks(dur.Ticks / 2);

            long inset = (long)(dur.Ticks * (1.0 - OverlayBodyPct / 100.0) / 2.0);
            DateTime b0 = t0.AddTicks(inset);
            DateTime b1 = t0.AddTicks(dur.Ticks - inset);

            double lo = Math.Min(Bars.OpenPrices[i], Bars.ClosePrices[i]);
            double hi = Math.Max(Bars.OpenPrices[i], Bars.ClosePrices[i]);
            if (hi - lo < Symbol.TickSize) hi = lo + Symbol.TickSize;

            string id = "ELEO_" + t0.Ticks.ToString();

            ChartTrendLine wick = Chart.DrawTrendLine(id + "_W", mid, Bars.LowPrices[i], mid.AddTicks(1), Bars.HighPrices[i], c);
            wick.Thickness = OverlayWickThickness;
            wick.IsInteractive = false;

            ChartRectangle body = Chart.DrawRectangle(id + "_B", b0, lo, b1, hi, c);
            body.IsFilled = true;
            body.Thickness = 0;
            body.IsInteractive = false;
        }

        private void DrawRisk(int i, DateTime mt, string id, int seq, double entry, double stop, double target, double risk)
        {
            if (!ShouldDraw(i)) return;

            double atr = _atr.Result[Math.Max(0, i - 1)];
            if (double.IsNaN(atr) || atr <= 0) atr = risk;
            double off = RiskLabelOffsetAtr * atr;

            // push each label to the far side of its own level, away from price
            bool isLong = target > entry;
            double slY = isLong ? stop - off : stop + off;
            double tpY = isLong ? target + off : target - off;

            ChartText sl = Chart.DrawText(id + "_SL", "\u00B7\u00B7\u00B7 SL " + seq.ToString(),
                mt, slY, StopColor);
            sl.FontSize = RiskLabelSize;
            sl.IsInteractive = false;

            string tp = "\u00B7\u00B7\u00B7 TP " + seq.ToString();
            if (ShowRonTarget && risk > 0)
                tp = tp + "  " + Math.Round(Math.Abs(target - entry) / risk, 1).ToString() + "R" +
                     (LetWinnersRun ? " min" : "");

            ChartText tt = Chart.DrawText(id + "_TP", tp, mt, tpY, TargetColor);
            tt.FontSize = RiskLabelSize;
            tt.IsInteractive = false;

            if (RiskRecentN <= 0) return;

            _riskIds.Add(id);
            while (_riskIds.Count > RiskRecentN)
            {
                string old = _riskIds[0];
                _riskIds.RemoveAt(0);
                Chart.RemoveObject(old + "_SL");
                Chart.RemoveObject(old + "_TP");
            }
        }

        private void DrawOutcome(int i, TradeRec t, int kind)
        {
            if (!ShouldDraw(i)) return;

            string glyph;
            double price;
            Color col;

            if (kind == 1) { glyph = "\u2714"; price = t.Target; col = TargetColor; }
            else if (kind == 2) { glyph = "b="; price = t.Entry; col = InLabelColor; }
            else { glyph = "\u2718"; price = t.Stop; col = StopColor; }

            ChartText m = Chart.DrawText(t.Id + "_O", glyph, Bars.OpenTimes[i], price, col);
            m.FontSize = OutcomeInMarkerSize;
            m.IsInteractive = false;
        }

        private void DrawArmZone(int i, ArmedZone arm)
        {
            if (!ShouldDraw(i)) return;

            TimeSpan dur = BarDuration();
            DateTime t0 = Bars.OpenTimes[Math.Max(0, i - CompWindow + 1)];
            DateTime t1 = Bars.OpenTimes[i].AddTicks(dur.Ticks * ArmValidityBars);

            byte a = (byte)Clamp(InArmFillOpacity * 255.0, 0.0, 255.0);
            Color fill = Color.FromArgb(a, InArmFillColor.R, InArmFillColor.G, InArmFillColor.B);

            string id = "SQXARM_" + Bars.OpenTimes[i].Ticks.ToString();

            ChartRectangle box = Chart.DrawRectangle(id + "_B", t0, arm.Low, t1, arm.High, fill);
            box.IsFilled = true;
            box.Thickness = 0;
            box.IsInteractive = false;

            ChartTrendLine up = Chart.DrawTrendLine(id + "_U", Bars.OpenTimes[i], arm.TriggerUp, t1, arm.TriggerUp, InArmFillColor);
            up.LineStyle = LineStyle.Dots;
            up.Thickness = ArmLineThickness;
            up.IsInteractive = false;

            ChartTrendLine dn = Chart.DrawTrendLine(id + "_D", Bars.OpenTimes[i], arm.TriggerDown, t1, arm.TriggerDown, InArmFillColor);
            dn.LineStyle = LineStyle.Dots;
            dn.Thickness = ArmLineThickness;
            dn.IsInteractive = false;

            if (!ShowArmLabels) return;

            ChartText lab = Chart.DrawText(id + "_L", arm.Score.ToString(), Bars.OpenTimes[i], arm.High, InArmFillColor);
            lab.FontSize = InLabelFontSize;
            lab.IsInteractive = false;
        }

        // ===================== DASHBOARD RENDER =============================
        private void BuildPanel()
        {
            if (_panelBuilt) return;
            _panelBuilt = true;

            _cells = new TextBlock[PanelRows, PanelCols];
            _bg = new Grid[PanelRows, PanelCols];

            Grid outer = new Grid(PanelRows, PanelCols);
            outer.BackgroundColor = PanelBackColor;
            outer.Opacity = PanelOpacity;

            bool left = PanelWhere == PanelCorner.TopLeft || PanelWhere == PanelCorner.BottomLeft;
            bool bottom = PanelWhere == PanelCorner.BottomRight || PanelWhere == PanelCorner.BottomLeft;
            outer.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            outer.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            outer.Margin = 4;

            for (int r = 0; r < PanelRows; r++)
            {
                for (int c = 0; c < PanelCols; c++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = "";
                    tb.FontSize = PanelFontSize;
                    tb.ForegroundColor = PanelColor;
                    tb.Margin = 2;
                    tb.HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    Grid cell = new Grid(1, 1);
                    cell.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    cell.AddChild(tb, 0, 0);

                    _cells[r, c] = tb;
                    _bg[r, c] = cell;
                    outer.AddChild(cell, r, c);
                }
            }

            Chart.AddControl(outer);
        }

        private void Row(int r, string a, string b, string c, string d)
        {
            if (r < 0 || r >= PanelRows) return;
            _cells[r, 0].Text = a;
            _cells[r, 1].Text = b;
            _cells[r, 2].Text = c;
            _cells[r, 3].Text = d;
        }

        private void RowColour(int r, Color col)
        {
            if (r < 0 || r >= PanelRows) return;
            for (int c = 0; c < PanelCols; c++) _cells[r, c].ForegroundColor = col;
        }

        // Full width coloured bar, the way the section headers read on a
        // TradingView table.
        private void SectionRow(int r, string a, string b, string c, string d)
        {
            Row(r, a, b, c, d);
            for (int i2 = 0; i2 < PanelCols; i2++)
            {
                _bg[r, i2].BackgroundColor = SectionBack;
                _cells[r, i2].ForegroundColor = SectionText;
            }
        }

        private void ClearRow(int r)
        {
            Row(r, "", "", "", "");
            for (int i2 = 0; i2 < PanelCols; i2++)
            {
                _bg[r, i2].BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                _cells[r, i2].ForegroundColor = PanelColor;
            }
        }

        // TP hit, SL hit for a full loss, BE stopped at entry for zero.
        // Expired trades are excluded from every count and both rates.
        private void DrawPanel(int i)
        {
            if (!ShowPanel) return;
            BuildPanel();

            DateTime today = Bars.OpenTimes[i].Date;
            int dow = ((int)today.DayOfWeek + 6) % 7;      // Monday = 0
            DateTime weekStart = today.AddDays(-dow);

            int wTp = 0, wSl = 0, wBe = 0;
            double wR = 0.0;
            int dTp = 0, dSl = 0, dBe = 0;
            int aTp = 0, aSl = 0, aBe = 0, aEx = 0;
            double aR = 0.0, dR = 0.0;

            for (int k = 0; k < _log.Count; k++)
            {
                Result r = _log[k];
                if (r.Muted || !InStats(r.Src)) continue;
                aR += r.R;
                if (r.Outcome == 1) aTp++;
                else if (r.Outcome == 2) aBe++;
                else if (r.Outcome == 3) aEx++;
                else aSl++;

                if (r.Day == today)
                {
                    dR += r.R;
                    if (r.Outcome == 1) dTp++;
                    else if (r.Outcome == 2) dBe++;
                    else if (r.Outcome != 3) dSl++;
                }

                if (r.Day >= weekStart && r.Day <= today)
                {
                    wR += r.R;
                    if (r.Outcome == 1) wTp++;
                    else if (r.Outcome == 2) wBe++;
                    else if (r.Outcome != 3) wSl++;
                }
            }

            int aN = aTp + aSl + aBe;
            double days = AnaMode == AnalysisWindow.Days
                ? (double)AnalyseDays
                : (Bars.OpenTimes[i] - Bars.OpenTimes[Math.Max(0, Bars.Count - PreloadBars)]).TotalDays;
            double perDay = days > 1.0 ? aN / days : 0.0;
            double flip = 100.0 / (1.0 + MinR);

            for (int r = 0; r < PanelRows; r++) ClearRow(r);

            int y = 0;
            SectionRow(y, "SUPERQUANTX", "", Symbol.Name, TfLabel()); y++;

            Row(y, "Signals per day", "", "", Math.Round(perDay, 2).ToString()); y++;
            if (ShowPeriodRows)
            {
                Row(y, "Agreeing engines", "buy " + AgreeingEngines(i, 1).ToString(),
                       "sell " + AgreeingEngines(i, -1).ToString(),
                       TfClass() == 0 ? "primary" : (TfClass() == 1 ? "swing" : "off tf")); y++;
            }

            if (ShowBias)
            {
                int ba, bb;
                HtfBias(Bars.OpenTimes[i], out ba, out bb);
                Row(y, "HTF bias", BiasWord(ba), BiasWord(bb), _inConso ? "RANGE" : "");
                RowColour(y, _inConso ? GreyColor : PanelColor); y++;
            }

            if (ShowPeriodRows)
            {
                Row(y, "Today TP/SL/BE", dTp.ToString(), dSl.ToString(), dBe.ToString()); y++;
                Row(y, "Today trades / R", (dTp + dSl + dBe).ToString(), "", Sign(dR) + " R");
                RowColour(y, dR >= 0 ? PanelPos : PanelNeg); y++;

                Row(y, "Week TP/SL/BE", wTp.ToString(), wSl.ToString(), wBe.ToString()); y++;
                Row(y, "Week trades / R", (wTp + wSl + wBe).ToString(), "", Sign(wR) + " R");
                RowColour(y, wR >= 0 ? PanelPos : PanelNeg); y++;
            }
            Row(y, "Win rate", "", Rate(aTp, aSl) + " %", "flip " + Math.Round(flip, 1).ToString() + " %"); y++;
            Row(y, "Safe rate", "", Safe(aTp, aSl, aBe) + " %", ""); y++;
            Row(y, "Average trade", "", "", Sign(aN == 0 ? 0.0 : aR / aN) + " R"); y++;
            Row(y, "Total", "", "", Sign(aR) + " R");
            RowColour(y, aR >= 0 ? PanelPos : PanelNeg); y++;

            Row(y, _histShort ? "HISTORY SHORT" : "History",
                   _histBars.ToString() + " bars",
                   Math.Round(_histDays, 0).ToString() + "d loaded",
                   AnalyseDays.ToString() + "d asked");
            RowColour(y, _histShort ? PanelNeg : PanelColor); y++;

            if (Bars.Count > 1)
            {
                DateTime ws = WindowStart(), we = WindowEnd();
                if (ws < Bars.OpenTimes[0]) ws = Bars.OpenTimes[0];
                if (we > Bars.OpenTimes[Bars.Count - 1]) we = Bars.OpenTimes[Bars.Count - 1];
                Row(y, "Window", ws.ToString("dd MMM yy"), we.ToString("dd MMM yy"),
                       Math.Round((we - ws).TotalDays, 0).ToString() + "d"); y++;
            }

            SectionRow(y, "OUTCOMES", "", "", aN.ToString() + " closed"); y++;
            Row(y, "Won / Lost / BE", aTp.ToString(), aSl.ToString(), aBe.ToString()); y++;
            if (ShowPeriodRows) { Row(y, "Worst losing run", "", "", _worstRun.ToString()); y++; }

            double winR = 0.0; int winN = 0;
            for (int k = 0; k < _log.Count; k++)
                if (_log[k].Outcome == 1 && !_log[k].Muted && InStats(_log[k].Src)) { winR += _log[k].R; winN++; }
            Row(y, "Avg winner", "", "", (winN == 0 ? "0" : Sign(winR / winN)) + " R");
            RowColour(y, PanelPos); y++;
            if (Layout == PanelLayout.Full) { Row(y, "R min / max", "", Sign(_minR), Sign(_maxR)); y++; }
            int openNow = 0;
            for (int k = 0; k < _trades.Count; k++) if (!_trades[k].Closed) openNow++;

            if (Layout == PanelLayout.Full) { Row(y, "Expired", "", "", aEx.ToString()); y++; }
            Row(y, "Still open", "", "", openNow.ToString()); y++;

            SectionRow(y, "ENGINES", "n / grey", "win%", "net R"); y++;

            // every engine is listed even at zero, otherwise a fully greyed
            // engine vanishes and looks like it was removed
            for (int src = 0; src < 10; src++)
            {
                if (!InStats(src)) continue;
                int tp = 0, sl = 0, be = 0, grey = 0;
                double net = 0.0;
                for (int k = 0; k < _log.Count; k++)
                {
                    if (_log[k].Src != src) continue;
                    if (_log[k].Muted) { grey++; continue; }
                    net += _log[k].R;
                    if (_log[k].Outcome == 1) tp++;
                    else if (_log[k].Outcome == 2) be++;
                    else if (_log[k].Outcome != 3) sl++;
                }
                int n = tp + sl + be;

                if (n == 0 && grey == 0) continue;      // an engine with nothing to say takes no row

                Row(y, SourceName(src), n.ToString() + " / " + grey.ToString(), Rate(tp, sl), Sign(net));
                RowColour(y, n == 0 ? GreyColor : (net >= 0 ? PanelPos : PanelNeg));
                y++;
            }

            if (Layout == PanelLayout.Full)
            {
                Row(y, "Cost per trade", CostFrom == CostSource.FromBroker ? "broker" : "manual", "",
                       Math.Round(DealCost(), 2).ToString()); y++;
                Row(y, "Window", WindowOffsetDays == 0 ? "tuned on this" : "OUT OF SAMPLE",
                       AnalyseDays.ToString() + "d", "back " + WindowOffsetDays.ToString() + "d"); y++;

                Row(y, "Why lost", "tight " + _lossTight.ToString(), "flat " + _lossFlat.ToString(),
                       "turn " + _lossTurn.ToString());
                RowColour(y, PanelNeg); y++;
            }

            if (ShowPeriodEngines && Layout == PanelLayout.Full)
            {
                bool wk = EnginePeriod == TablePeriod.Week;
                DateTime from = wk ? weekStart : today;

                int pN = 0;
                for (int k = 0; k < _log.Count; k++)
                    if (!_log[k].Muted && InStats(_log[k].Src) && _log[k].Day >= from && _log[k].Day <= today) pN++;

                double pDays = wk ? Math.Max(1.0, (today - weekStart).TotalDays + 1.0) : 1.0;

                SectionRow(y, wk ? "ENGINES week" : "ENGINES today",
                           "W/L/BE", "win%", "net R"); y++;

                for (int src = 0; src < 10; src++)
                {
                    if (!InStats(src)) continue;
                    int tp = 0, sl = 0, be = 0;
                    double net = 0.0;
                    for (int k = 0; k < _log.Count; k++)
                    {
                        Result r = _log[k];
                        if (r.Src != src || r.Muted) continue;
                        if (r.Day < from || r.Day > today) continue;
                        net += r.R;
                        if (r.Outcome == 1) tp++;
                        else if (r.Outcome == 2) be++;
                        else if (r.Outcome != 3) sl++;
                    }
                    if (tp + sl + be == 0) continue;

                    Row(y, SourceName(src),
                        tp.ToString() + "/" + sl.ToString() + "/" + be.ToString(),
                        Rate(tp, sl), Sign(net));
                    RowColour(y, net >= 0 ? PanelPos : PanelNeg);
                    y++;
                }

                Row(y, wk ? "week total / per day" : "today total",
                    pN.ToString(), wk ? Math.Round(pN / pDays, 2).ToString() : "",
                    ""); y++;
            }

            SectionRow(y, "LAST SIGNAL", "", "",
                _lsId.Length == 0 ? "none" : _lsTime.ToString("dd MMM HH:mm")); y++;

            if (_lsId.Length > 0)
            {
                string res = _lsResult == 1 ? "WON" : (_lsResult == 0 ? "LOST" :
                             (_lsResult == 2 ? "BREAKEVEN" : (_lsResult == 3 ? "EXPIRED" : "RUNNING")));

                Row(y, _lsLong ? "BUY" : "SELL", "", "", res);
                RowColour(y, _lsResult == 1 ? PanelPos : (_lsResult == 0 ? PanelNeg : PanelColor)); y++;

                Row(y, "Entry / Stop / Tgt",
                    Math.Round(_lsEntry, Symbol.Digits).ToString(),
                    Math.Round(_lsStop, Symbol.Digits).ToString(),
                    Math.Round(_lsTarget, Symbol.Digits).ToString()); y++;

                Row(y, "R : R", "", "", "1 : " + Math.Round(_lsRR, 1).ToString()); y++;
            }

            if (DashDetail == DashboardDetail.Full)
            {
                double avgStop = _sumStopN == 0 ? 0.0 : _sumStopAtr / _sumStopN;
                SectionRow(y, "DIAGNOSTICS v47", "", "", Math.Round(avgStop, 2).ToString() + " atr stop"); y++;
                Row(y, "Arm gates",
                       "T" + _rjTight.ToString() + " A" + _rjAtr.ToString() + " F" + _rjFall.ToString(),
                       "S" + _rjScore.ToString() + " U" + _rjFull.ToString() + " D" + _rjDup.ToString(),
                       "ok " + _armOk.ToString()); y++;
                Row(y, "Zone gates",
                       "life " + _zLife.ToString() + " broke " + _zDead.ToString(),
                       "touch " + _zTouch.ToString() + " gap " + _zGap.ToString(),
                       "noFire " + _zNoFire.ToString()); y++;
                int[] tW = new int[4];
                int[] tL = new int[4];
                for (int k = 0; k < _log.Count; k++)
                {
                    Result r = _log[k];
                    if (r.Src != 4 || r.Muted || r.Touch < 1) continue;
                    int b = r.Touch >= 3 ? 3 : r.Touch;
                    if (r.Outcome == 1) tW[b]++;
                    else if (r.Outcome != 2 && r.Outcome != 3) tL[b]++;
                }

                Row(y, "Zone build", "noBos " + _zNoBos.ToString() + " noSwp " + _zNoSweep.ToString(),
                       "noGap " + _zNoGap.ToString() + " lowSc " + _zLowScore.ToString(),
                       "built " + _zBuilt.ToString()); y++;
                int[] xW = new int[3]; int[] xL = new int[3];
                int[] zW = new int[3]; int[] zL = new int[3];
                for (int k = 0; k < _log.Count; k++)
                {
                    Result r = _log[k];
                    if (r.Src != 3 || r.Muted) continue;
                    bool won = r.Outcome == 1;
                    bool lost = r.Outcome != 1 && r.Outcome != 2 && r.Outcome != 3;
                    if (!won && !lost) continue;

                    int xb = r.EExt <= 1.0 ? 0 : (r.EExt <= 2.0 ? 1 : 2);
                    if (won) xW[xb]++; else xL[xb]++;

                    int zb = r.ESz <= 2.5 ? 0 : (r.ESz <= 3.5 ? 1 : 2);
                    if (won) zW[zb]++; else zL[zb]++;
                }

                int[] pW = new int[3]; int[] pL = new int[3];
                int[] gW = new int[3]; int[] gL = new int[3];
                int[] dW = new int[3]; int[] dL = new int[3];
                int[] lW = new int[3]; int[] lL = new int[3];
                for (int k = 0; k < _log.Count; k++)
                {
                    Result r = _log[k];
                    if (r.Muted) continue;
                    bool won = r.Outcome == 1;
                    bool lost = r.Outcome != 1 && r.Outcome != 2 && r.Outcome != 3;
                    if (!won && !lost) continue;

                    if (r.Src == 2)
                    {
                        int b1 = r.Mx <= 2.5 ? 0 : (r.Mx <= 3.5 ? 1 : 2);
                        if (won) pW[b1]++; else pL[b1]++;
                        int b2 = r.My <= 3 ? 0 : (r.My <= 6 ? 1 : 2);
                        if (won) gW[b2]++; else gL[b2]++;
                    }
                    else if (r.Src == 5)
                    {
                        int b3 = r.Mx <= 40 ? 0 : (r.Mx <= 60 ? 1 : 2);
                        if (won) dW[b3]++; else dL[b3]++;
                        int b4 = r.My <= 2.5 ? 0 : (r.My <= 4.0 ? 1 : 2);
                        if (won) lW[b4]++; else lL[b4]++;
                    }
                }

                int[] fW = new int[3]; int[] fL = new int[3];
                for (int k = 0; k < _log.Count; k++)
                {
                    Result r = _log[k];
                    if (r.Src != 8 || r.Muted) continue;
                    bool won = r.Outcome == 1;
                    bool lost = r.Outcome != 1 && r.Outcome != 2 && r.Outcome != 3;
                    if (!won && !lost) continue;
                    int b = r.Mx <= 0.40 ? 0 : (r.Mx <= 0.80 ? 1 : 2);
                    if (won) fW[b]++; else fL[b]++;
                }

                Row(y, "FVG win by gap",
                       "<.4 " + Rate(fW[0], fL[0]) + " n" + (fW[0] + fL[0]).ToString(),
                       "<.8 " + Rate(fW[1], fL[1]) + " n" + (fW[1] + fL[1]).ToString(),
                       ".8+ " + Rate(fW[2], fL[2]) + " n" + (fW[2] + fL[2]).ToString()); y++;

                Row(y, "SMC gates", "bos " + _smBos.ToString() + " zones " + _smZDir.Count.ToString(),
                       "made " + _smPromoted.ToString() + " purged " + _smPurged.ToString(),
                       "fired " + _smFired.ToString()); y++;
                Row(y, "SMC skips", "vol " + _smVolSkip.ToString(), "pd " + _smPdSkip.ToString(),
                       "tall " + _smTall.ToString()); y++;
                Row(y, "Refused on quality", "", "", _statBlockedQuality.ToString()); y++;

                Row(y, "FVG gates", "small " + _fvgSmall.ToString() + " weak " + _fvgWeak.ToString(),
                       "life " + _fvgLife.ToString()
                       + " broke " + _fvgBroke.ToString(),
                       "built " + _fvgBuilt.ToString() + " fired " + _fvgFired.ToString()); y++;

                Row(y, "PB win by size",
                       "<2.5 " + Rate(pW[0], pL[0]) + " n" + (pW[0] + pL[0]).ToString(),
                       "<3.5 " + Rate(pW[1], pL[1]) + " n" + (pW[1] + pL[1]).ToString(),
                       "3.5+ " + Rate(pW[2], pL[2]) + " n" + (pW[2] + pL[2]).ToString()); y++;
                Row(y, "PB win by age",
                       "<3 " + Rate(gW[0], gL[0]) + " n" + (gW[0] + gL[0]).ToString(),
                       "<6 " + Rate(gW[1], gL[1]) + " n" + (gW[1] + gL[1]).ToString(),
                       "6+ " + Rate(gW[2], gL[2]) + " n" + (gW[2] + gL[2]).ToString()); y++;
                Row(y, "TPB win by depth",
                       "<40 " + Rate(dW[0], dL[0]) + " n" + (dW[0] + dL[0]).ToString(),
                       "<60 " + Rate(dW[1], dL[1]) + " n" + (dW[1] + dL[1]).ToString(),
                       "60+ " + Rate(dW[2], dL[2]) + " n" + (dW[2] + dL[2]).ToString()); y++;
                Row(y, "TPB win by leg",
                       "<2.5 " + Rate(lW[0], lL[0]) + " n" + (lW[0] + lL[0]).ToString(),
                       "<4 " + Rate(lW[1], lL[1]) + " n" + (lW[1] + lL[1]).ToString(),
                       "4+ " + Rate(lW[2], lL[2]) + " n" + (lW[2] + lL[2]).ToString()); y++;

                Row(y, "Ele win by ext",
                       "<1 " + Rate(xW[0], xL[0]) + " n" + (xW[0] + xL[0]).ToString(),
                       "<2 " + Rate(xW[1], xL[1]) + " n" + (xW[1] + xL[1]).ToString(),
                       "2+ " + Rate(xW[2], xL[2]) + " n" + (xW[2] + xL[2]).ToString()); y++;
                Row(y, "Ele win by size",
                       "<2.5 " + Rate(zW[0], zL[0]) + " n" + (zW[0] + zL[0]).ToString(),
                       "<3.5 " + Rate(zW[1], zL[1]) + " n" + (zW[1] + zL[1]).ToString(),
                       "3.5+ " + Rate(zW[2], zL[2]) + " n" + (zW[2] + zL[2]).ToString()); y++;

                Row(y, "Ele gates", "small " + _eSmall.ToString() + " ext " + _eExt.ToString(),
                       "bos " + _eNoBos.ToString() + " gap " + _eNoGap.ToString(),
                       "ok " + _eOk.ToString()); y++;
                Row(y, "TPB gates", "dup " + _tDup.ToString() + " bos " + _tNoBos.ToString(),
                       "swp " + _tNoSweep.ToString() + " gap " + _tNoGap.ToString(),
                       "weak " + _tWeak.ToString()); y++;
                Row(y, "TPB fired / score", "lowSc " + _tLowScore.ToString(), _tFired.ToString(),
                       (_tFired > 0 ? (_tScoreSum / _tFired).ToString() : "0")); y++;

                Row(y, "Zone avg score", "", "",
                       (_zBuilt > 0 ? (_zScoreSum / _zBuilt).ToString() : "0")); y++;
                Row(y, "Zone fired", "stale " + _zStale.ToString(), "wide " + _zWide.ToString(),
                       _zFired.ToString()); y++;
                int[] aW = new int[3]; int[] aL = new int[3];
                int[] sW = new int[3]; int[] sL = new int[3];
                for (int k = 0; k < _log.Count; k++)
                {
                    Result r = _log[k];
                    if (r.Src != 4 || r.Muted || r.Touch < 1) continue;
                    bool won = r.Outcome == 1;
                    bool lost = r.Outcome != 1 && r.Outcome != 2 && r.Outcome != 3;
                    if (!won && !lost) continue;

                    int ab = r.ZAge <= 10 ? 0 : (r.ZAge <= 30 ? 1 : 2);
                    if (won) aW[ab]++; else aL[ab]++;

                    int sb = r.ZSize <= 0.5 ? 0 : (r.ZSize <= 1.0 ? 1 : 2);
                    if (won) sW[sb]++; else sL[sb]++;
                }

                Row(y, "Zone touch",
                       "t1 " + Rate(tW[1], tL[1]) + " n" + (tW[1] + tL[1]).ToString(),
                       "t2 " + Rate(tW[2], tL[2]) + " n" + (tW[2] + tL[2]).ToString(),
                       "t3+ " + Rate(tW[3], tL[3]) + " n" + (tW[3] + tL[3]).ToString()); y++;
                Row(y, "Zone age bars",
                       "<10 " + Rate(aW[0], aL[0]) + " n" + (aW[0] + aL[0]).ToString(),
                       "<30 " + Rate(aW[1], aL[1]) + " n" + (aW[1] + aL[1]).ToString(),
                       "30+ " + Rate(aW[2], aL[2]) + " n" + (aW[2] + aL[2]).ToString()); y++;
                Row(y, "Zone size atr",
                       "<.5 " + Rate(sW[0], sL[0]) + " n" + (sW[0] + sL[0]).ToString(),
                       "<1 " + Rate(sW[1], sL[1]) + " n" + (sW[1] + sL[1]).ToString(),
                       "1+ " + Rate(sW[2], sL[2]) + " n" + (sW[2] + sL[2]).ToString()); y++;

                Row(y, "Arm loss",
                       "exp " + _expExpire.ToString() + " vol " + _statBlockedVol.ToString(),
                       "rng " + _statBlockedRange.ToString() + " chase " + _statAbandoned.ToString(),
                       "qual " + _expQual.ToString()); y++;
                Row(y, "Pre-exp", "armed " + _statArmed.ToString(),
                       "fired " + _statFired.ToString(), "skip " + _statSkippedRisk.ToString()); y++;
                Row(y, "Refused", "risk " + _statSkippedRisk.ToString(),
                       "streak " + _statBlockedStreak.ToString(),
                       "cap " + _statBlockedCap.ToString()); y++;
            }
        }

        // Second table, consolidation signals only. These are kept out of the
        // main numbers so a range cannot flatter or ruin the headline result.
        private void BuildPanel2()
        {
            if (_panel2Built) return;
            _panel2Built = true;

            _cells2 = new TextBlock[Panel2Rows, PanelCols];
            _bg2 = new Grid[Panel2Rows, PanelCols];

            Grid outer = new Grid(Panel2Rows, PanelCols);
            outer.BackgroundColor = PanelBackColor;
            outer.Opacity = PanelOpacity;

            bool left = GreyPanelWhere == PanelCorner.TopLeft || GreyPanelWhere == PanelCorner.BottomLeft;
            bool bottom = GreyPanelWhere == PanelCorner.BottomRight || GreyPanelWhere == PanelCorner.BottomLeft;
            outer.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            outer.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            outer.Margin = 4;

            for (int r = 0; r < Panel2Rows; r++)
            {
                for (int c = 0; c < PanelCols; c++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = "";
                    tb.FontSize = PanelFontSize;
                    tb.ForegroundColor = PanelColor;
                    tb.Margin = 2;
                    tb.HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    Grid cell = new Grid(1, 1);
                    cell.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    cell.AddChild(tb, 0, 0);

                    _cells2[r, c] = tb;
                    _bg2[r, c] = cell;
                    outer.AddChild(cell, r, c);
                }
            }

            Chart.AddControl(outer);
        }

        private void Row2(int r, string a, string b, string c, string d)
        {
            if (r < 0 || r >= Panel2Rows) return;
            _cells2[r, 0].Text = a;
            _cells2[r, 1].Text = b;
            _cells2[r, 2].Text = c;
            _cells2[r, 3].Text = d;
        }

        private void DrawConsoPanel()
        {
            if (!ShowGreyPanel) return;
            BuildPanel2();

            for (int r = 0; r < Panel2Rows; r++)
            {
                Row2(r, "", "", "", "");
                for (int c = 0; c < PanelCols; c++)
                {
                    _bg2[r, c].BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    _cells2[r, c].ForegroundColor = GreyColor;
                }
            }

            int tp = 0, sl = 0, be = 0;
            double won = 0.0, lost = 0.0;

            for (int k = 0; k < _log.Count; k++)
            {
                Result r = _log[k];
                if (!r.Muted) continue;
                if (r.Outcome == 1) { tp++; won += r.R; }
                else if (r.Outcome == 2) be++;
                else if (r.Outcome != 3) { sl++; lost += r.R; }
            }

            int n = tp + sl + be;

            int y = 0;
            Row2(y, "GREYED SIGNALS", "", "", n.ToString());
            for (int c = 0; c < PanelCols; c++)
            {
                _bg2[y, c].BackgroundColor = SectionBack;
                _cells2[y, c].ForegroundColor = SectionText;
            }
            y++;

            Row2(y, "", "TP", "SL", "BE"); y++;
            Row2(y, "Count", tp.ToString(), sl.ToString(), be.ToString()); y++;
            Row2(y, "Win rate", "", "", Rate(tp, sl) + " %"); y++;
            Row2(y, "Would have won", "", "", Sign(won) + " R"); y++;
            Row2(y, "Would have lost", "", "", Sign(lost) + " R"); y++;
            Row2(y, "Net if taken", "", "", Sign(won + lost) + " R"); y++;

            int r1 = 0, r2 = 0, r3 = 0, r4 = 0;
            for (int k = 0; k < _log.Count; k++)
            {
                if (!_log[k].Muted) continue;
                if (_log[k].Reason == 1) r1++;
                else if (_log[k].Reason == 2) r2++;
                else if (_log[k].Reason == 3) r3++;
                else if (_log[k].Reason == 4) r4++;
            }
            Row2(y, "range / streak / htf", r1.ToString(), r2.ToString(), r3.ToString()); y++;
            Row2(y, "quality", "", "", r4.ToString()); y++;
            Row2(y, "off timeframe", "", "", _statOffTf.ToString()); y++;
            Row2(y, "refused on quality", "", "", _statBlockedQuality.ToString()); y++;
        }

        // ===================== WEEKDAY TABLE ================================
        private void BuildPanel3()
        {
            if (_panel3Built) return;
            _panel3Built = true;

            _cells3 = new TextBlock[Panel3Rows, Panel3Cols];
            _bg3 = new Grid[Panel3Rows, Panel3Cols];

            Grid outer = new Grid(Panel3Rows, Panel3Cols);
            outer.BackgroundColor = PanelBackColor;
            outer.Opacity = PanelOpacity;

            bool left = DayWhere == PanelCorner.TopLeft || DayWhere == PanelCorner.BottomLeft;
            bool bottom = DayWhere == PanelCorner.BottomRight || DayWhere == PanelCorner.BottomLeft;
            outer.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            outer.VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            outer.Margin = 4;

            for (int r = 0; r < Panel3Rows; r++)
            {
                for (int c = 0; c < Panel3Cols; c++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = "";
                    tb.FontSize = PanelFontSize;
                    tb.ForegroundColor = PanelColor;
                    tb.Margin = 2;
                    tb.HorizontalAlignment = c == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

                    Grid cell = new Grid(1, 1);
                    cell.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    cell.AddChild(tb, 0, 0);

                    _cells3[r, c] = tb;
                    _bg3[r, c] = cell;
                    outer.AddChild(cell, r, c);
                }
            }

            Chart.AddControl(outer);
        }

        private void Row3(int r, string a, string b, string c, string d, string e)
        {
            if (r < 0 || r >= Panel3Rows) return;
            _cells3[r, 0].Text = a;
            _cells3[r, 1].Text = b;
            _cells3[r, 2].Text = c;
            _cells3[r, 3].Text = d;
            _cells3[r, 4].Text = e;
        }

        private void Head3(int r, string a, string b, string c, string d, string e)
        {
            Row3(r, a, b, c, d, e);
            for (int q = 0; q < Panel3Cols; q++)
            {
                _bg3[r, q].BackgroundColor = SectionBack;
                _cells3[r, q].ForegroundColor = SectionText;
            }
        }

        // Monday is 0. Only trades inside the analysis window are in _log, so
        // this is the same population the main panel reports.
        private void DayRow(int r, string label, int dow, int srcWanted, bool allEngines)
        {
            int tp = 0, sl = 0, be = 0;
            double net = 0.0;

            for (int k = 0; k < _log.Count; k++)
            {
                Result x = _log[k];
                if (x.Muted) continue;
                if (allEngines) { if (!InStats(x.Src)) continue; }
                else if (x.Src != srcWanted) continue;
                int wd = ((int)x.Day.DayOfWeek + 6) % 7;
                if (dow >= 0 && wd != dow) continue;
                if (dow == -2 && wd < 5) continue;      // weekend only

                net += x.R;
                if (x.Outcome == 1) tp++;
                else if (x.Outcome == 2) be++;
                else if (x.Outcome != 3) sl++;
            }

            int n = tp + sl + be;
            Row3(r, label, n.ToString(),
                 tp.ToString() + "/" + sl.ToString() + "/" + be.ToString(),
                 n == 0 ? "" : Rate(tp, sl) + " %",
                 n == 0 ? "" : Sign(net) + " R");

            Color col = n == 0 ? GreyColor : (net >= 0 ? PanelPos : PanelNeg);
            for (int q = 0; q < Panel3Cols; q++) _cells3[r, q].ForegroundColor = col;
        }

        private int WeekendCount(int srcWanted, bool allEngines)
        {
            int n = 0;
            for (int k = 0; k < _log.Count; k++)
            {
                Result x = _log[k];
                if (x.Muted) continue;
                if (allEngines) { if (!InStats(x.Src)) continue; }
                else if (x.Src != srcWanted) continue;
                if (((int)x.Day.DayOfWeek + 6) % 7 >= 5) n++;
            }
            return n;
        }

        private void DrawDayPanel()
        {
            if (!ShowDayTable) return;
            BuildPanel3();

            for (int r = 0; r < Panel3Rows; r++)
            {
                Row3(r, "", "", "", "", "");
                for (int c = 0; c < Panel3Cols; c++)
                {
                    _bg3[r, c].BackgroundColor = Color.FromArgb(0, 0, 0, 0);
                    _cells3[r, c].ForegroundColor = PanelColor;
                }
            }

            bool allSel = DayEngine == EngineFilter.All;
            int src = allSel ? -1 : (int)DayEngine - 1;
            string name = allSel ? "ALL ENGINES" : SourceName(src);

            string[] dn = new string[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

            int y = 0;
            Head3(y, name, "n", "W/L/BE", "win%", "net R"); y++;

            for (int d = 0; d < 5; d++) { DayRow(y, dn[d], d, src, allSel); y++; }

            if (WeekendCount(src, allSel) > 0) { DayRow(y, "Weekend", -2, src, allSel); y++; }

            DayRow(y, "WEEK", -1, src, allSel);
            for (int q = 0; q < Panel3Cols; q++) _bg3[y, q].BackgroundColor = SectionBack;
            y++;

            if (allSel) return;

            y++;
            Head3(y, "ALL ENGINES", "n", "W/L/BE", "win%", "net R"); y++;
            for (int d = 0; d < 5; d++) { DayRow(y, dn[d], d, -1, true); y++; }
            if (WeekendCount(-1, true) > 0) { DayRow(y, "Weekend", -2, -1, true); y++; }
            DayRow(y, "WEEK", -1, -1, true);
            for (int q = 0; q < Panel3Cols; q++) _bg3[y, q].BackgroundColor = SectionBack;
        }

        private string BiasWord(int b)
        {
            if (b > 0) return "up";
            if (b < 0) return "down";
            return "flat";
        }

        private string TfLabel()
        {
            double m = BarDuration().TotalMinutes;
            if (m < 60.0) return "m" + Math.Round(m, 0).ToString();
            if (m < 1440.0) return "h" + Math.Round(m / 60.0, 0).ToString();
            return "D" + Math.Round(m / 1440.0, 0).ToString();
        }

        // Strike rate on trades that actually resolved to a win or a full loss.
        private string Rate(int tp, int sl)
        {
            int d = tp + sl;
            if (d == 0) return "";
            return Math.Round(100.0 * tp / d, 1).ToString();
        }

        // Share of trades that did not lose money. Break even counts here.
        private string Safe(int tp, int sl, int be)
        {
            int d = tp + sl + be;
            if (d == 0) return "";
            return Math.Round(100.0 * (tp + be) / d, 1).ToString();
        }

        // ===================== HELPERS ======================================
        private string TagOf(int src)
        {
            if (src == 0) return "EXP";
            if (src == 1) return "BOS";
            if (src == 2) return "PB";
            if (src == 4) return "ZONE";
            if (src == 8) return "FVG";
            if (src == 9) return "SMC";
            if (src == 5) return "TPB";
            if (src == 6) return "FBO";
            if (src == 7) return "SWP";
            return "ELE";
        }

        private string SourceName(int src)
        {
            if (src == 0) return "Pre-expansion";
            if (src == 1) return "Break structure";
            if (src == 2) return "Pullback";
            if (src == 4) return "Zone retest";
            if (src == 8) return "FVG retest";
            if (src == 9) return "SMC structure";
            if (src == 5) return "Trend pullback";
            if (src == 6) return "Range fakeout";
            if (src == 7) return "Prior day sweep";
            return "Elephant";
        }

        private int SourceOf(string tag)
        {
            if (tag == "EXP") return 0;
            if (tag == "BOS") return 1;
            if (tag == "PB") return 2;
            if (tag == "ZONE") return 4;
            if (tag == "FVG") return 8;
            if (tag == "SMC") return 9;
            if (tag.StartsWith("TPB")) return 5;
            if (tag.StartsWith("FBO")) return 6;
            if (tag.StartsWith("SWP")) return 7;
            return 3;
        }


        private string Sign(double v)
        {
            double r = Math.Round(v, 2);
            return (r > 0 ? "+" : "") + r.ToString();
        }

        private bool ShouldDraw(int i)
        {
            if (HistoryView == HistoryDisplay.AllHistory) return true;
            if (Bars.Count < 1) return true;

            DateTime last = Bars.OpenTimes[Bars.Count - 1].Date;
            DateTime d = Bars.OpenTimes[i].Date;
            if (HistoryView == HistoryDisplay.TodayOnly) return d == last;
            return (last - d).TotalDays < DaysToShow;
        }

        // Median of recent gaps, so a weekend cannot stretch a box across weeks.
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

        private ChartIconType NativeIconFor(bool isLong)
        {
            if (InMarkerShapeStyle == MarkerShape.Triangle) return isLong ? ChartIconType.UpTriangle : ChartIconType.DownTriangle;
            if (InMarkerShapeStyle == MarkerShape.Circle) return ChartIconType.Circle;
            if (InMarkerShapeStyle == MarkerShape.Dot) return ChartIconType.Circle;
            if (InMarkerShapeStyle == MarkerShape.Square) return ChartIconType.Square;
            if (InMarkerShapeStyle == MarkerShape.Diamond) return ChartIconType.Diamond;
            if (InMarkerShapeStyle == MarkerShape.Star) return ChartIconType.Star;
            return isLong ? ChartIconType.UpArrow : ChartIconType.DownArrow;
        }

        private string GlyphFor(bool isLong)
        {
            if (InMarkerShapeStyle == MarkerShape.Triangle) return isLong ? "\u25B2" : "\u25BC";
            if (InMarkerShapeStyle == MarkerShape.Circle) return "\u25CF";
            if (InMarkerShapeStyle == MarkerShape.Dot) return "\u2022";
            if (InMarkerShapeStyle == MarkerShape.Square) return "\u25A0";
            if (InMarkerShapeStyle == MarkerShape.Diamond) return "\u25C6";
            if (InMarkerShapeStyle == MarkerShape.Star) return "\u2605";
            return isLong ? "\u2191" : "\u2193";
        }

        private double ResolveEntry(int i, double level, double confirmed)
        {
            if (EntryMode == EntryPriceMode.TriggerLevel) return level;
            if (EntryMode == EntryPriceMode.BarOpen) return Bars.OpenPrices[i];
            if (EntryMode == EntryPriceMode.BarMid) return (Bars.HighPrices[i] + Bars.LowPrices[i]) / 2.0;
            return confirmed;
        }

        private bool InWindow(DateTime t)
        {
            // h1 and above is swing trading, held for weeks, so session hours
            // do not apply there
            if (TfMinutes() >= SwingFromMinutes) return true;

            int h = t.Hour;
            if (SessionStart == SessionEnd) return true;
            if (SessionStart < SessionEnd) return h >= SessionStart && h < SessionEnd;
            return h >= SessionStart || h < SessionEnd;
        }

        private bool TooManyToday(int i)
        {
            if (!UseGlobalCap) return false;
            DateTime d = Bars.OpenTimes[i].Date;
            int c;
            if (!_dailyCount.TryGetValue(d, out c)) c = 0;
            return c >= MaxPerDay;
        }

        private void BumpDaily(int i)
        {
            DateTime d = Bars.OpenTimes[i].Date;
            if (!_dailyCount.ContainsKey(d)) _dailyCount[d] = 0;
            _dailyCount[d] = _dailyCount[d] + 1;
            _lastSignalIndex = i;
        }

        private bool SpacedEnough(int i)
        {
            if (!GlobalSpacing) return true;
            return i - _lastSignalIndex >= MinGapBars;
        }

        private static double Clamp(double v, double lo, double hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
