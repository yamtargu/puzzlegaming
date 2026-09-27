using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The astrology look: an antique celestial chart come to life. Holds every color, size, alpha and timing of the
    /// title cartouche, the zodiac background and the house transition. Background alphas are capped at 0.10 so the
    /// sky never competes with the puzzle.
    /// </summary>
    [CreateAssetMenu(fileName = "ZodiacTheme", menuName = "One Line/Zodiac Theme")]
    public class ZodiacTheme : ScriptableObject
    {
        [Header("Links")]
        public ZodiacNames names;
        public ZodiacConstellations constellations;
        [Tooltip("Unlit sprite material for the background art (a saved asset, so the shader ships in builds).")]
        public Material backgroundMaterial;

        [Header("Colors")]
        [Tooltip("Antique gold linework.")]
        public Color gold = Hex("#C9A66B");
        [Range(0f, 1f)] public float frameAlpha = 0.7f;
        [Tooltip("Nebula tint per element: fire, earth, air, water.")]
        public Color[] elementTint =
        {
            Hex("#D98E4A"), // fire: warm amber
            Hex("#6E8F5A"), // earth: moss green
            Hex("#B7A8E0"), // air: pale lavender
            Hex("#4FA3A0"), // water: teal
        };
        [Tooltip("Light accent per element (HARD badge, name glow): fire, earth, air, water.")]
        public Color[] elementAccent =
        {
            Hex("#F0B27A"), Hex("#A9C98F"), Hex("#CFC4F2"), Hex("#8FD3CF"),
        };

        [Header("Title cartouche — shape (reference units)")]
        public float outerLine = 1.5f;
        public float innerLine = 0.75f;
        [Tooltip("Gap between the outer and inner line.")]
        public float lineGap = 8f;
        [Tooltip("Radius of the concave (notched) corners.")]
        public float cornerNotch = 26f;
        [Tooltip("Soft glow around the outer line: width and opacity.")]
        public float glowWidth = 16f;
        [Range(0f, 1f)] public float glowAlpha = 0.14f;
        public float medallionRadius = 40f;
        [Tooltip("Space between the medallion and where the frame lines stop.")]
        public float medallionGap = 10f;
        public float crescentSize = 28f;
        public float crescentGap = 22f;
        public float cornerStarSize = 22f;
        [Tooltip("Dot–line–dot motif outside the left/right sides.")]
        public float sideMotifLength = 40f;
        [Tooltip("Dark fill inside the title frame, so the sky doesn't show through the text.")]
        [Range(0f, 1f)] public float titleFillAlpha = 0.82f;
        [Range(0f, 0.2f)] public float starMapAlpha = 0.05f;
        [Tooltip("Star-map texture inset from the outer line.")]
        public float starMapInset = 14f;
        public float glyphStroke = 2.4f;
        [Tooltip("Medallion: dial line width, tick length (quarter ticks are 1.6x), disc and glow opacity.")]
        public float dialWidth = 1.3f;
        public float dialTick = 6f;
        [Range(0f, 1f)] public float medallionDiscAlpha = 0.92f;
        [Range(0f, 1f)] public float medallionGlowAlpha = 0.1f;

        [Header("Title cartouche — layout (y from the frame's top line, downward)")]
        [Tooltip("Frame top line, below the top buttons.")]
        public float frameTopBelowButtons = 16f;
        public float frameHeight = 236f;
        public float labelY = 66f;
        public float nameY = 130f;
        public float tierY = 196f;
        [Tooltip("HARD / BOSS badge center, below the frame's bottom line.")]
        public float badgeBelowFrame = 50f;
        [Tooltip("Horizontal padding between the text and the frame.")]
        public float framePadding = 56f;
        public float minFrameWidth = 600f;
        public float tierGlyphSize = 30f;

        [Header("Title cartouche — motion")]
        [Tooltip("Frame lines draw from the medallion outward and around.")]
        public float drawTime = 0.6f;
        public float medallionInTime = 0.25f;
        public float cornerStagger = 0.06f;
        public float cornerTwinkleTime = 0.45f;
        public float winGlowTime = 1.1f;
        [Range(0f, 1f)] public float winGlowAlpha = 0.5f;
        public float medallionTurn = 30f;
        public float medallionTurnTime = 1.1f;

        [Header("Background (all ≤ 10% alpha, desaturated, slow)")]
        [Tooltip("The project renders in Linear color space, where a faint overlay reads brighter than its alpha — " +
                 "keep these well under the 10% cap.")]
        [Range(0f, 0.1f)] public float constellationAlpha = 0.05f;
        [Tooltip("Constellation figure width, as a share of the screen height.")]
        public float constellationSize = 0.75f;
        [Range(0f, 0.1f)] public float wheelAlpha = 0.03f;
        [Range(0f, 0.1f)] public float wheelHighlightAlpha = 0.035f;
        [Tooltip("Wheel diameter, as a share of the screen height.")]
        public float wheelSize = 0.85f;
        [Tooltip("Seconds per full turn of the wheel.")]
        public float wheelTurnSeconds = 600f;
        [Range(0f, 0.1f)] public float planetAlpha = 0.035f;
        public int planetCount = 5;
        [Tooltip("Planet glyph size, as a share of the screen height.")]
        public float planetSize = 0.035f;
        [Tooltip("Drift speed, screen heights per second.")]
        public float planetDrift = 0.004f;
        [Range(0f, 0.1f)] public float nebulaAlpha = 0.06f;
        [Range(0f, 0.1f)] public float shootingStarAlpha = 0.08f;
        [Tooltip("Seconds between shooting stars (random in this range).")]
        public Vector2 shootingStarInterval = new(25f, 60f);
        public float elementFadeTime = 1.5f;

        [Header("Home screen sky (brighter wheel behind the logo)")]
        [Tooltip("Linear color space: 0.1 already reads like ~20% on screen.")]
        [Range(0f, 0.25f)] public float homeWheelAlpha = 0.1f;
        [Range(0f, 0.25f)] public float homeHighlightAlpha = 0.045f;
        [Tooltip("Wheel diameter on the home screen, as a share of the screen height.")]
        public float homeWheelSize = 0.6f;
        [Tooltip("Wheel center on the home screen, as a share of the screen half-height above the middle.")]
        public float homeWheelY = 0.48f;
        [Tooltip("The sky is this much closer on the home screen; entering a level eases it back.")]
        public float homeZoom = 1.06f;
        public Vector2 homeShootingStarInterval = new(8f, 18f);

        [Header("House transition")]
        public float transitionIn = 0.45f;
        public float transitionHold = 1.1f;
        public float transitionOut = 0.45f;
        public float transitionSkipOut = 0.25f;
        [Tooltip("The background wheel turning to the new sign.")]
        public float wheelTurnTime = 1.6f;
        public float transitionGlyphSize = 200f;
        [Range(0f, 1f)] public float transitionDim = 0.9f;

        public Color Tint(Zodiac.Element e) => Pick(elementTint, (int)e);
        public Color Accent(Zodiac.Element e) => Pick(elementAccent, (int)e);
        public Color Line => UIKit.WithAlpha(gold, frameAlpha);

        static Color Pick(Color[] colors, int i) =>
            colors == null || colors.Length == 0 ? Color.white : colors[Mathf.Clamp(i, 0, colors.Length - 1)];

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
