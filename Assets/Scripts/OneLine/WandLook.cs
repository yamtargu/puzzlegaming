using System;
using UnityEngine;

namespace OneLine
{
    /// <summary>What a wand does to the line; picks the WandLine shader mode and the dust behavior.</summary>
    public enum WandEffect { Classic, Stardust, Rainbow, Moonlight, Comet, GoldInk }

    public enum WandRarity { Common, Rare, Legendary }

    public enum DustShape { Soft, Sparkle, Star, Flake }

    /// <summary>
    /// Every value of one wand's look: the placed line (WandLine shader), the fairy dust that follows the finger,
    /// and the small burst when the line reaches a star. Used by WandTrail on the board and WandPreview in the shop.
    /// Assets live in Assets/Settings/Wands/ (created by One Line/Setup Levels + Scene, never overwritten).
    /// Looks only — nothing here can touch gameplay.
    /// </summary>
    [CreateAssetMenu(fileName = "WandLook", menuName = "One Line/Wand Look")]
    public class WandLook : ScriptableObject
    {
        [Header("Line")]
        [Tooltip("How much the line takes the path's colors (visit palette or equipped line color). 0 = only this look's colors.")]
        [Range(0f, 1f)] public float pathColorMix = 1f;
        public Color lineColor = Color.white;
        public Color glowColor = Color.white;
        [Tooltip("Core width (world units).")]
        public float coreWidth = 0.055f;
        [Tooltip("Full width of the soft glow around the core (world units).")]
        public float glowWidth = 0.28f;
        [Range(0f, 1f)] public float glowAlpha = 0.28f;
        [Tooltip("0 = the glow only adds light; 1 = normal blending (also darkens what's under it).")]
        [Range(0f, 1f)] public float glowOpacity = 0.6f;
        [Tooltip("Whiter center of the core.")]
        [Range(0f, 1f)] public float coreHighlight = 0.35f;
        [Tooltip("0 = crisp core edge; 1 = the core fades out softly (a glowing trail rather than a line).")]
        [Range(0f, 1f)] public float coreSoftness;

        [Header("Shimmer (Moonlight, Gold Ink)")]
        [Tooltip("Brightness added at the peak of a band of light sliding along the path.")]
        [Range(0f, 2f)] public float shimmerAmount = 0.6f;
        [Tooltip("Distance between the bands (world units).")]
        public float shimmerWavelength = 1.6f;
        [Tooltip("How fast the bands slide along the path (world units / s).")]
        public float shimmerSpeed = 0.5f;
        [Tooltip("Higher = narrower bands.")]
        public float shimmerSharpness = 6f;
        [Tooltip("Gold Ink: metallic shading across the width (a bright ridge, darker edges).")]
        [Range(0f, 1f)] public float metallic;

        [Header("Ink (Gold Ink): width follows the finger's speed")]
        [Tooltip("At or below this finger speed (world units / s) the stroke is thickest.")]
        public float inkSlowSpeed = 1.5f;
        [Tooltip("At or above this finger speed the stroke is thinnest.")]
        public float inkFastSpeed = 7f;
        [Tooltip("Core width multiplier at full speed.")]
        public float inkThin = 0.4f;
        [Tooltip("Core width multiplier when slow.")]
        public float inkThick = 1.8f;
        [Tooltip("Smoothing of the finger speed (seconds).")]
        public float inkSmoothing = 0.12f;

        [Header("Comet: a head at the finger with a tapering tail")]
        public Color cometHeadColor = new(0.92f, 0.97f, 1f);
        [Tooltip("Head size (world units).")]
        public float cometHeadSize = 0.3f;
        [Tooltip("Soft halo around the head, in head sizes.")]
        public float cometHaloScale = 2.6f;
        [Range(0f, 1f)] public float cometHaloAlpha = 0.35f;
        [Tooltip("Tail length, in seconds of finger movement.")]
        public float cometTailTime = 0.3f;
        [Tooltip("Tail width at the head (world units); it tapers to nothing.")]
        public float cometTailWidth = 0.16f;
        public Color cometTailColor = new(0.55f, 0.78f, 1f);
        [Tooltip("Head fade in / out when the finger lands / lifts (s).")]
        public float cometFadeTime = 0.15f;

