using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The "space / star" look: visit-order color palette plus every motion timing.
    /// Restrained by design — the level-completion burst is the only big moment.
    /// </summary>
    [CreateAssetMenu(fileName = "StarStyle", menuName = "One Line/Star Style")]
    public class StarStyle : ScriptableObject
    {
        [Header("Palette (visit order)")]
        [Tooltip("Colors nodes/segments step through as the path grows (first visit -> second-to-last).")]
        public Color[] visitColors =
        {
            Hex("#5ad1ff"), Hex("#7fd8ff"), Hex("#a78bff"), Hex("#c9a8ff"),
        };
        [Tooltip("The final node only.")]
        public Color finalColor = Hex("#f4d58d");
        public Color idleNode = new(0.80f, 0.85f, 0.95f, 0.9f);
        [Tooltip("Faint dashed line for connections not drawn yet.")]
        public Color idleEdge = new(1f, 1f, 1f, 0.14f);

        [Header("Background")]
        public Color backgroundCenter = Hex("#12172a");
        public Color backgroundEdge = Hex("#0a0d16");
        [Tooltip("How much the tier / equipped background color tints the center of the gradient.")]
        [Range(0f, 1f)] public float backgroundTint = 0.3f;

        [Header("Node motion")]
        public float breathePeriod = 2.6f;
        [Tooltip("Idle breathe: scale grows by this much at the peak (1.0 -> 1.06).")]
        public float breatheScale = 0.06f;
        [Tooltip("Idle breathe: opacity drops by this much at the peak (1.0 -> 0.85).")]
        public float breatheFade = 0.15f;
        public float popPeak = 1.22f;
        public float popRest = 1.06f;
        public float popUpTime = 0.08f;
        public float popSettleTime = 0.32f;
        public float sonarDuration = 1.3f;
        [Tooltip("Sonar ring end size, in node sizes.")]
        public float sonarScale = 3.2f;
        [Range(0f, 1f)] public float sonarAlpha = 0.55f;

        [Header("Star look (SDF shader)")]
        [Tooltip("OneLine/StarSDF. Without it (or on hardware that can't run it) stars fall back to the flat Art.Star sprite.")]
        public Shader starShader;
        [Tooltip("The constellation's main star (the start star, or the best-connected one on free-start levels): slightly bigger and warmer, with a stronger glint.")]
        public StarLook heroLook = StarLook.Hero();
        [Tooltip("Every other star: simpler and smaller.")]
        public StarLook minorLook = StarLook.Minor();
        [Tooltip("Main star size, in node sizes.")]
        public float heroScale = 1.25f;
        [Tooltip("How much an idle star's tips take on its idle color (0 = pure gold).")]
        [Range(0f, 1f)] public float idleTint = 0.15f;
        [Tooltip("How much a visited star's tips and glow take on its visit color.")]
        [Range(0f, 1f)] public float visitedTint = 0.8f;

        [Header("Star idle life")]
        [Tooltip("Seconds between a star's glints: random in this range, per star.")]
        public Vector2 glintInterval = new(4f, 10f);
        [Tooltip("Minor stars glint this much less often (2 = half as often).")]
        public float minorGlintSlowdown = 1.6f;
        public float glintDuration = 0.45f;

        [Header("Star pirouette (finger passes over)")]
        public bool pirouetteEnabled = true;
        [Tooltip("How close (world units) the finger must pass to set a star spinning. Visual only; snapping uses snapRadius.")]
        public float pirouetteHoverRadius = 0.38f;
        [Tooltip("Tilt onto one tip.")]
        public float pirouetteTiltTime = 0.12f;
        [Tooltip("One full turn around the vertical axis, eased in-out.")]
        public float pirouetteSpinTime = 0.6f;
        [Tooltip("Each star's spin time varies by up to this fraction (0.15 = ±15%).")]
        [Range(0f, 0.5f)] public float pirouetteSpinJitter = 0.15f;
        [Tooltip("Hop at the start of the spin, in star radii: each star gets a value in this range.")]
        public Vector2 pirouetteHop = new(0.1f, 0.24f);
        [Tooltip("Back to the resting rotation, with a squash & bounce.")]
        public float pirouetteSettleTime = 0.34f;
        [Range(0f, 0.5f)] public float pirouetteSquash = 0.16f;
        [Tooltip("Extra glow while spinning (multiplies the look's glow).")]
        public float pirouetteFlash = 0.8f;
        [Tooltip("After a pirouette ends, the star ignores the finger for this long.")]
        public float pirouetteCooldown = 0.8f;
        [Tooltip("Sparkles when the star lands: random count in this range.")]
        public Vector2Int sparkCount = new(3, 6);
        [Tooltip("Sparkle speed, in star sizes per second.")]
        public float sparkSpeed = 1.6f;
        public float sparkLifetime = 0.5f;
        [Tooltip("Sparkle size, in star sizes.")]
        public float sparkSize = 0.22f;

        [Header("Wand trail")]
        public float trailDrawTime = 0.3f;
        public float trailWidth = 0.055f;
        [Tooltip("Soft colored glow under each segment, as a multiple of the trail width.")]
        public float trailGlowWidth = 5f;
        [Range(0f, 1f)] public float trailGlowAlpha = 0.16f;
        [Tooltip("Dust particles emitted per segment while it draws.")]
        public int dustPerSegment = 10;
        public float idleEdgeWidth = 0.03f;
        [Tooltip("Dash + gap length of idle connections (world units).")]
        public float dashPeriod = 0.22f;

        [Header("Screen feedback")]
        [Tooltip("Whole-screen zoom punch on each correct move (1.015 = +1.5%).")]
        public float movePunch = 1.015f;
        public float movePunchTime = 0.22f;
        [Tooltip("Shake on a rejected move, in screen pixels.")]
        public float failShakePixels = 5f;
        public float failShakeTime = 0.22f;

        [Header("Level complete")]
        public int rayCount = 18;
        [Tooltip("Ray length at the end of the burst (world units).")]
        public float rayLength = 6f;
        public float rayWidth = 0.05f;
        public float burstTime = 1.1f;
        [Range(0f, 1f)] public float flashAlpha = 0.4f;
        [Tooltip("Warm flash end size (world units).")]
        public float flashSize = 16f;

        /// <summary>Color of the <paramref name="index"/>-th visited node (0-based) out of <paramref name="total"/>.</summary>
        public Color ForVisit(int index, int total)
        {
            if (total > 1 && index >= total - 1) return finalColor;
            if (visitColors.Length == 0) return Color.white;
            if (visitColors.Length == 1 || total <= 2) return visitColors[0];
            // Spread the stops over every node except the last.
            float t = index / (float)(total - 2) * (visitColors.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(t), visitColors.Length - 2);
            return Color.Lerp(visitColors[i], visitColors[i + 1], t - i);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // ---------- Star materials ----------

        [System.NonSerialized] Material heroMaterial, minorMaterial;

        /// <summary>True when stars can be drawn with the SDF shader.</summary>
        public bool HasStarShader => starShader && starShader.isSupported;
        public Material HeroMaterial => GetMaterial(ref heroMaterial, heroLook, "Star (hero)");
        public Material MinorMaterial => GetMaterial(ref minorMaterial, minorLook, "Star (minor)");

        Material GetMaterial(ref Material material, StarLook look, string materialName)
        {
            if (!HasStarShader) return null;
            if (!material) material = new Material(starShader) { name = materialName, hideFlags = HideFlags.DontSave };
            look.Apply(material, visitedTint);
            return material;
        }

        // Live tuning: inspector changes (also in Play mode) repaint the stars right away.
        void OnValidate()
        {
            if (heroMaterial) heroLook.Apply(heroMaterial, visitedTint);
            if (minorMaterial) minorLook.Apply(minorMaterial, visitedTint);
        }

        /// <summary>Stable hash of a level's name (string.GetHashCode isn't stable across runtimes).</summary>
        public static int Seed(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                if (text != null)
                    foreach (char ch in text) h = (h ^ ch) * 16777619;
                return (int)h;
            }
        }
    }

    /// <summary>Shape and light of one kind of star, pushed into the StarSDF material.</summary>
    [System.Serializable]
    public class StarLook
    {
        [Header("Shape")]
        [Range(3, 8)] public int points = 5;
        [Tooltip("Inner / outer radius: lower = slimmer, sharper arms.")]
        [Range(0.2f, 0.7f)] public float innerRatio = 0.4f;
        [Range(0f, 0.25f)] public float tipRoundness = 0.035f;
        [Tooltip("How much the edges between tips curve inward.")]
        [Range(0f, 0.25f)] public float edgeCurvature = 0.12f;

        [Header("Light")]
        public Color coreColor = new(1f, 0.98f, 0.92f);
        public Color tipColor = new(1f, 0.76f, 0.34f);
        [Range(0.05f, 1f)] public float coreSize = 0.3f;
        [Tooltip("How fast the white core gives way to the tip color.")]
        [Range(0.3f, 4f)] public float falloff = 2.2f;
        [Range(0f, 1f)] public float coreBoost = 0.2f;
        public Color rimColor = new(1f, 0.86f, 0.55f);
        [Range(0.005f, 0.3f)] public float rimWidth = 0.07f;
        [Range(0f, 2f)] public float rimIntensity = 0.6f;

        [Header("Glow")]
        public Color glowColor = new(1f, 0.8f, 0.45f);
        [Tooltip("Halo size, in star radii.")]
        [Range(0.2f, 3f)] public float glowRadius = 1.2f;
        [Range(0f, 2f)] public float glowIntensity = 0.4f;
        [Tooltip("0 = the halo only adds light; higher also darkens what's behind it a little.")]
        [Range(0f, 1f)] public float glowOpacity = 0.15f;
        [Tooltip("Extra glow at the peak of the idle breathe.")]
        [Range(0f, 2f)] public float breatheGlow = 0.5f;

        [Header("Glint")]
        [Range(0f, 3f)] public float glintIntensity = 1.2f;
        [Tooltip("Flare arm length, in star radii.")]
        [Range(0.5f, 3f)] public float glintLength = 2.4f;
        [Range(0.005f, 0.2f)] public float glintWidth = 0.045f;
        public Color glintColor = new(1f, 0.96f, 0.85f);
        [Tooltip("Light band sweeping across the star while it spins.")]
        [Range(0f, 1.5f)] public float sheenIntensity = 0.5f;

        public static StarLook Hero() => new()
        {
            tipColor = new Color(1f, 0.70f, 0.26f),
            glowColor = new Color(1f, 0.74f, 0.36f),
            coreBoost = 0.3f,
            glowRadius = 1.35f,
            glowIntensity = 0.5f,
            rimIntensity = 0.75f,
            glintIntensity = 1.5f,
            glintLength = 2.7f,
        };

        public static StarLook Minor() => new()
        {
            innerRatio = 0.42f,
            edgeCurvature = 0.1f,
            rimIntensity = 0.4f,
            glowRadius = 0.95f,
            glowIntensity = 0.26f,
            breatheGlow = 0.4f,
            glintIntensity = 0.9f,
            glintLength = 2.1f,
            sheenIntensity = 0.4f,
        };

        static readonly int PointsId = Shader.PropertyToID("_Points"), InnerRatioId = Shader.PropertyToID("_InnerRatio"),
            TipRoundId = Shader.PropertyToID("_TipRound"), EdgeCurveId = Shader.PropertyToID("_EdgeCurve"),
            CoreColorId = Shader.PropertyToID("_CoreColor"), TipColorId = Shader.PropertyToID("_TipColor"),
            CoreSizeId = Shader.PropertyToID("_CoreSize"), FalloffId = Shader.PropertyToID("_Falloff"),
            CoreBoostId = Shader.PropertyToID("_CoreBoost"), RimColorId = Shader.PropertyToID("_RimColor"),
            RimWidthId = Shader.PropertyToID("_RimWidth"), RimIntensityId = Shader.PropertyToID("_RimIntensity"),
            GlowColorId = Shader.PropertyToID("_GlowColor"), GlowRadiusId = Shader.PropertyToID("_GlowRadius"),
            GlowIntensityId = Shader.PropertyToID("_GlowIntensity"), GlowAlphaId = Shader.PropertyToID("_GlowAlpha"),
            BreatheGlowId = Shader.PropertyToID("_BreatheGlow"), GlintColorId = Shader.PropertyToID("_GlintColor"),
            GlintIntensityId = Shader.PropertyToID("_GlintIntensity"), GlintLengthId = Shader.PropertyToID("_GlintLength"),
            GlintWidthId = Shader.PropertyToID("_GlintWidth"), SheenId = Shader.PropertyToID("_SheenIntensity"),
            CanvasId = Shader.PropertyToID("_Canvas"), Anim2Id = Shader.PropertyToID("_Anim2");

        public void Apply(Material m, float visitedTint)
        {
            m.SetFloat(PointsId, points);
            m.SetFloat(InnerRatioId, innerRatio);
            m.SetFloat(TipRoundId, tipRoundness);
            m.SetFloat(EdgeCurveId, edgeCurvature);
            m.SetColor(CoreColorId, coreColor);
            m.SetColor(TipColorId, tipColor);
            m.SetFloat(CoreSizeId, coreSize);
            m.SetFloat(FalloffId, falloff);
            m.SetFloat(CoreBoostId, coreBoost);
            m.SetColor(RimColorId, rimColor);
            m.SetFloat(RimWidthId, rimWidth);
            m.SetFloat(RimIntensityId, rimIntensity);
            m.SetColor(GlowColorId, glowColor);
            m.SetFloat(GlowRadiusId, glowRadius);
            m.SetFloat(GlowIntensityId, glowIntensity);
            m.SetFloat(GlowAlphaId, glowOpacity);
            m.SetFloat(BreatheGlowId, breatheGlow);
            m.SetColor(GlintColorId, glintColor);
            m.SetFloat(GlintIntensityId, glintIntensity);
            m.SetFloat(GlintLengthId, glintLength);
            m.SetFloat(GlintWidthId, glintWidth);
            m.SetFloat(SheenId, sheenIntensity);
            m.SetFloat(CanvasId, Art.StarQuadRadii);
            // Stars without their own property block (e.g. the win sequence's) are tinted like visited stars.
            m.SetVector(Anim2Id, new Vector4(0f, 0f, 1f, visitedTint));
        }
    }
}
