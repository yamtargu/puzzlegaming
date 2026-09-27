using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The astrological sky behind the board, all at ≤ 10% alpha and slow:
    /// a huge astrolabe wheel with the 12 glyphs turning once every ~10 minutes (the current sign's segment slightly
    /// brighter), the current sign's constellation as a faint gold star figure, drifting planet glyphs, a soft nebula
    /// tinted by the sign's element and a rare shooting star. Layers move with the board tilt like
    /// ParallaxBackground. Entering a new sign turns the wheel to it. On the home screen (<see cref="SetHome"/>) the
    /// wheel drifts up behind the logo, grows brighter and the sky moves a little closer. Visual only.
    /// </summary>
    public class ZodiacBackground : MonoBehaviour
    {
        public ZodiacTheme theme;
        public LevelManager levelManager;
        public BoardView boardView;
        public DepthSettings depth;
        public int seed = 11;
        [Tooltip("Speed of the slow motions (wheel turn, nebula drift, planets). Lunar Stillness lowers it. Visual only.")]
        public float timeScale = 1f;

        // Sorting: in front of the backdrop gradient (-60), behind ParallaxBackground's stars (-40..-30).
        const int NebulaOrder = -58, WheelOrder = -56, ConstellationOrder = -50, PlanetOrder = -45;

        Camera cam;
        Transform far, mid, wheel;
        SpriteRenderer wheelRenderer, highlight, shooting;
        readonly SpriteRenderer[] nebula = new SpriteRenderer[2];
        readonly SpriteRenderer[] figure = new SpriteRenderer[2]; // current + fading out
        readonly List<SpriteRenderer> planets = new();
        readonly List<float> planetSpeed = new();
        readonly List<Texture2D> textures = new();
        Texture2D[] figureTex = new Texture2D[2];
        int shownSign = -1, requestedSign = -1;
        float wheelBase;
        float homeBlend; // 0 = game framing, 1 = home framing
        float skyTime;   // Time.time, scaled by timeScale
        Color wheelColor;
        Tween wheelTween, fadeTween, homeTween;
        System.Random rnd;

        // shooting star
        float nextShoot, shootTime = -1f;
        Vector2 shootFrom, shootDir;
        const float ShootDuration = 0.9f;

        static readonly Vector2[] NebulaPos = { new(-0.32f, 0.42f), new(0.36f, -0.38f) };
        static readonly float[] NebulaSize = { 1.7f, 1.3f };

        void OnEnable() { if (levelManager) levelManager.LevelLoaded += OnLevelLoaded; }
        void OnDisable() { if (levelManager) levelManager.LevelLoaded -= OnLevelLoaded; }

        void Start()
        {
            cam = Camera.main;
            if (!theme || !cam) { enabled = false; return; }
            rnd = new System.Random(seed);
            Build();
            nextShoot = Time.time + Lerp(ShootingInterval, 0.5f);
            if (levelManager && levelManager.pathManager && levelManager.pathManager.Level) OnLevelLoaded(levelManager.CurrentIndex);
            else if (requestedSign >= 0) ApplySign(requestedSign);
        }

        /// <summary>Shows a sign without a level loaded (the home screen shows where "Continue" leads).</summary>
        public void ShowSign(int sign)
        {
            requestedSign = sign;
            if (far) ApplySign(sign);
        }

        /// <summary>Eases the sky between the home framing (wheel behind the logo, brighter, closer) and the game's.</summary>
        public void SetHome(bool home, float time)
        {
            homeTween?.Kill();
            float from = homeBlend, to = home ? 1f : 0f;
            if (time <= 0f) { homeBlend = to; return; }
            homeTween = Tween.Run(this, time, Ease.InOutSine, k => homeBlend = Mathf.Lerp(from, to, k));
        }

        Vector2 ShootingInterval => Vector2.Lerp(theme.shootingStarInterval, theme.homeShootingStarInterval, homeBlend);

        void OnDestroy()
        {
            foreach (var t in textures) if (t) Destroy(t);
            foreach (var t in figureTex) if (t) Destroy(t);
        }

        // ---------- per frame (no allocations) ----------

        void LateUpdate()
        {
            if (!far) return;
            float size = cam.orthographicSize;
            Vector2 tilt = boardView ? boardView.Tilt : Vector2.zero;
            Vector3 rates = depth ? depth.parallaxStrength : new Vector3(0.03f, 0.07f, 0.13f);
            float zoom = size * Mathf.Lerp(1f, theme.homeZoom, homeBlend);
            Place(far, -tilt * rates.x * size, zoom, 80f);
            Place(mid, -tilt * rates.y * size, zoom, 65f);

            skyTime += Time.deltaTime * timeScale;
            float t = skyTime;
            wheel.localRotation = Quaternion.Euler(0f, 0f, wheelBase - Drift(t));
            // Home: up behind the logo, a little smaller and brighter.
            wheel.localPosition = new Vector3(0f, theme.homeWheelY * homeBlend, 0f);
            wheel.localScale = Vector3.one * (2f * Mathf.Lerp(theme.wheelSize, theme.homeWheelSize, homeBlend));
            wheelRenderer.color = UIKit.WithAlpha(wheelColor, Mathf.Lerp(theme.wheelAlpha, theme.homeWheelAlpha, homeBlend));
            highlight.color = UIKit.WithAlpha(theme.gold, Mathf.Lerp(theme.wheelHighlightAlpha, theme.homeHighlightAlpha, homeBlend));

            for (int i = 0; i < nebula.Length; i++)
            {
                float w = t * 2f * Mathf.PI / (140f + 60f * i);
                nebula[i].transform.localPosition = (Vector3)(NebulaPos[i] + 0.06f * new Vector2(Mathf.Sin(w), Mathf.Cos(w * 0.7f)));
            }

            float halfWidth = cam.aspect + 0.15f;
            for (int i = 0; i < planets.Count; i++)
            {
                var p = planets[i].transform.localPosition;
                p.x += planetSpeed[i] * Time.deltaTime * timeScale;
                if (p.x > halfWidth) p.x = -halfWidth;
                planets[i].transform.localPosition = p;
            }

            UpdateShootingStar(Time.time);
        }

        float Drift(float t) => Mathf.Repeat(t * 360f / Mathf.Max(1f, theme.wheelTurnSeconds), 360f);

        static void Place(Transform layer, Vector2 offset, float scale, float z)
        {
            layer.localScale = new Vector3(scale, scale, 1f);
            layer.localPosition = new Vector3(offset.x, offset.y, z);
        }

        void UpdateShootingStar(float t)
        {
            if (shootTime < 0f)
            {
                if (t < nextShoot) return;
                // Start somewhere in the upper half, heading down and sideways.
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                shootFrom = new Vector2((float)(rnd.NextDouble() - 0.5) * cam.aspect, 0.3f + 0.5f * (float)rnd.NextDouble());
                shootDir = new Vector2(side, -0.45f).normalized;
                shooting.transform.localRotation = Quaternion.FromToRotation(Vector3.up, -shootDir); // tail trails behind
                shooting.gameObject.SetActive(true);
                shootTime = t;
            }
            float k = (t - shootTime) / ShootDuration;
            if (k >= 1f)
            {
                shooting.gameObject.SetActive(false);
                shootTime = -1f;
                nextShoot = t + Lerp(ShootingInterval, (float)rnd.NextDouble());
                return;
            }
            shooting.transform.localPosition = shootFrom + shootDir * (0.7f * k);
            shooting.color = UIKit.WithAlpha(theme.gold, theme.shootingStarAlpha * Mathf.Sin(k * Mathf.PI));
        }

        // ---------- sign changes ----------

        void OnLevelLoaded(int _)
        {
            if (far) ApplySign(levelManager.CurrentSign);
        }

        void ApplySign(int sign)
        {
            if (sign == shownSign) return;
            bool first = shownSign < 0;
            shownSign = sign;

            highlight.transform.localRotation = Quaternion.Euler(0f, 0f, 30f * sign);
            // The current sign's segment at the top right now; the slow turn carries it on from there.
            float target = -30f * sign + Drift(skyTime);
            wheelTween?.Kill();
            if (first) wheelBase = target;
            else
            {
                float from = wheelBase, delta = Mathf.DeltaAngle(from, target);
                wheelTween = Tween.Run(wheel, theme.wheelTurnTime, Ease.InOutSine, k => wheelBase = from + delta * k);
            }

            // Constellation: fade the old figure out and the new one in; nebula toward the element's tint.
            (figure[0], figure[1]) = (figure[1], figure[0]);
            (figureTex[0], figureTex[1]) = (figureTex[1], figureTex[0]);
            if (figureTex[0]) Destroy(figureTex[0]);
            if (figure[0].sprite) Destroy(figure[0].sprite);
            figureTex[0] = RasterFigure(theme.constellations ? theme.constellations.Get(sign) : null);
            figure[0].sprite = figureTex[0] ? MakeSprite(figureTex[0]) : null;
            float width = Mathf.Min(theme.constellationSize * 2f, cam.aspect * 2f * 0.95f);
            figure[0].transform.localScale = Vector3.one * width;

            var gold = theme.gold;
            var fromTint = nebula[0].color;
            var toTint = UIKit.WithAlpha(Desaturate(theme.Tint(Zodiac.ElementOf(sign)), 0.25f), theme.nebulaAlpha);
            float outFrom = figure[1].color.a;
            fadeTween?.Kill();
            fadeTween = Tween.Run(this, first ? 0.01f : theme.elementFadeTime, Ease.InOutSine, k =>
            {
                figure[0].color = UIKit.WithAlpha(gold, theme.constellationAlpha * k);
                figure[1].color = UIKit.WithAlpha(gold, outFrom * (1f - k));
                var c = Color.Lerp(fromTint, toTint, k);
                nebula[0].color = c;
                nebula[1].color = UIKit.WithAlpha(c, c.a * 0.8f);
            });
        }

        // ---------- building ----------

        void Build()
        {
            far = Layer("Zodiac Far");
            mid = Layer("Zodiac Mid");

            for (int i = 0; i < nebula.Length; i++)
            {
                nebula[i] = AddRenderer(far, "Nebula", Art.SoftCircle, NebulaOrder, Color.clear);
                nebula[i].transform.localScale = Vector3.one * NebulaSize[i];
            }

            var wheelSprite = MakeSprite(Track(RasterWheel(1024)));
            wheelColor = Desaturate(theme.gold, 0.2f);
            wheelRenderer = AddRenderer(far, "Zodiac Wheel", wheelSprite, WheelOrder, UIKit.WithAlpha(wheelColor, theme.wheelAlpha));
            wheel = wheelRenderer.transform;
            wheel.localScale = Vector3.one * theme.wheelSize * 2f; // share of screen height -> view units
            highlight = AddRenderer(wheel, "Current Sign", MakeSprite(Track(RasterWedge(512))), WheelOrder + 1,
                UIKit.WithAlpha(theme.gold, theme.wheelHighlightAlpha));

            for (int i = 0; i < figure.Length; i++)
                figure[i] = AddRenderer(mid, "Constellation", null, ConstellationOrder, Color.clear);

            // Planet glyphs, spread over the screen, drifting slowly sideways.
            var order = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            for (int i = 0; i < Mathf.Min(theme.planetCount, ZodiacGlyphs.PlanetCount); i++)
            {
                int pick = rnd.Next(order.Count);
                int planet = order[pick];
                order.RemoveAt(pick);
                var sr = AddRenderer(far, "Planet", MakeSprite(Track(RasterGlyph(ZodiacGlyphs.Planet(planet), 96, 5f))), PlanetOrder,
                    UIKit.WithAlpha(new Color(0.85f, 0.88f, 0.95f), theme.planetAlpha));
                sr.transform.localPosition = new Vector3(((float)rnd.NextDouble() * 2f - 1f) * 0.6f,
                    Mathf.Lerp(-0.85f, 0.85f, (i + (float)rnd.NextDouble() * 0.6f) / theme.planetCount), 0f);
                sr.transform.localScale = Vector3.one * theme.planetSize * 2f;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, ((float)rnd.NextDouble() - 0.5f) * 30f);
                planets.Add(sr);
                planetSpeed.Add(theme.planetDrift * 2f * Mathf.Lerp(0.6f, 1.4f, (float)rnd.NextDouble()));
            }

            shooting = AddRenderer(far, "Shooting Star", Art.Ray, PlanetOrder + 1, Color.clear);
            shooting.transform.localScale = new Vector3(0.012f, 0.28f, 1f);
            shooting.gameObject.SetActive(false);
        }

        Transform Layer(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(cam.transform, false);
            return t;
        }

        SpriteRenderer AddRenderer(Transform parent, string name, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = theme.backgroundMaterial ? theme.backgroundMaterial : Art.UnlitSpriteMaterial; // exact opacity, not brightened by the node lights
            sr.sortingOrder = order;
            sr.color = color;
            return sr;
        }

        Texture2D Track(Texture2D t) { textures.Add(t); return t; }

        static Sprite MakeSprite(Texture2D t) =>
            UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), t.width, 0, SpriteMeshType.FullRect);

        static float Lerp(Vector2 range, float k) => Mathf.Lerp(range.x, range.y, k);

        static Color Desaturate(Color c, float amount)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return Color.Lerp(c, new Color(grey, grey, grey, c.a), amount);
        }

        // ---------- textures ----------

        // Astrolabe: outer rings with the 12 glyphs between sign dividers, degree ticks, inner rings, a dotted ring,
        // two off-center orbits and faint spokes. One pass per pixel for the circles, then the glyphs stamped on top.
        static Texture2D RasterWheel(int size)
        {
            var r = new AlphaRaster(size, size);
            float c = size * 0.5f, R = c - 3f;
            (float radius, float width, float alpha)[] rings =
            {
                (1f, 2f, 1f), (0.975f, 1f, 0.7f), (0.8f, 1.6f, 0.9f), (0.785f, 0.8f, 0.6f), (0.62f, 1f, 0.7f),
                (0.3f, 0.8f, 0.5f), (0.12f, 0.8f, 0.5f),
            };
            var orbitA = new Vector2(0.1f, 0.06f) * R;
            var orbitB = new Vector2(-0.08f, -0.12f) * R;
            float tickOuter = 0.785f * R;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude;
                if (d > R + 3f) continue;
                float a = 0f;
                foreach (var ring in rings) a = Mathf.Max(a, Cover(Mathf.Abs(d - ring.radius * R), ring.width) * ring.alpha);
                a = Mathf.Max(a, Cover(Mathf.Abs((p - orbitA).magnitude - 0.4f * R), 0.9f) * 0.55f);
                a = Mathf.Max(a, Cover(Mathf.Abs((p - orbitB).magnitude - 0.52f * R), 0.8f) * 0.45f);

                float deg = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                float perDeg = d * Mathf.Deg2Rad; // pixels per degree at this radius
                // Degree ticks just inside the glyph band.
                float depth = tickOuter - d;
                if (depth >= 0f && depth <= 22f)
                {
                    int k = Mathf.RoundToInt(deg);
                    float len = k % 10 == 0 ? 22f : k % 5 == 0 ? 14f : 8f;
                    if (depth <= len) a = Mathf.Max(a, Cover(Mathf.Abs(deg - k) * perDeg, k % 5 == 0 ? 1.1f : 0.8f) * 0.8f);
                }
                // Sign dividers (glyph band) and faint spokes (inner field), between the signs.
                float rel = Mathf.Repeat(deg - 105f, 30f);
                float along = Mathf.Min(rel, 30f - rel) * perDeg;
                if (d >= 0.8f * R && d <= R) a = Mathf.Max(a, Cover(along, 1.2f) * 0.9f);
                else if (d >= 0.12f * R && d <= 0.62f * R) a = Mathf.Max(a, Cover(along, 0.7f) * 0.35f);
                // Dotted ring.
                if (Mathf.Abs(d - 0.46f * R) < 3f)
                {
                    float dotRel = Mathf.Repeat(deg, 5f);
                    float dot = new Vector2(Mathf.Min(dotRel, 5f - dotRel) * perDeg, d - 0.46f * R).magnitude;
                    a = Mathf.Max(a, Cover(dot, 3.2f) * 0.7f);
                }
                r.Max(x, y, a);
            }

            // Glyphs, upright relative to the rim (Aries at the top, then counter-clockwise).
            for (int s = 0; s < Zodiac.SignCount; s++)
            {
                float angle = 90f + 30f * s;
                var center = new Vector2(c, c) + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * (0.8875f * R);
                r.Glyph(ZodiacGlyphs.Sign(s), center, 0.052f * R, 2.2f, 1f, angle - 90f);
            }
            return r.ToTexture();
        }

        // Soft annular wedge over one sign's segment (at the top).
        static Texture2D RasterWedge(int size)
        {
            var r = new AlphaRaster(size, size);
            float c = size * 0.5f, R = c - 3f * size / 1024f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude / R;
                float deg = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 90f));
                float radial = Mathf.Clamp01((d - 0.8f) / 0.02f) * Mathf.Clamp01((1f - d) / 0.02f);
                float angular = Mathf.Clamp01((15f - deg) / 1.5f);
                r.Max(x, y, radial * angular);
            }
            return r.ToTexture();
        }

        // Constellation: thin lines that stop short of the stars (like a printed star chart), stars sized by
        // brightness with a soft halo.
        static Texture2D RasterFigure(ZodiacConstellations.Constellation f)
        {
            if (f == null || f.stars == null || f.stars.Length == 0) return null;
            const int size = 1024;
            var r = new AlphaRaster(size, size);
            Vector2 Px(Vector2 n) => (n * 0.86f + new Vector2(0.5f, 0.5f)) * size;
            float Radius(int i) => Mathf.Lerp(7f, 2.8f, Mathf.Clamp01(((f.magnitudes != null && i < f.magnitudes.Length ? f.magnitudes[i] : 3f) - 0.5f) / 4.5f));

            foreach (var l in f.lines)
            {
                if (l.x < 0 || l.y < 0 || l.x >= f.stars.Length || l.y >= f.stars.Length) continue;
                Vector2 a = Px(f.stars[l.x]), b = Px(f.stars[l.y]);
                var dir = (b - a).normalized;
                float ga = Radius(l.x) + 9f, gb = Radius(l.y) + 9f;
                if ((b - a).magnitude <= ga + gb) continue;
                r.Segment(a + dir * ga, b - dir * gb, 1.6f, 0.6f);
            }
            for (int i = 0; i < f.stars.Length; i++)
            {
                float rad = Radius(i);
                r.Glow(Px(f.stars[i]), rad * 4f, 0.3f);
                r.Disc(Px(f.stars[i]), rad, 1f);
            }
            return r.ToTexture();
        }

        static Texture2D RasterGlyph(IReadOnlyList<Vector2[]> strokes, int size, float width)
        {
            var r = new AlphaRaster(size, size);
            r.Glyph(strokes, new Vector2(size, size) * 0.5f, size * 0.4f, width, 1f, 0f);
            return r.ToTexture();
        }

        static float Cover(float distance, float width) =>
            width >= 1f ? Mathf.Clamp01(width * 0.5f + 0.5f - distance) : Mathf.Clamp01(1f - distance) * width;
    }

    /// <summary>A white, alpha-only drawing surface for procedural textures (max-blended, anti-aliased).</summary>
    public sealed class AlphaRaster
    {
        readonly int w, h;
        readonly float[] a;

        public AlphaRaster(int width, int height)
        {
            w = width;
            h = height;
            a = new float[w * h];
        }

        public void Max(int x, int y, float v)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (v > a[i]) a[i] = v;
        }

        public void Segment(Vector2 p0, Vector2 p1, float width, float alpha)
        {
            float pad = width * 0.5f + 1.5f;
            int x0 = Mathf.FloorToInt(Mathf.Min(p0.x, p1.x) - pad), x1 = Mathf.CeilToInt(Mathf.Max(p0.x, p1.x) + pad);
            int y0 = Mathf.FloorToInt(Mathf.Min(p0.y, p1.y) - pad), y1 = Mathf.CeilToInt(Mathf.Max(p0.y, p1.y) + pad);
            var d = p1 - p0;
            float len2 = Mathf.Max(d.sqrMagnitude, 1e-6f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - p0, d) / len2);
                float dist = (p - (p0 + d * t)).magnitude;
                float cover = width >= 1f ? Mathf.Clamp01(width * 0.5f + 0.5f - dist) : Mathf.Clamp01(1f - dist) * width;
                Max(x, y, cover * alpha);
            }
        }

        public void Disc(Vector2 c, float radius, float alpha)
        {
            for (int y = Mathf.FloorToInt(c.y - radius - 2); y <= Mathf.CeilToInt(c.y + radius + 2); y++)
            for (int x = Mathf.FloorToInt(c.x - radius - 2); x <= Mathf.CeilToInt(c.x + radius + 2); x++)
                Max(x, y, Mathf.Clamp01(radius + 0.5f - (new Vector2(x + 0.5f, y + 0.5f) - c).magnitude) * alpha);
        }

        public void Glow(Vector2 c, float radius, float alpha)
        {
            for (int y = Mathf.FloorToInt(c.y - radius); y <= Mathf.CeilToInt(c.y + radius); y++)
            for (int x = Mathf.FloorToInt(c.x - radius); x <= Mathf.CeilToInt(c.x + radius); x++)
            {
                float k = 1f - Mathf.Clamp01((new Vector2(x + 0.5f, y + 0.5f) - c).magnitude / radius);
                Max(x, y, k * k * alpha);
            }
        }

        /// <summary>Glyph strokes (in -1..1) centered at <paramref name="center"/>, half-size in pixels.</summary>
        public void Glyph(IReadOnlyList<Vector2[]> strokes, Vector2 center, float halfSize, float width, float alpha, float rotationDeg)
        {
            float cs = Mathf.Cos(rotationDeg * Mathf.Deg2Rad), sn = Mathf.Sin(rotationDeg * Mathf.Deg2Rad);
            Vector2 Map(Vector2 p) => center + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) * halfSize;
            foreach (var s in strokes)
                for (int i = 1; i < s.Length; i++)
                    Segment(Map(s[i - 1]), Map(s[i]), width, alpha);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true); // upload and free the CPU copy
            return tex;
        }
    }
}