        [Header("Sparkles along the line (Stardust)")]
        [Tooltip("Average distance between sparkles (world units).")]
        public float sparkleSpacing = 0.2f;
        [Tooltip("Arm length of a sparkle's 4-point glint (world units).")]
        public float sparkleSize = 0.09f;
        [Tooltip("Glints per second per sparkle (each has its own random phase and speed).")]
        public float sparkleRate = 0.45f;
        public float sparkleIntensity = 1.3f;
        public Color sparkleColor = Color.white;

        [Header("Hue flow (Rainbow)")]
        [Tooltip("Full hue cycles per world unit of path.")]
        public float hueCyclesPerUnit = 0.12f;
        [Tooltip("How fast the hue flows along the line (cycles per second).")]
        public float hueSpeed = 0.06f;
        [Tooltip("Low = pastel.")]
        [Range(0f, 1f)] public float hueSaturation = 0.45f;
        [Range(0f, 1f)] public float hueValue = 1f;

        [Header("Drag dust (follows the finger)")]
        public DustShape dustShape = DustShape.Soft;
        public Color[] dustColors = { new(1f, 0.86f, 0.5f), new(1f, 0.95f, 0.78f) };
        [Tooltip("How much each mote takes the path color instead of dustColors.")]
        [Range(0f, 1f)] public float dustLineColorMix;
        [Tooltip("Motes per world unit the finger moves.")]
        public float dustPerUnit = 12f;
        [Tooltip("Motes per second while the finger stands still.")]
        public float dustIdleRate = 0.6f;
        public int dustMaxPerFrame = 6;
        [Tooltip("Cap on live dust particles for this wand.")]
        public int dustMaxParticles = 160;
        [Tooltip("Start size range (world units).")]
        public Vector2 dustSize = new(0.06f, 0.13f);
        [Tooltip("Lifetime range (s).")]
        public Vector2 dustLifetime = new(0.6f, 1.1f);
        [Tooltip("Random speed off the finger (world units / s).")]
        public float dustScatter = 0.2f;
        [Tooltip("Steady drift (world units / s): up = rising, down = falling.")]
        public Vector2 dustDrift = new(0f, 0.08f);
        [Tooltip("Downward pull (world units / s²).")]
        public float dustGravity;
        [Tooltip("Speed back along the finger's path (a comet's tail).")]
        public float dustTrailBack;
        [Tooltip("Stretches motes along their motion (0 = round).")]
        public float dustStretch;
        [Tooltip("Motes flicker as they drift (glitter).")]
        public bool dustTwinkle;
        [Tooltip("Spin speed range (degrees / s, random direction). 0 = no spin.")]
        public Vector2 dustSpin;
        [Tooltip("Flakes tumble (flip over) this many times per second. 0 = flat.")]
        public float dustFlip;
        [Tooltip("Side-to-side flutter (world units / s). 0 = none.")]
        public float dustFlutter;
        [Tooltip("How quickly the flutter changes direction (per second).")]
        public float dustFlutterFrequency = 0.9f;

        [Header("Reach burst (the line reaches a star)")]
        public DustShape burstShape = DustShape.Sparkle;
        public Vector2Int burstCount = new(10, 14);
        [Tooltip("Burst speed (world units / s).")]
        public float burstSpeed = 1.1f;
        [Tooltip("Under 0.5 s.")]
        public float burstLifetime = 0.4f;
        public Vector2 burstSize = new(0.06f, 0.12f);
        public Color[] burstColors = { new(1f, 0.9f, 0.6f) };
        [Range(0f, 1f)] public float burstLineColorMix = 0.5f;
        [Tooltip("A quick flash of light on the star (world units; 0 = none).")]
        public float burstFlashSize;
        public Color burstFlashColor = Color.white;
        public float burstFlashTime = 0.2f;

