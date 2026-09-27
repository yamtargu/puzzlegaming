using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Live shop preview of a wand: a short line draws itself across a few stars with the wand's colors, glow,
    /// sparkles, rainbow, shimmer or ink width, fairy dust trails behind the moving tip (a comet head and tail for
    /// Comet) and a small burst pops at each star; then it fades and starts over. One uGUI mesh, rebuilt only while
    /// visible (inside the scroll view, shop open), so it costs nothing during gameplay. Mirrors WandTrail / the
    /// WandLine shader, driven by the same WandLook.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class WandPreview : MaskableGraphic
    {
        const int Samples = 12;  // per segment, for per-vertex colors and widths
        const int MaxMotes = 48;
        const int TailSamples = 24;

        // Constellation used by every preview, as fractions of the rect (x, y from the center).
        static readonly Vector2[] Stars = { new(-0.4f, -0.2f), new(-0.13f, 0.2f), new(0.14f, -0.14f), new(0.4f, 0.18f) };

        struct Mote
        {
            public Vector2 pos, vel;
            public float age, life, size, rotation, spin, flutter, phase;
            public Color color;
            public DustShape shape;
            public bool twinkle, flash;
        }

        CosmeticCatalog.Wand wand;
        WandLook look;
        WandEffect effect;
        UIStyle ui;
        Color pathA, pathB, starColor;

        readonly Vector2[] points = new Vector2[Stars.Length];
        readonly float[] startAt = new float[Stars.Length]; // path distance (px) at each star
        readonly Color32[] band = new Color32[Samples + 1];
        readonly float[] bandWidth = new float[Samples + 1];
        readonly Mote[] motes = new Mote[MaxMotes];
        readonly Vector2[] tailPoints = new Vector2[TailSamples];
        readonly float[] tailTimes = new float[TailSamples];
        int moteCount, tailNewest, tailCount;
        float clock, pathPx, drawnPx, alpha = 1f, dustDue, tailPushedAt, headAlpha;
        int reached;
        Vector2 lastHead;

        public CosmeticCatalog.Wand Wand => wand;

        /// <summary>Shows <paramref name="item"/>; <paramref name="visitA"/>/<paramref name="visitB"/> stand in for the path's colors.</summary>
        public void Show(CosmeticCatalog.Wand item, WandLook itemLook, UIStyle style, Color visitA, Color visitB)
        {
            wand = item;
            look = itemLook;
            effect = item.effect;
            ui = style;
            pathA = visitA;
            pathB = visitB;
            starColor = style.Gold;
            raycastTarget = false;
            Restart();
        }

        /// <summary>Starts the loop over from the first star.</summary>
        public void Restart()
        {
            clock = 0f;
            drawnPx = 0f;
            reached = 0;
            moteCount = 0;
            tailCount = 0;
            headAlpha = 0f;
            dustDue = 0f;
            Layout();
            lastHead = points[0];
            SetVerticesDirty();
        }

        float PxPerUnit => rectTransform.rect.height / Mathf.Max(0.1f, ui ? ui.wandPreviewUnits : 1.3f);

        void Layout()
        {
            var r = rectTransform.rect;
            pathPx = 0f;
            for (int i = 0; i < Stars.Length; i++)
            {
                points[i] = r.center + Vector2.Scale(Stars[i], r.size);
                if (i > 0) pathPx += Vector2.Distance(points[i - 1], points[i]);
                startAt[i] = pathPx;
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (look) Restart();
        }

        void Update()
        {
            if (!look || !ui || canvasRenderer.cull) return; // scrolled out of view: nothing to do
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            clock += dt;
            float cycle = ui.wandPreviewDrawTime + ui.wandPreviewHold + ui.wandPreviewFade;
            if (clock >= cycle) Restart();

            float px = PxPerUnit;
            bool drawing = clock < ui.wandPreviewDrawTime;
            drawnPx = pathPx * Ease.InOutSine(Mathf.Clamp01(clock / ui.wandPreviewDrawTime));
            float fadeStart = ui.wandPreviewDrawTime + ui.wandPreviewHold;
            alpha = clock > fadeStart ? 1f - (clock - fadeStart) / ui.wandPreviewFade : 1f;

            // Dust off the moving tip, more when it moves faster.
            Vector2 head = PointAt(drawnPx);
            Vector2 delta = head - lastHead;
            float moved = delta.magnitude / px;
            if (drawing)
            {
                dustDue += moved * look.dustPerUnit + look.dustIdleRate * dt;
                int count = Mathf.Min((int)dustDue, look.dustMaxPerFrame);
                dustDue -= (int)dustDue;
                Vector2 dir = moved > 1e-5f ? delta / (moved * px) : Vector2.zero;
                bool spins = look.dustSpin.y > 0f;
                for (int i = 0; i < count; i++)
                {
                    var velocity = Random.insideUnitCircle * look.dustScatter + look.dustDrift - dir * look.dustTrailBack;
                    Spawn(new Mote
                    {
                        pos = Vector2.Lerp(lastHead, head, Random.value), vel = velocity * px,
                        color = look.DustColor(effect, drawnPx / px, clock, PathColor(drawnPx)),
                        size = Random.Range(look.dustSize.x, look.dustSize.y) * px,
                        life = Random.Range(look.dustLifetime.x, look.dustLifetime.y),
                        shape = look.dustShape, twinkle = look.dustTwinkle,
                        rotation = look.dustShape == DustShape.Sparkle || spins ? Random.value * 360f : 0f,
                        spin = spins ? Random.Range(look.dustSpin.x, look.dustSpin.y) * (Random.value < 0.5f ? -1f : 1f) : 0f,
                        flutter = look.dustFlutter, phase = Random.value * 2f * Mathf.PI,
                    });
                }
            }
            lastHead = head;

            if (effect == WandEffect.Comet) UpdateTail(head, drawing, dt);

            // A burst each time the tip reaches a star.
            while (reached + 1 < Stars.Length && drawnPx >= startAt[reached + 1] - 0.5f)
            {
                reached++;
                Burst(points[reached], PathColor(startAt[reached]), px);
            }

            // Motes: drift, slow down, fall, flutter, spin, age.
            float damp = Mathf.Pow(0.92f, dt * 60f);
            for (int i = moteCount - 1; i >= 0; i--)
            {
                ref var m = ref motes[i];
                m.age += dt;
                if (m.age >= m.life)
                {
                    motes[i] = motes[--moteCount];
                    continue;
                }
                m.vel *= damp;
                if (!m.flash) m.vel.y -= look.dustGravity * px * dt;
                m.pos += m.vel * dt;
                m.pos.x += Mathf.Sin(m.age * look.dustFlutterFrequency * 2f * Mathf.PI + m.phase) * m.flutter * px * dt;
                m.rotation += m.spin * dt;
            }
            SetVerticesDirty();
        }

        // Comet: the head's recent positions, spaced so they span the tail's length in time.
        void UpdateTail(Vector2 head, bool drawing, float dt)
        {
            float spacing = Mathf.Max(0.001f, look.cometTailTime) / (TailSamples - 2);
            if (drawing)
            {
                if (tailCount == 0 || clock - tailPushedAt >= spacing)
                {
                    tailNewest = (tailNewest + 1) % TailSamples;
                    tailCount = Mathf.Min(tailCount + 1, TailSamples);
                    tailPushedAt = clock;
                }
                tailPoints[tailNewest] = head;
                tailTimes[tailNewest] = clock;
            }
            while (tailCount > 0 && clock - tailTimes[(tailNewest - tailCount + 1 + TailSamples) % TailSamples] > look.cometTailTime)
                tailCount--;
            headAlpha = Mathf.MoveTowards(headAlpha, drawing ? 1f : 0f, dt / Mathf.Max(0.01f, look.cometFadeTime));
        }

        void Burst(Vector2 at, Color pathColor, float px)
        {
            var range = look.burstCount;
            int count = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);
            float spin = Random.value * 2f * Mathf.PI;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * 2f * Mathf.PI + spin;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Spawn(new Mote
                {
                    pos = at, vel = dir * (look.burstSpeed * Random.Range(0.6f, 1.1f) * px),
                    color = look.BurstColor(effect, drawnPx / px, clock, pathColor),
                    size = Random.Range(look.burstSize.x, look.burstSize.y) * px,
                    life = look.burstLifetime * Random.Range(0.75f, 1f),
                    shape = look.burstShape, rotation = Random.value * 90f,
                });
            }
            if (look.burstFlashSize > 0f)
                Spawn(new Mote { pos = at, color = look.burstFlashColor, size = look.burstFlashSize * px, life = look.burstFlashTime, flash = true });
        }

        void Spawn(Mote mote)
        {
            if (moteCount >= MaxMotes) return;
            mote.life = Mathf.Max(0.05f, mote.life);
            motes[moteCount++] = mote;
        }

        Vector2 PointAt(float distance)
        {
            for (int i = 1; i < Stars.Length; i++)
            {
                if (distance > startAt[i] && i < Stars.Length - 1) continue;
                float len = startAt[i] - startAt[i - 1];
                return Vector2.Lerp(points[i - 1], points[i], len > 0f ? Mathf.Clamp01((distance - startAt[i - 1]) / len) : 1f);
            }
            return points[0];
        }

        // The path's own color here (the visit palette, first to last star).
        Color PathColor(float distance) => Color.Lerp(pathA, pathB, pathPx > 0f ? distance / pathPx : 0f);

        // ---------- mesh ----------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!look || !ui) return;
            float px = PxPerUnit;
            float pixel = VectorMesh.Pixel(this);

            float coreHalf = look.coreWidth * 0.5f * px;
            float glowHalf = look.glowWidth * 0.5f * px;
            float soft = coreHalf * look.coreSoftness;
            // Each segment is a capsule (round ends), like the WandLine shader draws it on the board.
            for (int s = 1; s < Stars.Length; s++)
            {
                float from = startAt[s - 1], to = Mathf.Min(startAt[s], drawnPx);
                if (to <= from + 0.5f) break;
                Vector2 a = points[s - 1], b = PointAt(to);
                FillBand(s, from, to, px, glow: true);
                Capsule(vh, a, b, 0f, glowHalf, look.glowAlpha, false);
                FillBand(s, from, to, px, glow: false);
                Capsule(vh, a, b, Mathf.Max(0f, coreHalf - soft - pixel * 0.5f), pixel + soft, 1f, true);
                // Whiter center of the core.
                for (int i = 0; i <= Samples; i++) band[i] = Color32.Lerp(band[i], new Color32(255, 255, 255, band[i].a), look.coreHighlight);
                Capsule(vh, a, b, 0f, coreHalf * 0.8f, look.coreHighlight, true);
            }

            if (effect == WandEffect.Comet) Comet(vh, px);

            // Stars over the line, like on the board.
            for (int i = 0; i < Stars.Length; i++)
            {
                bool lit = i <= reached;
                VectorMesh.Disc(vh, points[i], 0f, 9f * pixel * (lit ? 1.6f : 1f), UIKit.WithAlpha(starColor, 0.35f * alpha));
                VectorMesh.Disc(vh, points[i], 2.6f * pixel, pixel, UIKit.WithAlpha(lit ? Color.white : starColor, (lit ? 1f : 0.7f) * alpha));
            }

            if (effect == WandEffect.Stardust) Sparkles(vh, px, pixel);

            for (int i = 0; i < moteCount; i++)
            {
                ref var m = ref motes[i];
                float t = m.age / m.life;
                var c = m.color;
                if (m.flash)
                {
                    c.a *= (1f - t) * alpha;
                    VectorMesh.Disc(vh, m.pos, 0f, m.size * 0.5f * WandLook.FlashCurve.Evaluate(t), c);
                    continue;
                }
                float size = m.size * (m.twinkle ? WandLook.TwinkleCurve.Evaluate(t) : WandLook.ShrinkCurve.Evaluate(t));
                c.a *= (t < 0.4f ? Mathf.Lerp(0.9f, 0.6f, t / 0.4f) : Mathf.Lerp(0.6f, 0f, (t - 0.4f) / 0.6f)) * alpha;
                switch (m.shape)
                {
                    case DustShape.Sparkle:
                        Glint(vh, m.pos, size * 0.5f, m.rotation, c);
                        break;
                    case DustShape.Flake:
                        // Tumbling: the flake's width flips over as it falls.
                        float face = Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(Mathf.PI * look.dustFlip * m.age)));
                        Flake(vh, m.pos, size, face, m.rotation, c);
                        break;
                    default:
                        VectorMesh.Disc(vh, m.pos, size * 0.2f, size * 0.3f, c);
                        break;
                }
            }
        }

        // Colors (and Gold Ink widths) along [from, to] of segment `s` into `band` / `bandWidth`.
        void FillBand(int s, float from, float to, float px, bool glow)
        {
            var own = glow && effect != WandEffect.Rainbow ? look.glowColor : look.lineColor;
            float segLength = Mathf.Max(1e-3f, startAt[s] - startAt[s - 1]);
            bool shimmers = effect is WandEffect.Moonlight or WandEffect.GoldInk;
            for (int i = 0; i <= Samples; i++)
            {
                float d = Mathf.Lerp(from, to, i / (float)Samples);
                var c = effect == WandEffect.Rainbow ? look.Hue(d / px, clock) : own;
                c = Color.Lerp(c, PathColor(d), look.pathColorMix);
                if (shimmers)
                {
                    float shimmer = look.Shimmer(d / px, clock);
                    c = glow ? c * (1f + shimmer) : c + shimmer * Color.Lerp(c, Color.white, 0.6f);
                    c.a = 1f;
                }
                band[i] = c;
                // A stroke is slow at the stars and fastest between them: thick - thin - thick.
                float u = Mathf.Clamp01((d - startAt[s - 1]) / segLength);
                float speed = Mathf.Lerp(look.inkSlowSpeed, look.inkFastSpeed, 2f * Mathf.Sqrt(u * (1f - u)));
                bandWidth[i] = effect == WandEffect.GoldInk ? look.InkWidth(speed) : 1f;
            }
        }

        // A band from a to b with per-sample colors (solid half width `core`, fading out over `feather`) and round ends.
        // With `inked`, the solid part follows the per-sample widths.
        void Capsule(VertexHelper vh, Vector2 a, Vector2 b, float core, float feather, float opacity, bool inked)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 dir = d / len;
            var n = new Vector2(-dir.y, dir.x);
            int first = vh.currentVertCount;
            for (int i = 0; i <= Samples; i++)
            {
                Vector2 p = a + d * (i / (float)Samples);
                float w = inked ? core * bandWidth[i] : core;
                var c = WithAlpha(band[i], opacity * alpha);
                var clear = c;
                clear.a = 0;
                vh.AddVert(p + n * (w + feather), clear, Vector4.zero);
                vh.AddVert(p + n * w, c, Vector4.zero);
                vh.AddVert(p - n * w, c, Vector4.zero);
                vh.AddVert(p - n * (w + feather), clear, Vector4.zero);
                if (i == 0) continue;
                int o = first + (i - 1) * 4;
                for (int k = 0; k < 3; k++)
                {
                    vh.AddTriangle(o + k, o + k + 1, o + k + 5);
                    vh.AddTriangle(o + k, o + k + 5, o + k + 4);
                }
            }
            Cap(vh, a, -dir, inked ? core * bandWidth[0] : core, feather, WithAlpha(band[0], opacity * alpha));
            Cap(vh, b, dir, inked ? core * bandWidth[Samples] : core, feather, WithAlpha(band[Samples], opacity * alpha));
        }

        // Half disc closing a band end, bulging toward `outward`.
        static void Cap(VertexHelper vh, Vector2 center, Vector2 outward, float core, float feather, Color32 color)
        {
            const int steps = 10;
            var clear = color;
            clear.a = 0;
            var n = new Vector2(-outward.y, outward.x);
            int c = vh.currentVertCount;
            vh.AddVert(center, color, Vector4.zero);
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.PI * i / steps;
                Vector2 dir = n * Mathf.Cos(angle) + outward * Mathf.Sin(angle);
                vh.AddVert(center + dir * core, color, Vector4.zero);
                vh.AddVert(center + dir * (core + feather), clear, Vector4.zero);
                if (i == 0) continue;
                int o = c + 1 + (i - 1) * 2;
                vh.AddTriangle(c, o, o + 2);
                vh.AddTriangle(o, o + 1, o + 3);
                vh.AddTriangle(o, o + 3, o + 2);
            }
        }

        // Comet: a soft tail tapering away from the head, then the head with its halo.
        void Comet(VertexHelper vh, float px)
        {
            if (tailCount >= 2)
            {
                int first = vh.currentVertCount;
                for (int i = 0; i < tailCount; i++)
                {
                    var p = tailPoints[(tailNewest - i + TailSamples) % TailSamples];
                    var prev = tailPoints[(tailNewest - Mathf.Max(0, i - 1) + TailSamples) % TailSamples];
                    var next = tailPoints[(tailNewest - Mathf.Min(tailCount - 1, i + 1) + TailSamples) % TailSamples];
                    var along = next - prev;
                    var n = along.sqrMagnitude > 1e-6f ? new Vector2(-along.y, along.x).normalized : Vector2.up;
                    float k = i / (float)(tailCount - 1);
                    float half = look.cometTailWidth * 0.5f * px * (1f - k);
                    var c = Color.Lerp(look.cometHeadColor, look.cometTailColor, Mathf.Clamp01(k / 0.35f));
                    c.a = Mathf.Lerp(1f, 0f, k) * alpha;
                    Color32 solid = c;
                    var clear = solid;
                    clear.a = 0;
                    vh.AddVert(p + n * half, clear, Vector4.zero);
                    vh.AddVert(p, solid, Vector4.zero);
                    vh.AddVert(p - n * half, clear, Vector4.zero);
                    if (i == 0) continue;
                    int o = first + (i - 1) * 3;
                    vh.AddTriangle(o, o + 1, o + 4);
                    vh.AddTriangle(o, o + 4, o + 3);
                    vh.AddTriangle(o + 1, o + 2, o + 5);
                    vh.AddTriangle(o + 1, o + 5, o + 4);
                }
            }
            if (headAlpha <= 0.01f || tailCount == 0) return;
            var at = tailPoints[tailNewest];
            float size = look.cometHeadSize * px;
            // Halo: kept tighter than its full radius, like the board's SoftCircle whose falloff hides the outer part.
            VectorMesh.Disc(vh, at, 0f, size * look.cometHaloScale * 0.3f, UIKit.WithAlpha(look.cometHeadColor, look.cometHaloAlpha * headAlpha * alpha));
            VectorMesh.Disc(vh, at, size * 0.18f, size * 0.3f, UIKit.WithAlpha(look.cometHeadColor, headAlpha * alpha));
        }

        // Stardust: short 4-point glints along the drawn line, each with its own phase (like the shader).
        void Sparkles(VertexHelper vh, float px, float pixel)
        {
            float spacing = Mathf.Max(0.02f, look.sparkleSpacing) * px;
            int cells = Mathf.FloorToInt(drawnPx / spacing);
            for (int c = 0; c <= cells; c++)
            {
                float pos = (c + 0.2f + 0.6f * Hash(c)) * spacing;
                if (pos > drawnPx) break;
                float cycle = Frac(clock * look.sparkleRate * (0.6f + 0.8f * Hash(c + 31.7f)) + Hash(c + 47.3f));
                float flash = Mathf.Clamp01(1f - Mathf.Abs(cycle - 0.08f) / 0.08f);
                flash *= flash;
                if (flash < 0.02f) continue;
                Vector2 at = PointAt(pos) + new Vector2(0f, (Hash(c + 17.1f) - 0.5f) * look.coreWidth * 1.4f * px);
                var col = look.sparkleColor;
                col.a = Mathf.Clamp01(flash * look.sparkleIntensity) * alpha;
                Glint(vh, at, Mathf.Max(3f * pixel, look.sparkleSize * px), 0f, col);
            }
        }

        // Thin 4-point star: two crossed diamonds.
        static void Glint(VertexHelper vh, Vector2 center, float arm, float degrees, Color color)
        {
            float w = arm * 0.14f;
            float r = degrees * Mathf.Deg2Rad;
            var x = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            var y = new Vector2(-x.y, x.x);
            Color32 c = color;
            for (int k = 0; k < 2; k++)
            {
                var along = k == 0 ? x : y;
                var side = k == 0 ? y : x;
                int o = vh.currentVertCount;
                vh.AddVert(center + along * arm, c, Vector4.zero);
                vh.AddVert(center + side * w, c, Vector4.zero);
                vh.AddVert(center - along * arm, c, Vector4.zero);
                vh.AddVert(center - side * w, c, Vector4.zero);
                vh.AddTriangle(o, o + 1, o + 2);
                vh.AddTriangle(o, o + 2, o + 3);
            }
        }

        // Gold-leaf flake (Art.FlakeShape), rotated, its width scaled by `face` (tumbling).
        static void Flake(VertexHelper vh, Vector2 center, float size, float face, float degrees, Color color)
        {
            float r = degrees * Mathf.Deg2Rad;
            var x = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            var y = new Vector2(-x.y, x.x);
            Color32 c = color;
            int o = vh.currentVertCount;
            vh.AddVert(center, c, Vector4.zero);
            var shape = Art.FlakeShape;
            for (int i = 0; i < shape.Length; i++)
                vh.AddVert(center + (x * (shape[i].x * face) + y * shape[i].y) * size, c, Vector4.zero);
            for (int i = 0; i < shape.Length; i++) vh.AddTriangle(o, o + 1 + i, o + 1 + (i + 1) % shape.Length);
        }

        static Color32 WithAlpha(Color32 c, float a)
        {
            c.a = (byte)(c.a * Mathf.Clamp01(a));
            return c;
        }

        static float Hash(float n) => Frac(Mathf.Sin(n * 12.9898f) * 43758.5453f);
        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
