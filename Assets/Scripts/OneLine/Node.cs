using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// A star in the puzzle graph: its neighbors, visited state and motion.
    /// Idle: slow breathe (scale + opacity + glow) and an occasional glint. Visited: quick settle pop to a resting
    /// size, plus a sonar ring. When the finger passes over it: a "pirouette" on one tip (StarSDF shader only,
    /// drawn inside the quad, so the transform never moves). Each star has a seeded personality: which tip it
    /// stands on, spin direction and speed, hop height, glint timing. All visual only.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Node : MonoBehaviour
    {
        public int Index { get; private set; }
        public bool Visited { get; private set; }
        public readonly List<Node> Neighbors = new();

        /// <summary>World position (the board may be tilted, and the node may float above it).</summary>
        public Vector3 Position => transform.position;
        /// <summary>Where the node sits on the board plane, in board-local space (z = 0). Lines attach here.</summary>
        public Vector3 BoardPoint { get; private set; }
        /// <summary>The color the node shows right now (idle color, or its visit color).</summary>
        public Color CurrentColor => Visited ? visitedColor : idleColor;

        SpriteRenderer sr;
        StarStyle style;
        Color idleColor;
        Color visitedColor;
        Vector3 baseScale;
        float breathePhase;
        float visitTime = -1f;

        // StarSDF look (false = flat Art.Star fallback: no glow, glint or pirouette).
        bool sdf;
        bool hero;
        uint rng; // per-star xorshift state, seeded from the level + index
        float nextGlint, glintStart = -1f;

        // Pirouette personality (fixed per star) and state.
        float tiltTarget, spinDir, spinScale, hopHeight, glintAngle;
        float pirouetteStart = -1f, readyAt;
        bool landed, fingerOver;

        static MaterialPropertyBlock block;
        static readonly int AnimId = Shader.PropertyToID("_Anim"), Anim2Id = Shader.PropertyToID("_Anim2"),
            Anim3Id = Shader.PropertyToID("_Anim3");

        /// <param name="hero">The constellation's main star: the warmer hero look.</param>
        /// <param name="seed">Personality seed, stable per level + star so a level always plays the same.</param>
        public void Init(int index, float size, Color idle, int sortingOrder, StarStyle style, bool hero, int seed)
        {
            Index = index;
            this.style = style;
            this.hero = hero;
            var p = transform.localPosition;
            BoardPoint = new Vector3(p.x, p.y, 0f);
            idleColor = idle;
            baseScale = Vector3.one * size;

            sr = GetComponent<SpriteRenderer>();
            sdf = style && style.HasStarShader;
            sr.sprite = sdf ? Art.StarQuad : Art.Star;
            sr.sharedMaterial = sdf ? hero ? style.HeroMaterial : style.MinorMaterial : Art.SpriteMaterial;
            sr.sortingOrder = sortingOrder;

            rng = (uint)seed * 2654435761u ^ 0x9E3779B9u;
            if (rng == 0) rng = 1;
            breathePhase = Next01() * 10f; // stars don't breathe in lockstep
            AssignPersonality();
            pirouetteStart = glintStart = -1f;
            readyAt = 0f;
            fingerOver = false;
            ResetState();
        }

        void AssignPersonality()
        {
            if (!style) return;
            var look = hero ? style.heroLook : style.minorLook;
            int points = Mathf.Max(3, look.points);
            // Tilt that brings the chosen tip straight down (tip 0 points up; tips go clockwise).
            int tip = Mathf.Min(points - 1, (int)(Next01() * points));
            tiltTarget = Mathf.DeltaAngle(0f, tip * 360f / points - 180f) * Mathf.Deg2Rad;
            spinDir = Next01() < 0.5f ? -1f : 1f;
            spinScale = 1f + (Next01() * 2f - 1f) * style.pirouetteSpinJitter;
            hopHeight = Mathf.Lerp(style.pirouetteHop.x, style.pirouetteHop.y, Next01());
            glintAngle = (Next01() - 0.5f) * 0.5f;
            nextGlint = Time.time + Next01() * GlintInterval().y; // first glints are spread out
        }

        float Next01()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        Vector2 GlintInterval() => hero ? style.glintInterval : style.glintInterval * style.minorGlintSlowdown;

        public bool IsNeighbor(Node other) => Neighbors.Contains(other);

        public bool HasUnvisitedNeighbor()
        {
            foreach (var n in Neighbors)
                if (!n.Visited) return true;
            return false;
        }

        /// <summary>Marks the node visited in <paramref name="color"/>, with a settle pop and a sonar ring.</summary>
        public void Visit(Color color)
        {
            Visited = true;
            visitedColor = color;
            sr.color = color;
            visitTime = Time.time;
            if (style) SonarRing.Spawn(transform, color, baseScale.x, style);
        }

        public void ResetState()
        {
            Visited = false;
            visitTime = -1f;
            transform.localScale = baseScale;
            sr.color = idleColor;
        }

        public void SetColor(Color c) => sr.color = c;

        /// <summary>
        /// Called each frame while a line is being drawn: is the finger over this star? A star pirouettes when the
        /// finger enters it (not while it rests there). Visual only.
        /// </summary>
        public void SetFingerOver(bool over)
        {
            if (over && !fingerOver) TryPirouette();
            fingerOver = over;
        }

        void TryPirouette()
        {
            if (!sdf || !style || !style.pirouetteEnabled) return;
            float now = Time.time;
            if (pirouetteStart >= 0f || now < readyAt) return; // never restart mid-spin; cooldown after one
            if (SaveService.ReduceMotion)
            {
                // No spin: just a glint and the sparkles.
                glintStart = now;
                Sparkle();
                readyAt = now + style.glintDuration + style.pirouetteCooldown;
                return;
            }
            pirouetteStart = now;
            landed = false;
        }

        void Sparkle()
        {
            var range = style.sparkCount;
            int count = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);
            StarSparks.Burst(transform, Color.Lerp(style.finalColor, CurrentColor, TintAmount), count, style);
        }

        float TintAmount => Visited ? style.visitedTint : style.idleTint;

        /// <summary>Changes the idle color (and the visited color, if given) and repaints now (visual only).</summary>
        public void SetPalette(Color idle, Color? visited = null)
        {
            idleColor = idle;
            if (visited.HasValue && Visited) visitedColor = visited.Value;
            sr.color = CurrentColor;
        }

        void Update()
        {
            if (!style || !sr) return;
            // Slow breathe: 0 -> 1 -> 0 over breathePeriod, eased in-out.
            float k = 0.5f - 0.5f * Mathf.Cos((Time.time + breathePhase) / style.breathePeriod * 2f * Mathf.PI);
            if (sdf) UpdateShader(k);
            if (!Visited)
            {
                transform.localScale = baseScale * (1f + style.breatheScale * k);
                var c = idleColor;
                c.a *= 1f - style.breatheFade * k;
                sr.color = c;
                return;
            }
            // Settle: up to popPeak fast, then ease down to popRest. No overshoot past the peak.
            float t = Time.time - visitTime;
            float s = t < style.popUpTime
                ? Mathf.Lerp(1f, style.popPeak, Ease.OutQuad(t / style.popUpTime))
                : Mathf.Lerp(style.popPeak, style.popRest, Ease.OutCubic(Mathf.Clamp01((t - style.popUpTime) / style.popSettleTime)));
            transform.localScale = baseScale * s;
        }

        // Glow breathe, glint and pirouette, all drawn by the StarSDF shader.
        void UpdateShader(float breathe)
        {
            float now = Time.time;

            // Glint: a quick cross flare every few seconds, on this star's own random schedule.
            if (now >= nextGlint)
            {
                glintStart = now;
                var range = GlintInterval();
                nextGlint = now + style.glintDuration + Mathf.Lerp(range.x, range.y, Next01());
            }
            float glint = 0f;
            float g = glintStart >= 0f ? (now - glintStart) / style.glintDuration : 1f;
            if (g < 1f)
            {
                glint = Mathf.Sin(Mathf.PI * Mathf.Sqrt(g)); // fast flash in, slower fade
                glint *= glint;
            }

            float tilt = 0f, spin = 0f, hop = 0f, squash = 0f, flash = 0f;
            if (pirouetteStart >= 0f)
            {
                float t = now - pirouetteStart;
                float tiltTime = style.pirouetteTiltTime;
                float spinTime = style.pirouetteSpinTime * spinScale;
                float settleTime = style.pirouetteSettleTime;
                if (t < tiltTime)
                {
                    // a) Rock over onto the chosen tip.
                    tilt = tiltTarget * Ease.InOutSine(t / tiltTime);
                }
                else if (t < tiltTime + spinTime)
                {
                    // b) One full turn on that tip, with a little hop at the start.
                    float u = (t - tiltTime) / spinTime;
                    tilt = tiltTarget;
                    spin = spinDir * 2f * Mathf.PI * Ease.InOutSine(u);
                    hop = hopHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 2f));
                    flash = style.pirouetteFlash * Mathf.Sin(Mathf.PI * u);
                }
                else if (t < tiltTime + spinTime + settleTime)
                {
                    // c) Land: back to rest with a slight overshoot, a squash & bounce and a few sparkles.
                    if (!landed)
                    {
                        landed = true;
                        Sparkle();
                    }
                    float u = (t - tiltTime - spinTime) / settleTime;
                    tilt = tiltTarget * (1f - Ease.OutBack(u));
                    squash = style.pirouetteSquash * Mathf.Sin(2f * Mathf.PI * u) * (1f - u) * (1f - u);
                }
                else
                {
                    pirouetteStart = -1f;
                    readyAt = now + style.pirouetteCooldown;
                }
            }

            block ??= new MaterialPropertyBlock();
            block.SetVector(AnimId, new Vector4(breathe, glint, tilt, spin));
            block.SetVector(Anim2Id, new Vector4(hop, squash, spinDir, TintAmount));
            block.SetVector(Anim3Id, new Vector4(flash, glintAngle, 0f, 0f));
            sr.SetPropertyBlock(block);
        }
    }

    /// <summary>Thin ring that expands from a node and fades out.</summary>
    public static class SonarRing
    {
        public static void Spawn(Transform node, Color color, float nodeSize, StarStyle style)
        {
            var go = new GameObject("Sonar", typeof(SpriteRenderer));
            go.transform.SetParent(node.parent, false); // not under the node, so its pop doesn't scale the ring
            go.transform.localPosition = node.localPosition;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = Art.Ring;
            sr.sharedMaterial = Art.SpriteMaterial;
            sr.sortingOrder = 15;
            Tween.Run(go, style.sonarDuration, Ease.OutCubic, k =>
            {
                go.transform.localScale = Vector3.one * Mathf.Lerp(nodeSize, nodeSize * style.sonarScale, k);
                var c = color;
                c.a = style.sonarAlpha * (1f - k);
                sr.color = c;
            }, () => Object.Destroy(go));
        }
    }
}
