using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Every number of the timed levels in one place: which levels are timed, the time-limit formula (used by the
    /// Editor step One Line/Recalculate Time Limits and as a runtime fallback), star thresholds, the two time boosts
    /// (effect, limits, starting stock, coin prices) and the look/motion of the hourglass, boost buttons and panels.
    /// </summary>
    [CreateAssetMenu(fileName = "TimerSettings", menuName = "One Line/Timer Settings")]
    public class TimerSettings : ScriptableObject
    {
        [Header("Which levels are timed")]
        [Tooltip("Only HARD levels (every 5th) from this level on have a time limit. 5 and 10 are onboarding.")]
        public int firstTimedLevel = 15;
        [Tooltip("Shown once, on the first timed level.")]
        public string introHint = "The stars wait for no one";
        public float introHintTime = 3.2f;

        [Header("Time limit = base + perNode * nodes + perBit * difficulty, x HARD/BOSS, rounded up, clamped")]
        public float baseSeconds = 5f;
        public float perNodeSeconds = 1.5f;
        public float perDifficultyBitSeconds = 4f;
        public float hardMultiplier = 1.2f;
        public float bossMultiplier = 1.4f;
        [Tooltip("Rounded UP to a multiple of this many seconds.")]
        public float roundToSeconds = 5f;
        public float minSeconds = 20f;
        public float maxSeconds = 150f;

        [Header("Stars (share of the time left at the win)")]
        [Range(0f, 1f)] public float threeStarShare = 0.5f;
        [Range(0f, 1f)] public float twoStarShare = 0.2f;

        [Header("Saturn's Gift (+time)")]
        public float saturnSeconds = 15f;
        [Tooltip("Per level, shared by the boost button and the time-up panel.")]
        public int maxSaturnPerLevel = 2;

        [Header("Lunar Stillness (slow time)")]
        [Tooltip("The timer runs at this speed while it lasts. Gameplay input is never slowed.")]
        [Range(0.1f, 1f)] public float lunarSpeed = 0.5f;
        [Tooltip("Real (unpaused) seconds.")]
        public float lunarSeconds = 10f;
        public int maxLunarPerLevel = 1;

        [Header("Inventory & prices (coins). A level pays 10 (HARD 15, BOSS 20) — see CoinManager")]
        public int startingSaturn = 2;
        public int startingLunar = 2;
        [Tooltip("Saturn's Gifts earned the first time a timed BOSS level is cleared with 3 stars.")]
        public int bossThreeStarReward = 1;
        [Tooltip("≈ 3 levels of earnings.")]
        public int saturnPrice = 30;
        public int saturnPackPrice = 125;
        [Tooltip("≈ 4 levels of earnings.")]
        public int lunarPrice = 40;
        public int lunarPackPrice = 170;
        public int packSize = 5;

        [Header("Hourglass — layout (reference units)")]
        [Tooltip("Bottom-left, above the Restart button. The drawing is laid out for 130 x 190 and scaled.")]
        public Vector2 hourglassSize = new(150f, 220f);
        [Tooltip("Gap between the top of the Restart button and the hourglass's time label (or its flip, if that reaches lower).")]
        public float hourglassAboveRestart = 16f;
        [Tooltip("Gap kept between the title frame's side motif and the boost column.")]
        public float hourglassGap = 16f;
        [Tooltip("The astrolabe menu button in the top-left corner (replaces Settings + Map on timed levels).")]
        public float menuButtonSize = 96f;
        public float timeLabelSize = 30f;
        public float timeLabelGap = 4f;

        [Header("Hourglass — look")]
        public Color sandGold = new(1f, 0.83f, 0.45f);
        public Color sandAmber = new(1f, 0.6f, 0.24f);
        public Color sandRed = new(0.95f, 0.36f, 0.34f);
        public Color sandSilver = new(0.72f, 0.84f, 1f);
        [Tooltip("Sand turns amber at this share left, soft red at the next.")]
        [Range(0f, 1f)] public float amberShare = 0.3f;
        [Range(0f, 1f)] public float redShare = 0.1f;
        [Range(0f, 1f)] public float glassAlpha = 0.5f;
        [Range(0f, 1f)] public float sheenAlpha = 0.22f;
        [Range(0f, 1f)] public float sandGlowAlpha = 0.28f;
        [Range(0f, 1f)] public float labelAlpha = 0.45f;
        [Range(0f, 1f)] public float labelWarningAlpha = 0.95f;
        public int streamMotes = 8;
        public int sandSparkles = 6;
        [Tooltip("Stream motes fall this fast (reference units / s) at normal speed.")]
        public float streamSpeed = 140f;

        [Header("Hourglass — motion")]
        [Tooltip("The level waits with the sand in the bottom bulb; the first touch flips it (180°, ease-in-out-back).")]
        public float flipTime = 0.55f;
        [Tooltip("Scale at the middle of the flip (1 → this → 1).")]
        public float flipPunch = 1.15f;
        [Tooltip("After the flip the sand slides off the top cap down onto the neck.")]
        public float settleTime = 0.3f;
        [Tooltip("Size of the small gold light burst when the flip lands.")]
        public float flipBurstSize = 200f;
        [Tooltip("How deep the top sand's surface dips in the middle (drawing units).")]
        public float sandDip = 7f;
        [Tooltip("The last seconds: the glass pulses and ticks once per second.")]
        public float warningSeconds = 10f;
        public float warningPulse = 0.05f;
        public float crackTime = 0.6f;
        public float timeUpShake = 10f;
        public float refillTime = 0.8f;
        public int winMotes = 6;
        public float winMoteTime = 0.9f;

        [Header("Boost buttons")]
        public float boostButtonSize = 114f;
        public float boostGap = 22f;
        public float boostTopGap = 18f;
        public float boostBadgeSize = 44f;
        public float boostGlyphSize = 36f;
        [Range(0f, 1f)] public float boostDisabledAlpha = 0.4f;
        [Tooltip("Usable boosts: soft glow behind the button (Linear color space — keep it low).")]
        [Range(0f, 1f)] public float boostGlowAlpha = 0.22f;
        [Tooltip("Usable boosts breathe very slowly: seconds per breath and extra scale at the peak.")]
        public float boostBreathPeriod = 3.6f;
        public float boostBreathScale = 0.03f;
        public float spiralTime = 0.7f;
        public float floatTextRise = 90f;
        public float floatTextTime = 1.1f;
        [Tooltip("Moonlight vignette: a deep blue that settles into the screen edges.")]
        public Color moonVignette = new(0.03f, 0.06f, 0.16f);
        [Range(0f, 1f)] public float moonVignetteAlpha = 0.55f;
        [Tooltip("Linear color space: a light overlay reads much brighter than its alpha — keep this tiny.")]
        [Range(0f, 0.1f)] public float moonTintAlpha = 0.012f;
        public Color moonTint = new(0.55f, 0.7f, 1f);
        [Tooltip("Sky twinkle / drift speed while Lunar Stillness lasts.")]
        [Range(0.1f, 1f)] public float skySlowdown = 0.4f;
        public float moonFadeTime = 0.6f;

        [Header("Panels")]
        public Vector2 timeUpSize = new(840f, 790f);
        public Vector2 pauseSize = new(760f, 900f);
        public Vector2 offerSize = new(780f, 820f);
        public Vector2 panelButtonSize = new(560f, 110f);
        public float panelButtonGap = 26f;

        // ---------- rules ----------

        /// <summary>Only HARD levels from <see cref="firstTimedLevel"/> on are eligible for a time limit.</summary>
        public bool IsTimedNumber(int levelNumber) => levelNumber >= firstTimedLevel && Difficulty.IsHard(levelNumber);

        /// <summary>The formula (seconds) for a level with this many nodes and this difficulty (bits).</summary>
        public float ComputeLimit(int levelNumber, int nodeCount, float difficultyBits)
        {
            float t = baseSeconds + perNodeSeconds * nodeCount + perDifficultyBitSeconds * difficultyBits;
            if (Difficulty.IsBoss(levelNumber)) t *= bossMultiplier;
            else if (Difficulty.IsHard(levelNumber)) t *= hardMultiplier;
            if (roundToSeconds > 0f) t = Mathf.Ceil(t / roundToSeconds - 1e-4f) * roundToSeconds;
            return Mathf.Clamp(t, minSeconds, maxSeconds);
        }

        public float ComputeLimit(int levelNumber, LevelData level) =>
            ComputeLimit(levelNumber, level.nodes.Length, level.difficulty > 0f ? level.difficulty : Difficulty.Measure(level));

        /// <summary>
        /// The limit a level plays with: a hand-tuned value as is (0 = untimed); otherwise the stored value, or the
        /// formula if the Editor step hasn't filled it yet; 0 for levels that aren't eligible.
        /// </summary>
        public float LimitFor(int levelNumber, LevelData level)
        {
            if (!level) return 0f;
            if (level.manualTimeLimit) return Mathf.Max(0f, level.timeLimit);
            if (!IsTimedNumber(levelNumber)) return 0f;
            return level.timeLimit > 0f ? level.timeLimit : ComputeLimit(levelNumber, level);
        }

        public int Stars(float shareLeft) => shareLeft >= threeStarShare ? 3 : shareLeft >= twoStarShare ? 2 : 1;

        public int Price(BoostType type, int amount) => type == BoostType.SaturnsGift
            ? amount >= packSize ? saturnPackPrice : saturnPrice * amount
            : amount >= packSize ? lunarPackPrice : lunarPrice * amount;

        public int StartingCount(BoostType type) => type == BoostType.SaturnsGift ? startingSaturn : startingLunar;

        /// <summary>Sand color for this share of time left: gold → amber → soft red.</summary>
        public Color SandColor(float share)
        {
            if (share <= redShare) return sandRed;
            if (share <= amberShare)
                return Color.Lerp(sandRed, sandAmber, Mathf.SmoothStep(0f, 1f, (share - redShare) / Mathf.Max(0.001f, amberShare - redShare)));
            return Color.Lerp(sandAmber, sandGold, Mathf.SmoothStep(0f, 1f, (share - amberShare) / 0.1f));
        }
    }
}
