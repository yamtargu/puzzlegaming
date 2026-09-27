using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Every timing and size of the level-complete "constellation fusion" (CompletionSequence): recognition, trace
    /// pulse, merge along the drawn path, glide, fusion flare, the zodiac sign rising with the level name, exit, and
    /// the next level's entrance. World sizes are in world units (a star is ~0.55).
    /// </summary>
    [CreateAssetMenu(fileName = "CompletionSettings", menuName = "One Line/Completion Settings")]
    public class CompletionSettings : ScriptableObject
    {
        [Header("Timing (seconds)")]
        public float recognitionTime = 0.15f;
        public float traceTime = 0.35f;
        public float flareTime = 0.3f;
        [Tooltip("Next level: the stars spread out from the center.")]
        public float emergeTime = 0.35f;
        [Tooltip("Reduce motion: only a short flare where the figure was (then the name).")]
        public float reduceMotionTime = 0.45f;

        [Header("1. Recognition")]
        [Tooltip("How far the stars brighten toward white.")]
        [Range(0f, 1f)] public float brighten = 0.35f;
        public float lineThicken = 1.35f;
        [Range(0f, 1f)] public float haloAlpha = 0.22f;
        [Tooltip("Halo size around the figure, as a multiple of the figure's size.")]
        public float haloScale = 1.5f;

        [Header("2. Trace pulse")]
        public float pulseSize = 0.9f;
        public float pingScale = 1.55f;
        public float pingTime = 0.2f;
        public float sparkleSize = 0.5f;

        [Header("3. Merge along the drawn path")]
        [Tooltip("Time per segment before clamping; the whole merge stays between min and max.")]
        public float perSegmentTime = 0.08f;
        public float minMergeTime = 0.8f;
        public float maxMergeTime = 1.4f;
        [Tooltip("Progress along the path (0-1) over the merge time (0-1): steeper at the end = the last segments go faster.")]
        public AnimationCurve mergeCurve = new(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.4f, 1.4f));
        [Tooltip("How long an absorbed star takes to slide into the orb.")]
        public float absorbTime = 0.12f;
        public float orbSize = 0.6f;
        [Tooltip("The orb grows by this share of its size once every star is absorbed.")]
        public float orbGrowth = 1.5f;
        public float streakTime = 0.14f;
        public float streakWidth = 0.08f;
        [Tooltip("Camera zoom at the moment of fusion (1.06 = 6% closer).")]
        public float cameraZoom = 1.06f;
        [Tooltip("The sky dims by this much behind the board.")]
        [Range(0f, 0.5f)] public float backgroundDim = 0.15f;

        [Header("Glide to the fusion point")]
        public float glideTime = 0.2f;
        [Tooltip("The fusion point: the figure's centroid, pulled this far toward the screen center.")]
        [Range(0f, 1f)] public float fusionCenterBias = 0.3f;

        [Header("4. Fusion + flare")]
        public float coreSize = 1.1f;
        public float flashSize = 5f;
        [Range(0f, 1f)] public float flashAlpha = 0.85f;
        public float ringSize = 7f;
        public float ringTime = 0.6f;
        [Range(0f, 1f)] public float ringAlpha = 0.7f;
        [Tooltip("Screen shake at the flare, in screen pixels.")]
        public float shakePixels = 5f;
        public float glyphSize = 1.3f;
        [Range(0f, 1f)] public float glyphAlpha = 0.9f;
        [Tooltip("Rays of the burst, as a share of StarStyle's ray length.")]
        public float rayLengthScale = 0.8f;

        [Header("5. The sign rises")]
        public float riseTime = 0.7f;
        [Tooltip("The glyph grows to this multiple of its (normal-level) flare size.")]
        public float riseScale = 2.2f;
        [Tooltip("The risen sign never gets taller than this share of the view.")]
        [Range(0.1f, 0.6f)] public float maxRiseViewShare = 0.3f;
        [Tooltip("How far it lifts (world units, toward the top of the screen).")]
        public float riseHeight = 0.6f;
        [Tooltip("Perspective cameras only: how far it also moves toward the camera.")]
        public float riseDepth = 0.8f;
        [Range(0f, 0.8f)] public float riseDim = 0.3f;
        public float glowPulsePeriod = 0.9f;
        [Range(0f, 1f)] public float raysAlpha = 0.16f;
        [Tooltip("Degrees per second.")]
        public float raysSpin = 14f;
        public int orbitSparkles = 4;
        [Tooltip("Degrees per second.")]
        public float sparkleOrbitSpeed = 60f;

        [Header("Name highlight")]
        public float nameFadeTime = 0.3f;
        public float nameStagger = 0.08f;
        [Tooltip("The shimmer starts this long after the rise begins and sweeps across in shimmerTime.")]
        public float shimmerDelay = 0.15f;
        public float shimmerTime = 0.5f;
        [Tooltip("The glow that follows the shimmer holds this long.")]
        public float nameHoldTime = 0.4f;
        public float signTextSize = 30f;
        public float signTextSpacing = 22f;
        public float nameTextSize = 70f;
        [Tooltip("Space between the glyph and the text (reference units).")]
        public float nameGap = 40f;
        public float nameRise = 24f;

        [Header("6. Exit")]
        public float exitTime = 0.35f;
        public int puffCount = 28;
        public float puffSpeed = 0.7f;

        [Header("Next level")]
        [Tooltip("Scale the new level starts from (its stars grow out of the center).")]
        [Range(0f, 1f)] public float emergeFrom = 0.08f;

        [Header("HARD / BOSS")]
        [Tooltip("Size and ray count multipliers.")]
        public float hardScale = 1.2f;
        public float bossScale = 1.5f;
        [Tooltip("BOSS flares this much longer and gets a second shockwave ring.")]
        public float bossFlareLength = 1.4f;
        public float secondRingDelay = 0.12f;
        [Tooltip("BOSS: the rising sign grows this much more and gets a second glow ring.")]
        public float bossRiseScale = 1.2f;
    }
}
