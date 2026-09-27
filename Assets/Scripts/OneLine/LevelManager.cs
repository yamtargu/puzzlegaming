using System;
using System.Collections;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Plays the levels of a LevelPack in order, saves progress and the star rating (LevelTimer rates timed levels),
    /// pays the coin reward and the BOSS Saturn's Gift, advances after a win.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public LevelPack levelPack;
        [Tooltip("If set, only this level is played (and replayed after a win). For quick testing.")]
        public LevelData testLevel;
        public PathManager pathManager;
        [Tooltip("Name + colors per tier of 10 levels.")]
        public ThemeSet themeSet;
        [Tooltip("Shop items. The equipped line skin / background replace the tier colors (visual only).")]
        public CosmeticCatalog cosmetics;
        [Tooltip("Load the next level by itself after a win. Off when a WinPanel (Next button) is used.")]
        public bool autoAdvance = true;
        [Tooltip("Load the saved level as soon as the game starts. Off when a ScreenRouter starts on the home screen.")]
        public bool loadOnStart = true;
        [Tooltip("Rates timed levels (stars by the time left). Without it every win gets placeholderStars.")]
        public LevelTimer timer;
        [Tooltip("Stars saved for a win when there is no LevelTimer.")]
        [Range(0, 3)] public int placeholderStars = 3;
        [Tooltip("Seconds between a win and the next level loading (auto advance only).")]
        public float nextLevelDelay = 0.5f;
        [Tooltip("How far HARD / BOSS levels tint the background toward the tier's accent color.")]
        [Range(0, 0.5f)] public float hardTint = 0.08f;
        [Range(0, 0.5f)] public float bossTint = 0.15f;

        public int CurrentIndex { get; private set; }
        public int CurrentNumber => CurrentIndex + 1;
        public int LevelCount => testLevel ? 1 : levelPack.Count;
        public ThemeSet.Tier CurrentTier { get; private set; }
        public int CurrentPercent { get; private set; }
        /// <summary>Stars of the last win (WinPanel shows them).</summary>
        public int LastStars { get; private set; } = 3;
        public bool IsHard => Difficulty.IsHard(CurrentNumber);
        public bool IsBoss => Difficulty.IsBoss(CurrentNumber);
        /// <summary>Zodiac house of the current level (0 = Aries … 11 = Pisces). Theme only.</summary>
        public int CurrentSign => Zodiac.SignOf(CurrentNumber);
        /// <summary>The level "Continue" opens: the first unfinished one.</summary>
        public int NextUnfinishedIndex => testLevel ? 0 : Mathf.Clamp(SaveService.HighestUnlocked, 0, LevelCount - 1);

        /// <summary>Raised after a level is loaded. Argument: level index.</summary>
        public event Action<int> LevelLoaded;

        (Color background, Color edge, Color node, Color path) defaultPalette;

        void Awake() => defaultPalette =
            (pathManager.background, pathManager.edgeColor, pathManager.nodeColor, pathManager.pathColor);

        void OnEnable()
        {
            if (pathManager) pathManager.Completed += OnCompleted;
            Cosmetics.Changed += OnCosmeticsChanged;
        }

        void OnDisable()
        {
            if (pathManager) pathManager.Completed -= OnCompleted;
            Cosmetics.Changed -= OnCosmeticsChanged;
        }

        void Start()
        {
            if (!testLevel && (!levelPack || levelPack.Count == 0))
            {
                Debug.LogError("LevelManager: assign a LevelPack with at least one level, or a Test Level.", this);
                enabled = false;
                return;
            }
            if (loadOnStart) LoadLevel(testLevel ? 0 : SaveService.LastPlayed);
        }

        public void LoadLevel(int index)
        {
            LevelData level;
            if (testLevel)
            {
                CurrentIndex = 0;
                level = testLevel;
            }
            else
            {
                CurrentIndex = ((index % levelPack.Count) + levelPack.Count) % levelPack.Count;
                level = levelPack[CurrentIndex];
            }

#if UNITY_EDITOR
            // Builds are already guarded by LevelValidation; in the editor, refuse to play a broken level.
            if (!LevelSolver.IsSolvable(level))
            {
                var report = LevelSolver.Analyze(level, 1);
                Debug.LogError($"Refusing to load UNSOLVABLE level {level.name}: {string.Join("; ", report.Problems)}", level);
                if (!testLevel && ++skipped < levelPack.Count) LoadLevel(CurrentIndex + 1);
                return;
            }
            skipped = 0;
#endif
            if (!testLevel) SaveService.LastPlayed = CurrentIndex;
            CurrentPercent = Difficulty.Percent(level.difficulty > 0 ? level.difficulty : Difficulty.Measure(level));
            ApplyTheme();
            pathManager.Load(level);
            LevelLoaded?.Invoke(CurrentIndex);
        }

        // Colors = the tier's palette, with the player's equipped cosmetics swapped in. Visual only.
        void ApplyTheme()
        {
            CurrentTier = themeSet ? themeSet.Get(Difficulty.TierIndex(CurrentNumber)) : null;
            var (background, edge, node, path) = CurrentTier != null
                ? (CurrentTier.background, CurrentTier.edge, CurrentTier.node, CurrentTier.path)
                : defaultPalette;

            // Default look: stars colored by visit order (StarStyle). An equipped line skin replaces it with one color.
            pathManager.usePalette = true;
            if (cosmetics)
            {
                var skin = cosmetics.EquippedSkin();
                if (skin != null && !skin.useTierColors)
                {
                    path = skin.line;
                    node = skin.node;
                    pathManager.usePalette = false;
                }
                var back = cosmetics.EquippedBackground();
                if (back != null && !back.useTierColors) { background = back.background; edge = back.edge; }
            }

            float tint = IsBoss ? bossTint : IsHard ? hardTint : 0f;
            pathManager.ApplyTheme(Color.Lerp(background, path, tint), edge, node, path);
        }

        // Equipping something in the shop repaints the level on screen right away.
        void OnCosmeticsChanged()
        {
            if (pathManager.Level) ApplyTheme();
        }

#if UNITY_EDITOR
        int skipped; // guards against looping forever when every level is broken
#endif

        public void LoadNext()
        {
            if (!testLevel && CurrentIndex == levelPack.Count - 1)
                Debug.Log("All levels complete — starting over from level 1.");
            LoadLevel(CurrentIndex + 1);
        }

        void OnCompleted()
        {
            // No undo or par-time system exists yet, so only the base + HARD/BOSS part of the reward applies.
            CoinManager.Add(CoinManager.LevelReward(CurrentNumber, unusedUndos: 0, beatParTime: false));
            LastStars = timer ? timer.StarsNow : placeholderStars;
            if (!testLevel)
            {
                // A timed BOSS cleared with 3 stars for the first time earns a Saturn's Gift.
                if (timer && timer.IsTimed && IsBoss && LastStars == 3 && SaveService.GetStars(CurrentIndex) < 3 && timer.settings)
                    Boosts.Grant(BoostType.SaturnsGift, timer.settings.bossThreeStarReward, BoostSource.BossReward);
                SaveService.RecordWin(CurrentIndex, LastStars);
            }
            if (autoAdvance) StartCoroutine(NextAfterDelay());
        }

        IEnumerator NextAfterDelay()
        {
            yield return new WaitForSeconds(nextLevelDelay);
            LoadNext();
        }
    }
}
