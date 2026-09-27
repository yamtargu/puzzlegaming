using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Anti-aliased lines and discs for uGUI meshes. Each stroke gets a one-pixel alpha feather on both sides, so
    /// hairlines stay crisp at any resolution (no textures involved).
    /// </summary>
    public static class VectorMesh
    {
        /// <summary>
        /// Adds a polyline of <paramref name="count"/> points starting at <paramref name="start"/>.
        /// <paramref name="feather"/> is the soft edge (one screen pixel for crisp lines, wider for glows).
        /// </summary>
        public static void Stroke(VertexHelper vh, List<Vector2> pts, int start, int count, bool closed, float width,
            float feather, Color32 color)
        {
            if (count < 2) return;
            // Coverage stays right for thin lines: below the feather width the line fades instead of thinning.
            float core = width > feather ? (width - feather) * 0.5f : 0f;
            var solid = color;
            if (width < feather) solid.a = (byte)(color.a * width / feather);
            var clear = color;
            clear.a = 0;
            float outer = core + feather;
            int first = vh.currentVertCount;

            for (int i = 0; i < count; i++)
            {
                Vector2 p = pts[start + i];
                Vector2 prev = i > 0 ? pts[start + i - 1] : closed ? pts[start + count - 1] : p;
                Vector2 next = i < count - 1 ? pts[start + i + 1] : closed ? pts[start] : p;
                Vector2 t0 = Dir(prev, p), t1 = Dir(p, next);
                Vector2 tangent = t0 + t1;
                if (tangent.sqrMagnitude < 1e-8f) tangent = t1.sqrMagnitude > 0f ? t1 : t0;
                tangent.Normalize();
                var normal = new Vector2(-tangent.y, tangent.x);
                // Miter: keep the stroke width constant through corners (clamped for sharp turns).
                Vector2 side = t1.sqrMagnitude > 0f ? t1 : t0;
                float dot = Vector2.Dot(normal, new Vector2(-side.y, side.x));
                float miter = 1f / Mathf.Max(dot, 0.4f);
                Add(vh, p + normal * (outer * miter), clear);
                Add(vh, p + normal * (core * miter), solid);
                Add(vh, p - normal * (core * miter), solid);
                Add(vh, p - normal * (outer * miter), clear);
            }

            int segments = closed ? count : count - 1;
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 4, b = first + (i + 1) % count * 4;
                for (int k = 0; k < 3; k++)
                {
                    vh.AddTriangle(a + k, b + k, b + k + 1);
                    vh.AddTriangle(a + k, b + k + 1, a + k + 1);
                }
            }
        }

        /// <summary>Filled circle with a soft rim of <paramref name="feather"/>.</summary>
        public static void Disc(VertexHelper vh, Vector2 center, float radius, float feather, Color32 color, int segments = 20)
        {
            var clear = color;
            clear.a = 0;
            int c = vh.currentVertCount;
            Add(vh, center, color);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Add(vh, center + d * radius, color);
                Add(vh, center + d * (radius + feather), clear);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int inner = c + 1 + i * 2, innerNext = c + 1 + j * 2;
                vh.AddTriangle(c, inner, innerNext);
                vh.AddTriangle(inner, inner + 1, innerNext + 1);
                vh.AddTriangle(inner, innerNext + 1, innerNext);
            }
        }

        static Vector2 Dir(Vector2 a, Vector2 b)
        {
            var d = b - a;
            float m = d.magnitude;
            return m > 1e-5f ? d / m : Vector2.zero;
        }

        static void Add(VertexHelper vh, Vector2 p, Color32 c) => vh.AddVert(p, c, Vector4.zero);

        /// <summary>Copies the first <paramref name="length"/> units of a polyline into <paramref name="dst"/>.</summary>
        public static void Truncate(List<Vector2> src, int start, int count, float length, List<Vector2> dst)
        {
            if (count == 0 || length <= 0f) return;
            dst.Add(src[start]);
            for (int i = 1; i < count; i++)
            {
                var a = src[start + i - 1];
                var b = src[start + i];
                float d = Vector2.Distance(a, b);
                if (d >= length)
                {
                    if (length > 1e-4f) dst.Add(Vector2.Lerp(a, b, length / d));
                    return;
                }
                length -= d;
                dst.Add(b);
            }
        }

        public static float Length(List<Vector2> pts, int start, int count)
        {
            float sum = 0f;
            for (int i = 1; i < count; i++) sum += Vector2.Distance(pts[start + i - 1], pts[start + i]);
            return sum;
        }

        /// <summary>One screen pixel in the graphic's canvas units.</summary>
        public static float Pixel(Graphic g) => g.canvas && g.canvas.scaleFactor > 0f ? 1f / g.canvas.scaleFactor : 1f;
    }

    /// <summary>
    /// A uGUI graphic made of anti-aliased strokes and discs, rebuilt only when its content changes.
    /// Used for the zodiac glyphs and the astrolabe dials.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VectorGraphic : MaskableGraphic
    {
        struct Prim
        {
            public int start, count;
            public float width, radius;
            public Color32 color;
            public bool closed, disc;
        }

        readonly List<Vector2> points = new();
        readonly List<Prim> prims = new();

        public void Clear()
        {
            points.Clear();
            prims.Clear();
            SetVerticesDirty();
        }

        public void Polyline(IReadOnlyList<Vector2> pts, float width, Color color, bool closed = false,
            Vector2 offset = default, float scale = 1f, float rotationDeg = 0f)
        {
            int start = points.Count;
            float cs = Mathf.Cos(rotationDeg * Mathf.Deg2Rad), sn = Mathf.Sin(rotationDeg * Mathf.Deg2Rad);
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i] * scale;
                points.Add(offset + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs));
            }
            prims.Add(new Prim { start = start, count = pts.Count, width = width, color = color, closed = closed });
            SetVerticesDirty();
        }

        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            int start = points.Count;
            points.Add(a);
            points.Add(b);
            prims.Add(new Prim { start = start, count = 2, width = width, color = color });
            SetVerticesDirty();
        }

        public void Circle(Vector2 center, float radius, float width, Color color, int segments = 96)
        {
            int start = points.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            prims.Add(new Prim { start = start, count = segments, width = width, color = color, closed = true });
            SetVerticesDirty();
        }

        public void Disc(Vector2 center, float radius, Color color)
        {
            int start = points.Count;
            points.Add(center);
            prims.Add(new Prim { start = start, count = 1, radius = radius, color = color, disc = true });
            SetVerticesDirty();
        }

        /// <summary>Glyph strokes (in -1..1) scaled to <paramref name="halfSize"/>.</summary>
        public void Glyph(IReadOnlyList<Vector2[]> strokes, Vector2 center, float halfSize, float width, Color color,
            float rotationDeg = 0f)
        {
            foreach (var s in strokes) Polyline(s, width, color, false, center, halfSize, rotationDeg);
        }

        /// <summary>Astrolabe dial: a ring, a hairline inner ring and 12 tick marks (longer on the quarters).</summary>
        public void Dial(float radius, float width, Color color, float tick = 6f)
        {
            Circle(Vector2.zero, radius, width, color);
            Circle(Vector2.zero, radius - 6f, width * 0.5f, UIKit.WithAlpha(color, color.a * 0.7f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float len = i % 3 == 0 ? tick * 1.6f : tick;
                Line(d * radius, d * (radius + len), width * (i % 3 == 0 ? 1f : 0.8f), color);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float px = VectorMesh.Pixel(this);
            var tint = color;
            foreach (var p in prims)
            {
                Color32 c = p.color * tint;
                if (p.disc) VectorMesh.Disc(vh, points[p.start], p.radius, px, c);
                else VectorMesh.Stroke(vh, points, p.start, p.count, p.closed, p.width, px, c);
            }
        }
    }

    /// <summary>
    /// The title cartouche: a double-line frame with concave star-chart corners, broken at the top for the zodiac
    /// medallion and at the bottom for the crescent, a soft outer glow and a small dot–line–dot motif on each side.
    /// <see cref="progress"/> draws the lines from the medallion outward and around (0 = nothing, 1 = complete).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CelestialFrame : MaskableGraphic
    {
        public ZodiacTheme theme;
        [Range(0f, 1f)] public float progress = 1f;
        [Tooltip("0 = normal glow, 1 = the bright win glow.")]
        [Range(0f, 1f)] public float glowBoost;
        [Tooltip("Dark fill inside the inner line, so the sky doesn't show through the title.")]
        public Color fillColor = Color.clear;
        [Tooltip("The top line breaks around a medallion of this radius. Negative = the theme's; 0 = no medallion.")]
        public float medallionRadius = -1f;
        [Tooltip("The bottom line breaks around a crescent.")]
        public bool crescent = true;
        [Tooltip("Small dot–line–dot outside the left and right sides.")]
        public bool sideMotif = true;

        readonly List<Vector2> outer = new(), inner = new(), glowPath = new(), draw = new(), fill = new();
        int outerHalf, innerHalf, glowHalf; // points in the left half; the right half is mirrored

        public void SetProgress(float p)
        {
            if (Mathf.Approximately(p, progress)) return;
            progress = p;
            SetVerticesDirty();
        }

        public void SetGlow(float boost)
        {
            if (Mathf.Approximately(boost, glowBoost)) return;
            glowBoost = boost;
            SetVerticesDirty();
        }

        public float MedallionRadius => medallionRadius < 0f ? (theme ? theme.medallionRadius : 0f) : medallionRadius;

        /// <summary>Points of a corner star in the frame's local space (TL, TR, BL, BR).</summary>
        public Vector2 CornerStar(int i)
        {
            var r = rectTransform.rect;
            float inset = theme ? theme.cornerNotch * 0.32f : 8f;
            float x = (i % 2 == 0 ? r.xMin + inset : r.xMax - inset);
            float y = (i < 2 ? r.yMax - inset : r.yMin + inset);
            return new Vector2(x, y);
        }

        /// <summary>Share of the drawing (0..1) at which the lines reach a corner (top) or the side middle.</summary>
        public float TopCornerAt => outer.Count == 0 ? 0.2f : ArcShare(outer, outerHalf, 1);
        public float SideMiddleAt => 0.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!theme) return;
            BuildHalves();
            float px = VectorMesh.Pixel(this);
            Color line = color;
            Color glow = UIKit.WithAlpha(theme.gold, Mathf.Lerp(theme.glowAlpha, theme.winGlowAlpha, glowBoost));

            // Fill (fades in with the drawing), glow under the outer line, then the lines.
            if (fillColor.a > 0f) Fill(vh, UIKit.WithAlpha(fillColor, fillColor.a * Mathf.Clamp01(progress)));
            DrawHalves(vh, glowPath, glowHalf, theme.glowWidth * (1f + 0.5f * glowBoost), theme.glowWidth * 0.5f * (1f + 0.5f * glowBoost), glow, true);
            Color bright = Color.Lerp(line, UIKit.WithAlpha(theme.gold, 1f), glowBoost);
            DrawHalves(vh, outer, outerHalf, theme.outerLine, px, bright, false);
            DrawHalves(vh, inner, innerHalf, theme.innerLine, px, bright, false);

            // Dot–line–dot on each side, once the drawing has passed the middle of the sides.
            float motif = sideMotif ? Mathf.Clamp01((progress - SideMiddleAt) / 0.15f) : 0f;
            if (motif > 0f)
            {
                var r = rectTransform.rect;
                Color32 c = UIKit.WithAlpha(bright, bright.a * motif);
                float len = theme.sideMotifLength;
                for (int s = -1; s <= 1; s += 2)
                {
                    float x0 = s < 0 ? r.xMin : r.xMax;
                    float cy = r.center.y;
                    VectorMesh.Disc(vh, new Vector2(x0 + s * 8f, cy), 2f, px, c, 12);
                    draw.Clear();
                    draw.Add(new Vector2(x0 + s * 14f, cy));
                    draw.Add(new Vector2(x0 + s * (len - 6f), cy));
                    VectorMesh.Stroke(vh, draw, 0, 2, false, 1f, px, c);
                    VectorMesh.Disc(vh, new Vector2(x0 + s * len, cy), 2f, px, c, 12);
                }
            }
        }

        // The whole inner outline (no gaps) as a triangle fan from the center — the shape is star-shaped around it.
        void Fill(VertexHelper vh, Color color)
        {
            var r = rectTransform.rect;
            fill.Clear();
            HalfPath(fill, r.center.x, r.xMin, r.yMax, r.yMin, theme.cornerNotch, theme.lineGap, 0f, 0f);
            int half = fill.Count;
            for (int i = half - 1; i >= 0; i--) fill.Add(new Vector2(2f * r.center.x - fill[i].x, fill[i].y));
            Color32 c = color;
            int center = vh.currentVertCount;
            vh.AddVert(r.center, c, Vector4.zero);
            foreach (var p in fill) vh.AddVert(p, c, Vector4.zero);
            for (int i = 0; i < fill.Count; i++) vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % fill.Count);
        }

        // Left half then its mirror, both truncated to the current progress.
        void DrawHalves(VertexHelper vh, List<Vector2> path, int count, float width, float feather, Color color, bool glow)
        {
            float length = VectorMesh.Length(path, 0, count) * Mathf.Clamp01(progress);
            if (length <= 0f) return;
            Color32 c = color;
            float mirror = 2f * rectTransform.rect.center.x;
            for (int side = 0; side < 2; side++)
            {
                draw.Clear();
                VectorMesh.Truncate(path, 0, count, length, draw);
                if (side == 1)
                    for (int i = 0; i < draw.Count; i++) draw[i] = new Vector2(mirror - draw[i].x, draw[i].y);
                VectorMesh.Stroke(vh, draw, 0, draw.Count, false, glow ? feather : width, feather, c); // glow: soft triangle profile
            }
        }

        // Left halves of the outer and inner lines, starting at the medallion and ending at the crescent.
        void BuildHalves()
        {
            var r = rectTransform.rect;
            float left = r.xMin, top = r.yMax, bottom = r.yMin;
            float notch = theme.cornerNotch, gap = theme.lineGap;
            float radius = MedallionRadius;
            float medal = radius > 0f ? radius + theme.medallionGap : 0f, moon = crescent ? theme.crescentGap : 0f;
            float cx = r.center.x;

            outer.Clear();
            HalfPath(outer, cx, left, top, bottom, notch, 0f, medal, moon);
            outerHalf = outer.Count;
            inner.Clear();
            HalfPath(inner, cx, left, top, bottom, notch, gap, medal, moon);
            innerHalf = inner.Count;
            // The glow doesn't break for the crescent (a gap in it would read as a dark notch) and ends under the
            // medallion's disc at the top.
            glowPath.Clear();
            HalfPath(glowPath, cx, left, top, bottom, notch, 0f, radius * 0.8f, 0f);
            glowHalf = glowPath.Count;
        }

        // From the top gap leftward, around the concave top-left corner, down the side, around the bottom-left corner
        // and right to the bottom gap. inset = distance inside the outer line.
        static void HalfPath(List<Vector2> pts, float cx, float left, float top, float bottom, float notch, float inset,
            float topGap, float bottomGap)
        {
            float R = notch + inset;                        // concave arcs stay centered on the frame corners
            float a = Mathf.Asin(Mathf.Clamp01(inset / R)); // where the inset line meets the arc
            float yTop = top - inset, yBottom = bottom + inset;
            float tg = Mathf.Sqrt(Mathf.Max(topGap * topGap - inset * inset, 0f));
            float bg = Mathf.Sqrt(Mathf.Max(bottomGap * bottomGap - inset * inset, 0f));

            pts.Add(new Vector2(cx - tg, yTop));
            const int n = 10;
            for (int i = 0; i <= n; i++) // top-left: from -a down to -(90° - a) around (left, top)
            {
                float t = Mathf.Lerp(-a, -(Mathf.PI / 2f - a), i / (float)n);
                pts.Add(new Vector2(left + Mathf.Cos(t) * R, top + Mathf.Sin(t) * R));
            }
            for (int i = 0; i <= n; i++) // bottom-left: from 90° - a down to a around (left, bottom)
            {
                float t = Mathf.Lerp(Mathf.PI / 2f - a, a, i / (float)n);
                pts.Add(new Vector2(left + Mathf.Cos(t) * R, bottom + Mathf.Sin(t) * R));
            }
            pts.Add(new Vector2(cx - bg, yBottom));
        }

        // Share of the path length up to point index `k` of arc `arc` (1 = end of the top-left corner).
        static float ArcShare(List<Vector2> pts, int count, int arc)
        {
            float total = VectorMesh.Length(pts, 0, count);
            float upTo = VectorMesh.Length(pts, 0, Mathf.Min(count, 12 * arc));
            return total > 0f ? upTo / total : 0f;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// A constellation drawn like the wand trail: a thin line with a soft glow through a list of points, drawn up to
    /// <see cref="progress"/> (share of its length). <see cref="Head"/> is the moving tip while it draws.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConstellationLine : MaskableGraphic
    {
        public float lineWidth = 2f;
        public float glowWidth = 16f;
        public Color glowColor = new(1f, 1f, 1f, 0.2f);
        [Range(0f, 1f)] public float progress = 1f;

        readonly List<Vector2> points = new(), draw = new();
        readonly List<float> shares = new();
        float length;

        public int Count => points.Count;
        public Vector2 Point(int i) => points[i];
        /// <summary>Share of the line (0..1) at which point <paramref name="i"/> is reached.</summary>
        public float ShareAt(int i) => shares[i];

        public void SetPoints(IReadOnlyList<Vector2> pts)
        {
            points.Clear();
            shares.Clear();
            for (int i = 0; i < pts.Count; i++) points.Add(pts[i]);
            length = VectorMesh.Length(points, 0, points.Count);
            float run = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) run += Vector2.Distance(points[i - 1], points[i]);
                shares.Add(length > 0f ? run / length : 0f);
            }
            SetVerticesDirty();
        }

        public void SetProgress(float p)
        {
            if (Mathf.Approximately(p, progress)) return;
            progress = p;
            SetVerticesDirty();
        }

        /// <summary>The tip of the drawn part.</summary>
        public Vector2 Head
        {
            get
            {
                if (points.Count == 0) return Vector2.zero;
                float target = length * Mathf.Clamp01(progress);
                for (int i = 1; i < points.Count; i++)
                {
                    float d = Vector2.Distance(points[i - 1], points[i]);
                    if (target <= d) return Vector2.Lerp(points[i - 1], points[i], d > 0f ? target / d : 1f);
                    target -= d;
                }
                return points[^1];
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2 || progress <= 0f) return;
            draw.Clear();
            VectorMesh.Truncate(points, 0, points.Count, length * Mathf.Clamp01(progress), draw);
            float px = VectorMesh.Pixel(this);
            float half = glowWidth * 0.5f;
            VectorMesh.Stroke(vh, draw, 0, draw.Count, false, half, half, glowColor * color);
            VectorMesh.Stroke(vh, draw, 0, draw.Count, false, lineWidth, px, color);
        }
    }

    /// <summary>
    /// A zodiac figure (stars sized by magnitude, the lines between them) scaled to the rect's width.
    /// <see cref="progress"/> draws the lines one after another, like the house reveal on the map; stars appear as the
    /// lines reach them.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConstellationFigure : MaskableGraphic
    {
        public float lineWidth = 1.5f;
        public float starRadius = 5f;
        [Range(0f, 1f)] public float progress = 1f;

        ZodiacConstellations.Constellation figure;
        readonly List<Vector2> draw = new();

        public void SetFigure(ZodiacConstellations.Constellation f)
        {
            figure = f;
            SetVerticesDirty();
        }

        public void SetProgress(float p)
        {
            if (Mathf.Approximately(p, progress)) return;
            progress = p;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (figure == null || figure.stars == null || figure.lines == null) return;
            float px = VectorMesh.Pixel(this);
            float size = rectTransform.rect.width;
            Color32 c = color;
            int count = figure.lines.Length;
            float drawn = Mathf.Clamp01(progress) * count;
            for (int i = 0; i < count && i < drawn; i++)
            {
                var l = figure.lines[i];
                if (l.x < 0 || l.y < 0 || l.x >= figure.stars.Length || l.y >= figure.stars.Length) continue;
                var a = figure.stars[l.x] * size;
                var b = figure.stars[l.y] * size;
                draw.Clear();
                draw.Add(a);
                draw.Add(Vector2.Lerp(a, b, Mathf.Clamp01(drawn - i)));
                VectorMesh.Stroke(vh, draw, 0, 2, false, lineWidth, px, c);
            }
            // A star shows once a drawn line touches it (all of them at full progress).
            for (int s = 0; s < figure.stars.Length; s++)
            {
                if (progress < 1f && !Reached(s, drawn)) continue;
                float mag = figure.magnitudes != null && s < figure.magnitudes.Length ? figure.magnitudes[s] : 3f;
                float r = starRadius * Mathf.Lerp(1.4f, 0.5f, Mathf.InverseLerp(1f, 5f, mag));
                VectorMesh.Disc(vh, figure.stars[s] * size, r, px * 1.5f, c, 14);
            }
        }

        bool Reached(int star, float drawn)
        {
            for (int i = 0; i < figure.lines.Length && i < drawn; i++)
            {
                var l = figure.lines[i];
                if (l.x == star || (l.y == star && drawn - i >= 1f)) return true;
            }
            return false;
        }
    }
}
