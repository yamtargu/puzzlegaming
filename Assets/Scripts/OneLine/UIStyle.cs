using TMPro;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Every visual value of the UI (fonts, sizes, colors, spacing, timings) for the night-sky HUD, shop,
    /// settings and win screen. Base colors come from StarStyle; the per-tier accent comes from ThemeSet.
    /// </summary>
    [CreateAssetMenu(fileName = "UIStyle", menuName = "One Line/UI Style")]
    public class UIStyle : ScriptableObject
    {
        public enum BottomRowMode { TierProgress, StarsInLevel }

        [Header("Links")]
        [Tooltip("Navy, gold (finalColor) and the palette come from here.")]
        public StarStyle starStyle;
        [Tooltip("Constellation-like names shown as level titles (used when no Zodiac theme is set).")]
        public LevelNames levelNames;
        [Tooltip("Astrology theme: level names per zodiac house, title cartouche, background, house transition.")]
        public ZodiacTheme zodiac;

        [Header("Fonts (empty = TMP default font)")]
        [Tooltip("Titles / level name. Cinzel (or Cormorant Garamond).")]
        public TMP_FontAsset titleFont;
        [Tooltip("Labels, numbers, buttons. Quicksand (or Nunito).")]
        public TMP_FontAsset bodyFont;

        [Header("Colors")]
        [Tooltip("Never pure white.")]
        public Color text = new(0.918f, 0.941f, 1f); // #EAF0FF
        [Range(0f, 1f)] public float labelAlpha = 0.6f;
        public Color panelFill = new(0.071f, 0.090f, 0.165f, 0.8f); // #12172a @ 80%
        [Tooltip("Opacity of modal cards (shop, settings, win) — higher than panelFill so what's behind doesn't read through.")]
        [Range(0f, 1f)] public float modalCardAlpha = 0.98f;
        public Color outline = new(1f, 1f, 1f, 0.2f);
        public Color glassFill = new(0.05f, 0.07f, 0.13f, 0.55f);
        [Range(0f, 1f)] public float glassInnerGlow = 0.07f;
        [Range(0f, 1f)] public float dimAlpha = 0.6f;
        [Range(0f, 1f)] public float badgeFillAlpha = 0.15f;

        [Header("Layout (reference 1080 x 1920)")]
        public float edgeMargin = 44f;
        [Tooltip("Height kept for the top HUD block (below the safe area).")]
        public float topBand = 400f;
        [Tooltip("Height kept for the bottom buttons + progress row (above the safe area).")]
        public float bottomBand = 250f;
        public float roundButton = 120f;
        public float topButton = 96f;
        public float iconSize = 46f;
        public float panelRadius = 32f;
        public float outlineWidth = 1.5f;

        [Header("Level title")]
        public float levelLabelSize = 28f;
        public float levelLabelSpacing = 22f;
        public float titleSize = 84f;
        public float titleSpacing = 4f;
        [Tooltip("Letter spacing at the start of the entrance animation (tightens to titleSpacing).")]
        public float titleSpacingStart = 18f;
        public float tierLineSize = 24f;
        public float tierLineSpacing = 16f;
        public float ornamentLength = 110f;
        [Range(0f, 1f)] public float titleGlow = 0.35f;

        [Header("Badge")]
        public float badgeTextSize = 24f;
        public float badgeHeight = 52f;
        public float badgePulsePeriod = 2.4f;
        public float orbitSpeed = 50f;

        [Header("Coins")]
        public float coinPillWidth = 220f;
        public float coinTextSize = 40f;
        public float coinCountTime = 0.6f;
        public float coinPunch = 1.15f;

        [Header("Bottom row")]
        public BottomRowMode bottomRow = BottomRowMode.TierProgress;
        public float progressStarSize = 26f;
        public float progressSpacing = 52f;

        [Header("Motion")]
        public float titleInTime = 0.5f;
        public float titleRise = 20f;
        public float pressScale = 0.92f;
        public float releaseOvershoot = 1.05f;
        public float pressTime = 0.08f;
        public float releaseTime = 0.25f;
        public float panelOpenTime = 0.28f;
        public float panelScaleFrom = 0.9f;
        [Tooltip("Pause after a win before the win screen appears (the completion burst plays first).")]
        public float winScreenDelay = 0.9f;
        public float stagger = 0.06f;
        public float winTitleGoldTime = 0.35f;
        [Tooltip("On a win, a band of gold light sweeps across the level name this fast.")]
        public float titleShimmerTime = 0.6f;
        [Tooltip("Soft half-width of the shimmer band, as a share of the text width.")]
        public float shimmerWidth = 0.16f;

        [Header("Screens (Home ⇄ Map ⇄ Game)")]
        public float screenFadeTime = 0.3f;
        [Tooltip("An incoming screen starts at this scale; an outgoing one grows by the same amount.")]
        public float screenScaleFrom = 0.96f;
        public float boardScaleFrom = 0.94f;
        [Tooltip("How long the sky takes to drift between the home and the game framing.")]
        public float skyBlendTime = 0.9f;
        [Tooltip("Back swipe: starts this close to the left edge and travels at least this far (reference units).")]
        public float backSwipeEdge = 40f;
        public float backSwipeDistance = 220f;

        [Header("Home")]
        public string gameTitle = "One-Line";
        public string gameSubtitle = "Path";
        [Tooltip("Rows of the home screen, as a share of the safe area's height from the bottom.")]
        public float homeLogoY = 0.74f;
        public float homeProgressY = 0.56f;
        public float homePlayY = 0.40f;
        public float homeSecondaryY = 0.28f;
        public float homePremiumY = 0.13f;
        public float logoSize = 150f;
        public float logoSpacing = 4f;
        public float logoSubtitleSize = 50f;
        public float logoSubtitleSpacing = 42f;
        public float logoSubtitleGap = 118f;
        [Tooltip("Stars of the logo constellation: x = letter index, y = across the letter (0-1), z = up the letter (0-1).")]
        public Vector3[] logoConstellation =
        {
            new(0f, 0.18f, 0.78f), new(1f, 0.55f, 0.12f), new(2f, 0.7f, 0.95f), new(4f, 0.3f, 0.08f),
            new(5f, 0.5f, 1.05f), new(6f, 0.45f, 0.2f), new(7f, 0.9f, 0.85f),
        };
        public float logoLineWidth = 2f;
        public float logoGlowWidth = 18f;
        public float logoStarSize = 30f;
        public float logoDelay = 0.35f;
        public float logoDrawTime = 1.8f;
        public float logoStarPopTime = 0.35f;
        [Tooltip("Twinkle speed of the logo stars once drawn (radians per second).")]
        public float logoTwinkleSpeed = 1.4f;
        public float homeSignSize = 30f;
        public int progressBarStars = 24;
        public float progressBarWidth = 620f;
        public float progressBarStarSize = 18f;
        public float progressBarY = -62f;
        public float progressLabelSize = 24f;
        public Vector2 playButtonSize = new(660f, 150f);
        public float playTextSize = 44f;
        public float playGlowPeriod = 2.6f;
        public Vector2 secondaryButtonSize = new(300f, 128f);
        public float secondaryGap = 40f;
        public float secondaryTextSize = 34f;
        public Vector2 premiumButtonSize = new(300f, 92f);
        public float homeIconSize = 48f;
        [Range(0f, 1f)] public float homeDimStarAlpha = 0.22f;
        public float wiggleDegrees = 5f;
        public float wiggleTime = 0.35f;

        [Header("Sign line (— ♉ TAURUS · … —)")]
        public float signGlyphGap = 12f;
        public float signGlyphStroke = 1.8f;
        public float signLabelBox = 1000f;
        public float ornamentGap = 18f;
        [Range(0f, 1f)] public float ornamentAlpha = 0.66f;

        [Header("Panels")]
        public float panelMedallionRadius = 46f;
        [Tooltip("The panel frame draws itself while the panel opens.")]
        public float panelDrawTime = 0.5f;
        public float panelTitleSize = 60f;
        public float panelTitleY = 150f;
        public float closeButtonSize = 84f;
        public float closeButtonInset = 76f;
        public Vector2 settingsSize = new(880f, 1360f);
        [Tooltip("First settings row, below the card's top edge.")]
        public float settingsFirstRowY = 270f;
        public float settingsRowHeight = 104f;
        public float settingsRowWidth = 700f;
        [Tooltip("Extra space between the switches and the links.")]
        public float settingsSectionGap = 36f;
        public float settingsTextSize = 36f;
        public float settingsFooterY = 90f;
        public float footerTextSize = 22f;
        [Range(0f, 1f)] public float rowLineAlpha = 0.14f;
        public float hairline = 1.5f;
        public float shopHeaderGap = 70f;
        public Vector2 premiumSize = new(900f, 1120f);
        public float perkTextSize = 32f;
        public float perkSpacing = 92f;
        public Vector2 buyButtonSize = new(560f, 128f);

        [Header("Shop: wands")]
        public Color rarityCommon = new(0.80f, 0.85f, 0.94f);
        public Color rarityRare = new(0.48f, 0.74f, 1f);
        public Color rarityLegendary = new(1f, 0.78f, 0.38f);
        public float wandRowHeight = 150f;
        [Tooltip("The patch of night sky behind a wand preview.")]
        public Color wandPreviewSky = new(0.02f, 0.03f, 0.07f, 0.55f);
        public Vector2 wandPreviewSize = new(250f, 120f);
        public Vector2 wandBigPreviewSize = new(780f, 420f);
        [Tooltip("World units shown across a preview's height (sets how big the line and dust look in it).")]
        public float wandPreviewUnits = 1.3f;
        [Tooltip("Preview loop: draw the line, hold, fade out, start over (seconds).")]
        public float wandPreviewDrawTime = 1.8f;
        public float wandPreviewHold = 0.8f;
        public float wandPreviewFade = 0.4f;

        public Color RarityColor(WandRarity rarity) => rarity switch
        {
            WandRarity.Legendary => rarityLegendary,
            WandRarity.Rare => rarityRare,
            _ => rarityCommon,
        };

        [Header("Celestial switch")]
        public Vector2 switchSize = new(132f, 68f);
        public float switchKnobInset = 7f;
        public float switchTime = 0.22f;
        public Color moonColor = new(0.78f, 0.84f, 0.96f);
        [Range(0f, 1f)] public float switchOnFillAlpha = 0.22f;
        [Range(0f, 1f)] public float switchOnOutlineAlpha = 0.8f;

        [Header("Level map (The Zodiac Path)")]
        public string mapTitle = "The Zodiac Path";
        public float mapTitleSize = 44f;
        [Tooltip("Height of the top bar (back button, title, coins) above the scrolling chart.")]
        public float mapTopBar = 150f;
        [Tooltip("Vertical distance between two levels on the path.")]
        public float mapNodeSpacing = 170f;
        [Tooltip("Extra space where a house begins (its header sits there).")]
        public float mapHouseGap = 400f;
        public float mapBottomPad = 120f;
        public float mapTopPad = 420f;
        [Tooltip("Sideways swing of the path (reference units) and levels per full swing.")]
        public float mapAmplitude = 250f;
        public float mapWaveLevels = 9f;
        [Tooltip("Random sideways offset per level, as a share of the amplitude.")]
        [Range(0f, 0.5f)] public float mapJitter = 0.12f;
        public int mapSeed = 12;
        public float mapLineWidth = 2.2f;
        public float mapGlowWidth = 18f;
        [Range(0f, 1f)] public float mapLockedLineAlpha = 0.22f;
        public float mapDash = 12f;
        public float mapDashGap = 12f;
        public float mapStarSize = 46f;
        public float mapCurrentSize = 66f;
        public float mapLockedSize = 16f;
        public Color mapLockedColor = new(0.55f, 0.62f, 0.78f, 0.5f);
        public float mapHaloSize = 190f;
        public float mapHardRing = 34f;
        public float mapBossRadius = 42f;
        public float mapPipSize = 15f;
        public float mapPipGap = 17f;
        public float mapPipY = -40f;
        public float mapNumberSize = 22f;
        public float mapNumberX = 58f;
        public float mapPulsePeriod = 1.8f;
        public float mapCometOrbit = 58f;
        public float mapCometSpeed = 70f;
        public float mapShimmerSpeed = 240f;
        public float mapShimmerSize = 40f;
        public int mapShimmers = 3;
        [Tooltip("Nodes are built only this far outside the view (reference units).")]
        public float mapPoolMargin = 500f;
        [Tooltip("Levels per pooled path piece.")]
        public int mapChunk = 10;
        public float mapScrollTime = 0.6f;
        [Tooltip("The glide to the current level starts this far below it (share of the view height).")]
        [Range(0f, 2f)] public float mapScrollLead = 0.8f;
        [Tooltip("Background constellations move this much slower than the path (1 = with it).")]
        [Range(0f, 1f)] public float mapParallax = 0.55f;
        public float mapFigureSize = 820f;
        [Range(0f, 0.3f)] public float mapFigureAlpha = 0.08f;
        [Range(0f, 0.3f)] public float mapFigureLockedAlpha = 0.035f;
        public Vector2 mapHeaderSize = new(640f, 150f);
        public float mapHeaderTextSize = 34f;
        public float mapHeaderSubSize = 24f;
        public float mapHeaderMedallion = 36f;
        [Range(0f, 1f)] public float mapHeaderLockedAlpha = 0.4f;
        public float mapRevealTime = 1.8f;
        public Vector2 mapPopupSize = new(760f, 700f);
        public Vector2 mapPlayButtonSize = new(400f, 116f);
        public string mapComingSoon = "More of the sky soon";

        [Header("In-game map button & win panel")]
        public float hudButtonGap = 20f;
        public Vector2 winButtonSize = new(300f, 106f);
        public float winButtonGap = 30f;

        [Header("Celebration burst (purchases)")]
        public int burstRays = 16;
        public float burstTime = 1.1f;
        public float burstRadius = 420f;

        /// <summary>Warm gold of the final star — buttons, coins, highlights.</summary>
        public Color Gold => starStyle ? starStyle.finalColor : new Color(0.96f, 0.84f, 0.55f);

        static ZodiacTheme defaultZodiac;

        /// <summary>The zodiac theme, or a default one when none is assigned.</summary>
        public ZodiacTheme Zodiac =>
            zodiac ? zodiac : defaultZodiac ? defaultZodiac : defaultZodiac = CreateInstance<ZodiacTheme>();

        /// <summary>Title of a level: zodiac name, else the constellation list, else "Level n".
        public string LevelTitle(int levelNumber)
        {
            if (zodiac && zodiac.names) return zodiac.names.Get(levelNumber);
            if (levelNames) return levelNames.Get(levelNumber);
            return $"Level {levelNumber}";
        }
    }
}