        static WandLook builtIn;

        /// <summary>The Classic look with default values, for wands without a look asset.</summary>
        public static WandLook Default
        {
            get
            {
                if (builtIn) return builtIn;
                builtIn = CreateInstance<WandLook>();
                builtIn.hideFlags = HideFlags.DontSave;
                return builtIn;
            }
        }

        /// <summary>The look of <paramref name="wand"/>, or the default one.</summary>
        public static WandLook Of(CosmeticCatalog.Wand wand) => wand != null && wand.look ? wand.look : Default;

        /// <summary>Raised when a look is edited in the inspector (live tuning in Play mode).</summary>
        public static event Action<WandLook> Changed;
        void OnValidate() => Changed?.Invoke(this);

        /// <summary>Pastel rainbow color at <paramref name="pathDistance"/> along the path — same formula as the shader.</summary>
        public Color Hue(float pathDistance, float time)
        {
            float h = pathDistance * hueCyclesPerUnit - time * hueSpeed;
            return Color.HSVToRGB(h - Mathf.Floor(h), hueSaturation, hueValue);
        }

        /// <summary>The line's own color at a point (before mixing in the path color).</summary>
        public Color LineColor(WandEffect effect, float pathDistance, float time) =>
            effect == WandEffect.Rainbow ? Hue(pathDistance, time) : lineColor;

        /// <summary>A dust mote's color: the rainbow hue where it's born, or one of dustColors, mixed toward the path color.</summary>
        public Color DustColor(WandEffect effect, float pathDistance, float time, Color pathColor)
        {
            var c = effect == WandEffect.Rainbow ? Hue(pathDistance, time) : Pick(dustColors);
            return Color.Lerp(c, pathColor, dustLineColorMix);
        }

        public Color BurstColor(WandEffect effect, float pathDistance, float time, Color pathColor)
        {
            var c = effect == WandEffect.Rainbow ? Hue(pathDistance, time) : Pick(burstColors);
            return Color.Lerp(c, pathColor, burstLineColorMix);
        }

        static Color Pick(Color[] colors) =>
            colors == null || colors.Length == 0 ? Color.white : colors[UnityEngine.Random.Range(0, colors.Length)];

        /// <summary>Gold Ink: core width multiplier for a finger speed (world units / s) — fast = thin, slow = thick.</summary>
        public float InkWidth(float speed) => Mathf.Lerp(inkThick, inkThin, Mathf.InverseLerp(inkSlowSpeed, inkFastSpeed, speed));

