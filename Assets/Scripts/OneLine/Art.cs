using UnityEngine;
using UnityEngine.Rendering;

namespace OneLine
{
    /// <summary>Procedural placeholder art so the prototype needs no imported assets.</summary>
    public static class Art
    {
        static Sprite circle;
        static Sprite square;
        static Material spriteMaterial;

        static Sprite softCircle;

        public static Sprite Circle => circle ? circle : circle = MakeCircle(128);

        /// <summary>White disc with a smooth falloff to transparent — for soft shadows and background blobs.</summary>
        public static Sprite SoftCircle => softCircle ? softCircle : softCircle = MakeSoftCircle(128);

        static Sprite star, ring, ray;
        static Material dashedMaterial, softLineMaterial;

        /// <summary>
        /// Sprite material whose texture fades out across the line's width — a LineRenderer with it looks like
        /// a soft glow instead of a hard-edged band.
        /// </summary>
        public static Material SoftLineMaterial
        {
            get
            {
                if (softLineMaterial) return softLineMaterial;
                const int h = 64;
                var tex = new Texture2D(4, h, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < h; y++)
                {
                    float across = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f); // 0 at the edges, 1 in the middle
                    float a = across * across * (3f - 2f * across);           // smoothstep
                    for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
                tex.Apply();
                softLineMaterial = new Material(SpriteMaterial) { name = "Soft Line", mainTexture = tex };
                return softLineMaterial;
            }
        }

        /// <summary>Anti-aliased 5-pointed star, 1 world unit across (point up).</summary>
        public static Sprite Star => star ? star : star = MakeStar(160);

        /// <summary>Half the width of <see cref="StarQuad"/>, in star radii: room for the halo, glint and pirouette hop.</summary>
        public const float StarQuadRadii = 3f;
        static Sprite starQuad;

        /// <summary>
        /// Plain quad for the StarSDF shader, sized so the star it draws is 1 world unit across (like <see cref="Star"/>);
        /// the quad itself is <see cref="StarQuadRadii"/> times wider.
        /// </summary>
        public static Sprite StarQuad => starQuad ? starQuad : starQuad = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f / StarQuadRadii, 0, SpriteMeshType.FullRect);

        static Sprite mote;

        /// <summary>A mote of light, 1 world unit across: a bright core with a short soft halo — for fairy dust.</summary>
        public static Sprite Mote => mote ? mote : mote = MakeMote(64);

        static Sprite flake;

        /// <summary>Outline of a gold-leaf flake, centered on 0, about 1 unit across (also drawn by WandPreview).</summary>
        public static readonly Vector2[] FlakeShape =
            { new(-0.32f, -0.2f), new(0.12f, -0.4f), new(0.4f, -0.02f), new(0.2f, 0.38f), new(-0.28f, 0.22f) };

        /// <summary>
        /// A gold-leaf flake, 1 world unit across: an irregular white shard, brighter toward one corner, so it reads
        /// as metal when tinted gold. For Gold Ink's dust.
        /// </summary>
        public static Sprite Flake => flake ? flake : flake = MakeFlake(48);

        /// <summary>Thin anti-aliased ring, 1 world unit across — for the sonar pulse.</summary>
        public static Sprite Ring => ring ? ring : ring = MakeRing(160, 0.035f);

        /// <summary>Thin vertical light ray, bright near the base, fading out toward the tip. Pivot at the base.</summary>
        public static Sprite Ray => ray ? ray : ray = MakeRay(16, 128);

        /// <summary>Sprite material with a dash texture, for LineRenderers in Tile mode (dashed lines).</summary>
        public static Material DashedMaterial
        {
            get
            {
                if (dashedMaterial) return dashedMaterial;
                var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < 8; x++) tex.SetPixel(x, 0, new Color(1, 1, 1, x < 4 ? 1f : 0f));
                tex.Apply();
                dashedMaterial = new Material(SpriteMaterial) { name = "Dashed Line", mainTexture = tex };
                return dashedMaterial;
            }
        }

        static Sprite roundedRect;

        /// <summary>Rounded rectangle for SpriteDrawMode.Sliced: 1 unit square, corners 0.375 units.</summary>
        public static Sprite RoundedRect => roundedRect ? roundedRect : roundedRect = MakeRoundedRect(64, 24);

        public static Sprite Square => square ? square : square =
            Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

        // The render pipeline's default sprite material, reused for LineRenderers and particles.
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial) return spriteMaterial;
                var rp = GraphicsSettings.currentRenderPipeline;
                spriteMaterial = rp && rp.default2DMaterial
                    ? rp.default2DMaterial
                    : new Material(Shader.Find("Sprites/Default"));
                return spriteMaterial;
            }
        }

        static Material unlitSpriteMaterial;

        /// <summary>
        /// Sprite material that ignores 2D lights — for background art whose opacity must stay exactly as set
        /// (the node glow lights would otherwise brighten it near the path).
        /// </summary>
        public static Material UnlitSpriteMaterial
        {
            get
            {
                if (unlitSpriteMaterial) return unlitSpriteMaterial;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                return unlitSpriteMaterial = shader ? new Material(shader) { name = "Unlit Sprite" } : SpriteMaterial;
            }
        }

        // Anti-aliased white disc, 1 world unit wide.
        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // Filled 5-pointed star via point-in-polygon with 4x4 supersampling for smooth edges.
        static Sprite MakeStar(int size)
        {
            var poly = new Vector2[10];
            float outer = size * 0.5f - 1f, inner = outer * 0.47f;
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f; // first point straight up
                float r = i % 2 == 0 ? outer : inner;
                poly[i] = new Vector2(size * 0.5f + Mathf.Cos(a) * r, size * 0.5f + Mathf.Sin(a) * r);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (Inside(poly, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // Ring whose stroke is `thickness` of the diameter.
        static Sprite MakeRing(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f, half = size * thickness * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01(half + 0.5f - Mathf.Abs(d - (r - half)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // w x h texture: soft across the width, fading from bright (base) to nothing (tip).
        static Sprite MakeRay(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);      // 0 edge .. 1 center
                float along = 1f - (y + 0.5f) / h;                             // 1 base .. 0 tip
                float a = Mathf.SmoothStep(0f, 1f, across) * along * along;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            // 1 unit tall, 1 unit wide after scaling; pivot at the base so it grows outward.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h);
        }

        static Sprite MakeRoundedRect(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance outside the inner rectangle whose corners are rounded by `radius`.
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // Opaque core that fades out smoothly toward the edge, 1 world unit wide.
        static Sprite MakeSoftCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r; // 0 center, 1 edge
                float a = 1f - Mathf.SmoothStep(0.35f, 1f, d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeFlake(int size)
        {
            var poly = new Vector2[FlakeShape.Length];
            for (int i = 0; i < poly.Length; i++) poly[i] = (FlakeShape[i] + new Vector2(0.5f, 0.5f)) * size;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (Inside(poly, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                byte shade = (byte)(Mathf.Lerp(0.6f, 1f, (x + y) / (2f * size)) * 255); // light from the top right
                px[y * size + x] = new Color32(shade, shade, shade, (byte)(hits * 255 / 16));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeMote(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r; // 0 center, 1 edge
                float core = Mathf.Clamp01((0.32f - d) / 0.1f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f) * 0.75f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(core, halo) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