        /// <summary>Brightness added by the shimmer bands at this point of the path — same formula as the shader.</summary>
        public float Shimmer(float pathDistance, float time)
        {
            float phase = (pathDistance - time * shimmerSpeed) / Mathf.Max(1e-3f, shimmerWavelength);
            phase -= Mathf.Floor(phase);
            return shimmerAmount * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase * 2f * Mathf.PI), Mathf.Max(1f, shimmerSharpness));
        }

        /// <summary>The particle sprite for a dust shape.</summary>
        public static Sprite Sprite(DustShape shape) => shape switch
        {
            DustShape.Sparkle => UIKit.Sparkle,
            DustShape.Star => Art.Star,
            DustShape.Flake => Art.Flake,
            _ => Art.Mote,
        };

        static readonly int ModeId = Shader.PropertyToID("_Mode"), LineColorId = Shader.PropertyToID("_LineColor"),
            GlowColorId = Shader.PropertyToID("_GlowColor"), PathMixId = Shader.PropertyToID("_PathMix"),
            CoreWidthId = Shader.PropertyToID("_CoreWidth"), GlowAlphaId = Shader.PropertyToID("_GlowAlpha"),
            GlowOpacityId = Shader.PropertyToID("_GlowOpacity"), HighlightId = Shader.PropertyToID("_CoreHighlight"),
            SparkleSpacingId = Shader.PropertyToID("_SparkleSpacing"), SparkleSizeId = Shader.PropertyToID("_SparkleSize"),
            SparkleRateId = Shader.PropertyToID("_SparkleRate"), SparkleIntensityId = Shader.PropertyToID("_SparkleIntensity"),
            SparkleColorId = Shader.PropertyToID("_SparkleColor"), HueId = Shader.PropertyToID("_Hue"),
            LinearId = Shader.PropertyToID("_Linear"), SoftnessId = Shader.PropertyToID("_CoreSoftness"),
            ShimmerId = Shader.PropertyToID("_Shimmer"), MetallicId = Shader.PropertyToID("_Metallic");

        /// <summary>Pushes the line values into a WandLine material.</summary>
        public void ApplyLine(Material m, WandEffect effect)
        {
            m.SetFloat(ModeId, (float)effect);
            m.SetColor(LineColorId, lineColor);
            m.SetColor(GlowColorId, glowColor);
            m.SetFloat(PathMixId, pathColorMix);
            m.SetFloat(CoreWidthId, coreWidth);
            m.SetFloat(GlowAlphaId, glowAlpha);
            m.SetFloat(GlowOpacityId, glowOpacity);
            m.SetFloat(HighlightId, coreHighlight);
            m.SetFloat(SparkleSpacingId, Mathf.Max(0.02f, sparkleSpacing));
            m.SetFloat(SparkleSizeId, sparkleSize);
            m.SetFloat(SparkleRateId, sparkleRate);
            m.SetFloat(SparkleIntensityId, sparkleIntensity);
            m.SetColor(SparkleColorId, sparkleColor);
            m.SetVector(HueId, new Vector4(hueCyclesPerUnit, hueSpeed, hueSaturation, hueValue));
            m.SetFloat(LinearId, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f);
            m.SetFloat(SoftnessId, coreSoftness);
            m.SetVector(ShimmerId, new Vector4(shimmerAmount, shimmerWavelength, shimmerSpeed, shimmerSharpness));
            m.SetFloat(MetallicId, metallic);
        }

        static AnimationCurve shrinkCurve, twinkleCurve;

        /// <summary>Size over a mote's life: shrinks as it fades.</summary>
        public static AnimationCurve ShrinkCurve => shrinkCurve ??= AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        /// <summary>Glitter: each mote flickers a few times as it fades (motes are born at different times, so they never sync).</summary>
        public static AnimationCurve TwinkleCurve => twinkleCurve ??= new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.1f, 1.2f), new Keyframe(0.2f, 0.45f), new Keyframe(0.32f, 1.1f),
            new Keyframe(0.45f, 0.4f), new Keyframe(0.6f, 1f), new Keyframe(0.75f, 0.35f), new Keyframe(0.88f, 0.7f),
            new Keyframe(1f, 0f));

        /// <summary>Flakes: width over a mote's life, flipping over <paramref name="flips"/> times and shrinking as it fades.</summary>
        public static AnimationCurve FlipCurve(float flips)
        {
            const int keys = 32;
            var curve = new AnimationCurve();
            for (int i = 0; i <= keys; i++)
            {
                float t = i / (float)keys;
                float face = Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(Mathf.PI * flips * t))); // edge-on never quite vanishes
                curve.AddKey(t, face * ShrinkCurve.Evaluate(t));
            }
            return curve;
        }

        /// <summary>Size over a flash's life: swells as it fades.</summary>
        public static AnimationCurve FlashCurve => flashCurve ??= AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.25f);
        static AnimationCurve flashCurve;

        /// <summary>Half the width of a segment's geometry: room for the glow, the thickest ink and the sparkles.</summary>
        public float HalfWidth(WandEffect effect)
        {
            float core = coreWidth * (effect == WandEffect.GoldInk ? Mathf.Max(1f, inkThick) : 1f);
            float sparkles = effect == WandEffect.Stardust ? sparkleSize * 2.2f : 0f;
            return Mathf.Max(glowWidth, core + 0.02f, sparkles) * 0.5f;
        }
    }
}
