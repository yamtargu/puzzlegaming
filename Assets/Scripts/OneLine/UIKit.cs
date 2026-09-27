using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Building blocks for the night-sky UI, all procedural: rounded glass panels, circular glass buttons,
    /// icons, text with a soft glow, press feedback, safe-area root, panel open/close animation.
    /// Every visual value comes from UIStyle.
    /// </summary>
    public static class UIKit
    {
        // ---------- sprites ----------

        static Sprite roundFill, roundLine, thinRing, fadeLine, gear, restart, sparkle, coin, cross;

        /// <summary>9-sliced rounded rectangle, 32 px corner radius at 100 px/unit (= 32 reference units).</summary>
        public static Sprite RoundFill => roundFill ? roundFill : roundFill = RoundedRect(96, 32, 0f);
        /// <summary>Outline-only version of RoundFill, 2 px stroke.</summary>
        public static Sprite RoundLine => roundLine ? roundLine : roundLine = RoundedRect(96, 32, 2f);
        /// <summary>Circle outline with a thin stroke, for glass buttons.</summary>
        public static Sprite ThinRing => thinRing ? thinRing : thinRing = Shape(256, p =>
        {
            float r = p.magnitude;
            return Mathf.Clamp01(1f - Mathf.Abs(r - 0.975f) / 0.02f);
        });
        /// <summary>Horizontal line fading in from the left end (flip X for the right-hand ornament).</summary>
        public static Sprite FadeLine => fadeLine ? fadeLine : fadeLine = Shape(128, 8, p =>
        {
            float along = (p.x + 1f) * 0.5f;
            float across = 1f - Mathf.Abs(p.y);
            return along * along * Mathf.Clamp01(across * 2f);
        });
        public static Sprite Gear => gear ? gear : gear = Shape(128, p =>
        {
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
            float teeth = Mathf.SmoothStep(0f, 1f, (Mathf.Cos(a * 8f) - 0.1f) * 3f);
            float outer = 0.68f + 0.16f * teeth;
            return Edge(outer - r) * Edge(r - 0.3f);
        });
        public static Sprite Restart => restart ? restart : restart = Shape(128, p =>
        {
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg; // -180..180
            bool inGap = a > 35f && a < 95f;                                   // opening at the top right
            float ring = Edge(0.13f - Mathf.Abs(r - 0.62f)) * (inGap ? 0f : 1f);
            // Arrow head at the end of the arc (angle 95 deg), pointing along the circle (counter-clockwise).
            var tip = new Vector2(Mathf.Cos(95f * Mathf.Deg2Rad), Mathf.Sin(95f * Mathf.Deg2Rad)) * 0.62f;
            var dir = new Vector2(-tip.y, tip.x).normalized * -1f;
            var side = new Vector2(-dir.y, dir.x);
            Vector2 q = p - tip;
            float along = Vector2.Dot(q, dir), across = Mathf.Abs(Vector2.Dot(q, side));
            float head = along > -0.02f && along < 0.32f && across < (0.32f - along) * 0.85f ? 1f : 0f;
            return Mathf.Max(ring, head);
        });
        /// <summary>Four-pointed sparkle — the hint icon and the twinkles.</summary>
        public static Sprite Sparkle => sparkle ? sparkle : sparkle = Shape(128, p =>
        {
            float d = Mathf.Sqrt(Mathf.Abs(p.x)) + Mathf.Sqrt(Mathf.Abs(p.y));
            return Edge((0.95f - d) * 2f);
        });
        /// <summary>Star inside a thin ring — the coin icon.</summary>
        public static Sprite Coin => coin ? coin : coin = Shape(128, p =>
        {
            float r = p.magnitude;
            float ring = Edge(0.07f - Mathf.Abs(r - 0.86f));
            float a = Mathf.Atan2(p.y, p.x) - Mathf.PI / 2f;
            float k = Mathf.Cos(Mathf.PI / 5f) / Mathf.Cos(Mathf.Repeat(a, 2f * Mathf.PI / 5f) - Mathf.PI / 5f);
            float starR = Mathf.Lerp(0.26f, 0.58f, Mathf.Pow(Mathf.Clamp01((k - 0.81f) / 0.19f), 3f));
            return Mathf.Max(ring, Edge((starR - r) * 3f));
        });
        public static Sprite Cross => cross ? cross : cross = Shape(96, p =>
        {
            float d1 = Mathf.Abs(p.x - p.y), d2 = Mathf.Abs(p.x + p.y);
            float inBox = Mathf.Abs(p.x) < 0.62f && Mathf.Abs(p.y) < 0.62f ? 1f : 0f;
            return Edge(0.1f - Mathf.Min(d1, d2)) * inBox;
        });

        static Sprite crescent, starMap;

        /// <summary>Crescent moon with the horns up — the ornament at the bottom of the title cartouche.</summary>
        public static Sprite Crescent => crescent ? crescent : crescent = Shape(96, p =>
            Edge((0.8f - p.magnitude) * 1.2f) * (1f - Edge((0.7f - (p - new Vector2(0f, 0.3f)).magnitude) * 1.2f)));

        /// <summary>
        /// Tileable 128 px patch of a star chart: tiny dots on a jittered grid. Use with Image.Type.Tiled at low alpha.
        /// </summary>
        public static Sprite StarMap
        {
            get
            {
                if (starMap) return starMap;
                const int size = 128, cells = 4;
                var r = new AlphaRaster(size, size);
                var rnd = new System.Random(3);
                float cell = size / (float)cells;
                for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    var c = new Vector2((cx + 0.2f + 0.6f * (float)rnd.NextDouble()) * cell, (cy + 0.2f + 0.6f * (float)rnd.NextDouble()) * cell);
                    r.Disc(c, 0.7f + 0.9f * (float)rnd.NextDouble(), 0.5f + 0.5f * (float)rnd.NextDouble());
                }
                var tex = r.ToTexture();
                tex.wrapMode = TextureWrapMode.Repeat;
                return starMap = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        static float Edge(float signedDistance) => Mathf.Clamp01(signedDistance * 40f + 0.5f);

        static Sprite Shape(int size, Func<Vector2, float> alpha) => Shape(size, size, alpha);

        // Samples alpha(p) with p in [-1,1]^2, 2x2 supersampled.
        static Sprite Shape(int w, int h, Func<Vector2, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                for (int s = 0; s < 4; s++)
                {
                    var p = new Vector2((x + 0.25f + 0.5f * (s & 1)) / w * 2f - 1f, (y + 0.25f + 0.5f * (s >> 1)) / h * 2f - 1f);
                    a += alpha(p);
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a / 4f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // Rounded rectangle for 9-slicing; stroke 0 = filled.
        static Sprite RoundedRect(int size, int radius, float stroke)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = radius - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)); // >0 inside
                float a = stroke <= 0f ? Mathf.Clamp01(d + 0.5f) : Mathf.Clamp01(stroke * 0.5f + 0.5f - Mathf.Abs(d - stroke * 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // ---------- canvas / layout ----------

        /// <summary>Layout reference resolution (portrait); CanvasScaler matches width and height equally.</summary>
        public static readonly Vector2 ReferenceResolution = new(1080f, 1920f);

        /// <summary>Canvas units → screen pixels, as the CanvasScaler computes it (for input thresholds).</summary>
        public static float ReferenceScale =>
            Mathf.Sqrt(Screen.width / ReferenceResolution.x * (Screen.height / ReferenceResolution.y));

        /// <summary>pixelsPerUnitMultiplier that makes the 9-sliced RoundFill/RoundLine a pill at this height.</summary>
        public static float PillCorners(float height) => 2f * RoundCorner / Mathf.Max(1f, height);

        const float RoundCorner = 32f; // corner radius of RoundFill / RoundLine in pixels

        public static Canvas MakeCanvas(Transform parent, string name, int order, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            if (interactive)
            {
                go.AddComponent<GraphicRaycaster>();
                EnsureEventSystem();
            }
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>()) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos,
            Vector2 size, bool sliced = false)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        /// <summary>An empty vector graphic (strokes and discs are added by the caller).</summary>
        public static VectorGraphic Vector(Transform parent, string name, Vector2 anchor, Vector2 pos, float size)
        {
            var rt = Rect(parent, name, anchor, pos, new Vector2(size, size));
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<VectorGraphic>();
            g.raycastTarget = false;
            return g;
        }

        // ---------- vector icons ----------

        /// <summary>Astrolabe dial: rim with 12 ticks, inner ring, an alidade across and a hub — the settings icon.</summary>
        public static void DrawAstrolabe(VectorGraphic g, float radius, Color c, float width = 2.2f)
        {
            g.Clear();
            float rim = radius * 0.72f;
            g.Circle(Vector2.zero, rim, width, c, 64);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                g.Line(d * rim, d * (i % 3 == 0 ? radius : Mathf.Lerp(rim, radius, 0.6f)), width * 0.8f, c);
            }
            g.Circle(Vector2.zero, rim * 0.52f, width * 0.6f, c, 48);
            var diagonal = new Vector2(0.72f, 0.69f) * rim * 0.9f;
            g.Line(-diagonal, diagonal, width * 0.8f, c);
            g.Disc(Vector2.zero, radius * 0.1f, c);
        }

        /// <summary>A sun: ring, center, eight rays — the Celestial Pass icon and the "on" knob of a switch.</summary>
        public static void DrawSun(VectorGraphic g, float radius, Color c, float width = 2f)
        {
            g.Clear();
            g.Circle(Vector2.zero, radius * 0.48f, width, c, 48);
            g.Disc(Vector2.zero, radius * 0.18f, c);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                g.Line(d * radius * 0.68f, d * radius, width, c);
            }
        }

        /// <summary>Cassiopeia's "W" in a ring — the star-chart (map) icon.</summary>
        public static void DrawChart(VectorGraphic g, float radius, Color c, float width = 2f)
        {
            g.Clear();
            g.Circle(Vector2.zero, radius, width * 0.6f, WithAlpha(c, c.a * 0.6f), 64);
            Vector2[] stars = { new(-0.66f, 0.22f), new(-0.34f, -0.26f), new(0f, 0.12f), new(0.34f, -0.3f), new(0.66f, 0.2f) };
            for (int i = 0; i < stars.Length; i++) stars[i] *= radius;
            g.Polyline(stars, width * 0.7f, WithAlpha(c, c.a * 0.8f));
            for (int i = 0; i < stars.Length; i++) g.Disc(stars[i], radius * (i == 2 ? 0.16f : 0.11f), c);
        }

        // ---------- text ----------

        public static TMP_FontAsset TitleFont(UIStyle s) => s && s.titleFont ? s.titleFont : TMP_Settings.defaultFontAsset;
        public static TMP_FontAsset BodyFont(UIStyle s) => s && s.bodyFont ? s.bodyFont : TMP_Settings.defaultFontAsset;

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            Vector2 anchor, Vector2 pos, Vector2 box, float spacing = 0f, FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.characterSpacing = spacing;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Soft outer glow on a TMP text (its own material instance). Never a hard drop shadow.</summary>
        public static void Glow(TMP_Text t, Color color, float outer)
        {
            var m = t.fontMaterial; // instance
            m.EnableKeyword(ShaderUtilities.Keyword_Glow);
            color.a = Mathf.Clamp01(color.a);
            m.SetColor(ShaderUtilities.ID_GlowColor, color);
            m.SetFloat(ShaderUtilities.ID_GlowOuter, outer);
            m.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
            m.SetFloat(ShaderUtilities.ID_GlowPower, 0.8f);
            m.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
            t.UpdateMeshPadding();
        }

        public static void SetGlowColor(TMP_Text t, Color color) =>
            t.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, color);

        /// <summary>
        /// A band of light sweeping across a text: <paramref name="sweep"/> is the band's center (0 = left edge,
        /// 1 = right edge), <paramref name="width"/> its soft half-width in the same units. Rebuilds the mesh first,
        /// so call it every frame of the sweep; a plain ForceMeshUpdate afterwards removes it.
        /// </summary>
        public static void Shimmer(TMP_Text t, float sweep, Color shine, float width)
        {
            t.ForceMeshUpdate();
            var info = t.textInfo;
            if (info.characterCount == 0) return;
            var bounds = t.textBounds;
            float x0 = bounds.min.x, w = Mathf.Max(1f, bounds.size.x);
            Color32 light = shine;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                float d = ((c.bottomLeft.x + c.topRight.x) * 0.5f - x0) / w - sweep;
                float k = Mathf.Exp(-d * d / (width * width));
                var colors = info.meshInfo[c.materialReferenceIndex].colors32;
                for (int v = 0; v < 4; v++)
                {
                    var o = colors[c.vertexIndex + v];
                    colors[c.vertexIndex + v] = Color32.Lerp(o, new Color32(light.r, light.g, light.b, o.a), k);
                }
            }
            t.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // ---------- panels & buttons ----------

        /// <summary>Rounded glass card: translucent navy fill + thin light outline.</summary>
        public static RectTransform Card(Transform parent, string name, UIStyle s, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var fill = Image(parent, name, RoundFill, s.panelFill, anchor, pos, size, true);
            var line = Image(fill.transform, "Outline", RoundLine, s.outline, new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.sizeDelta = Vector2.zero;
            return fill.rectTransform;
        }

        /// <summary>Circular glass button: dark translucent fill, thin light ring, soft inner glow, an icon.</summary>
        public static Button GlassButton(Transform parent, string name, UIStyle s, Sprite icon, Vector2 anchor, Vector2 pos,
            float size, UnityAction onClick)
        {
            var fill = Image(parent, name, Art.Circle, s.glassFill, anchor, pos, new Vector2(size, size));
            fill.raycastTarget = true;
            Image(fill.transform, "Inner Glow", Art.SoftCircle, WithAlpha(Color.white, s.glassInnerGlow),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.9f, size * 0.9f));
            Image(fill.transform, "Ring", ThinRing, s.outline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            if (icon)
                Image(fill.transform, "Icon", icon, WithAlpha(s.text, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(s.iconSize, s.iconSize));
            return MakeButton(fill, s, onClick);
        }

        /// <summary>Rounded glass pill button with a label.</summary>
        public static Button PillButton(Transform parent, string name, UIStyle s, string label, Vector2 anchor, Vector2 pos,
            Vector2 size, UnityAction onClick, out TextMeshProUGUI text, Color? outline = null)
        {
            var card = Card(parent, name, s, anchor, pos, size);
            var img = card.GetComponent<Image>();
            img.raycastTarget = true;
            if (outline.HasValue) card.Find("Outline").GetComponent<Image>().color = outline.Value;
            text = Text(card, "Label", BodyFont(s), 34f, s.text, new Vector2(0.5f, 0.5f), Vector2.zero, size, 4f, FontStyles.Bold);
            text.text = label;
            return MakeButton(img, s, onClick);
        }

        static Button MakeButton(Graphic target, UIStyle s, UnityAction onClick)
        {
            var b = target.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; // PressFeedback does the animation
            b.targetGraphic = target;
            b.onClick.AddListener(onClick);
            target.gameObject.AddComponent<PressFeedback>().style = s;
            return b;
        }

        // ---------- panel with dim backdrop ----------

        /// <summary>
        /// Full-screen dim overlay (tap outside to close) holding a card framed like the level title: navy fill,
        /// gold double line with notched corners, a medallion on the top line (see <see cref="Cartouche"/>, on the
        /// card) and a crescent at the bottom. Starts hidden.
        /// </summary>
        public static (CanvasGroup group, RectTransform card) Modal(Transform canvasRoot, string name, UIStyle s,
            Vector2 cardSize, UnityAction onBackdrop)
        {
            var overlay = Stretch(canvasRoot, name);
            var dim = overlay.gameObject.AddComponent<Image>();
            dim.color = WithAlpha(s.starStyle ? s.starStyle.backgroundEdge : Color.black, s.dimAlpha);
            if (onBackdrop != null) overlay.gameObject.AddComponent<Button>().onClick.AddListener(onBackdrop);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            // The card itself is an invisible hit area, so taps on it don't reach the backdrop.
            var hit = Image(overlay, "Card", null, Color.clear, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
            hit.raycastTarget = true;
            var card = hit.rectTransform;
            card.gameObject.AddComponent<ClickBlocker>();
            Cartouche.Create(card, s, new Cartouche.Options
            {
                medallionRadius = s.panelMedallionRadius,
                crescent = true,
                fill = WithAlpha(s.panelFill, s.modalCardAlpha),
            });
            overlay.gameObject.SetActive(false);
            return (group, card);
        }

        /// <summary>The round glass close button in a panel's top-right corner.</summary>
        public static Button CloseButton(RectTransform card, UIStyle s, UnityAction onClick) =>
            GlassButton(card, "Close", s, Cross, new Vector2(1f, 1f), new Vector2(-s.closeButtonInset, -s.closeButtonInset),
                s.closeButtonSize, onClick);

        /// <summary>A panel's title under its medallion.</summary>
        public static TextMeshProUGUI PanelTitle(RectTransform card, UIStyle s, string title, Color color)
        {
            var t = Text(card, "Title", TitleFont(s), s.panelTitleSize, color, new Vector2(0.5f, 1f), new Vector2(0f, -s.panelTitleY),
                new Vector2(card.sizeDelta.x - 2f * s.closeButtonInset - s.closeButtonSize, s.panelTitleSize * 1.5f), 4f);
            t.text = title;
            return t;
        }

        public static void Open(CanvasGroup group, RectTransform card, UIStyle s)
        {
            group.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            Tween.Run(group, s.panelOpenTime, Ease.OutCubic, k =>
            {
                group.alpha = k;
                card.localScale = Vector3.one * Mathf.Lerp(s.panelScaleFrom, 1f, k);
            });
            if (card.TryGetComponent(out Cartouche frame)) frame.PlayDraw(s.panelDrawTime);
        }

        public static void Close(CanvasGroup group, RectTransform card, UIStyle s, Action done = null)
        {
            group.blocksRaycasts = false;
            Tween.Run(group, s.panelOpenTime * 0.7f, Ease.OutCubic, k =>
            {
                group.alpha = 1f - k;
                card.localScale = Vector3.one * Mathf.Lerp(1f, s.panelScaleFrom, k);
            }, () =>
            {
                group.gameObject.SetActive(false);
                done?.Invoke();
            });
        }

        // ---------- click sound / haptics ----------

        static AudioSource clickSource;
        static AudioClip clickClip;

        public static void Click()
        {
            if (!AudioService.SfxOn) return;
            if (!clickSource)
            {
                var go = new GameObject("[UI Click]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                clickSource = go.AddComponent<AudioSource>();
                clickSource.playOnAwake = false;
                AudioService.RouteSfx(clickSource);
                const int rate = 44100, n = rate / 30;
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float k = (float)i / n;
                    data[i] = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1500f, 900f, k) * i / rate) * 0.12f * (1f - k) * (1f - k);
                }
                clickClip = AudioClip.Create("ui-click", n, 1, rate, false);
                clickClip.SetData(data, 0);
            }
            clickSource.PlayOneShot(clickClip);
        }

        /// <summary>
        /// Light haptic tap. Unity has no built-in light haptic (Handheld.Vibrate is a long buzz), so this is a hook
        /// for a native plugin (iOS UIImpactFeedbackGenerator / Android HapticFeedbackConstants).
        /// </summary>
        public static void LightHaptic()
        {
            if (!AudioService.HapticsOn) return;
            // Native call goes here (iOS UIImpactFeedbackGenerator.light / Android HapticFeedbackConstants.KEYBOARD_TAP).
        }

        // ---------- small animations ----------

        /// <summary>A quick side-to-side wiggle ("not available").</summary>
        public static void Wiggle(Transform t, UIStyle s)
        {
            Tween.Run(t, s.wiggleTime, Ease.Linear,
                k => t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * s.wiggleDegrees * (1f - k)),
                () => t.localRotation = Quaternion.identity);
        }

        /// <summary>A celebratory gold burst (rays, sparkles and a soft flash) that fades by itself.</summary>
        public static void Burst(RectTransform parent, Vector2 pos, Color color, UIStyle s)
        {
            var root = Rect(parent, "Burst", new Vector2(0.5f, 0.5f), pos, Vector2.zero);
            var mid = new Vector2(0.5f, 0.5f);
            var flash = Image(root, "Flash", Art.SoftCircle, Color.clear, mid, Vector2.zero, Vector2.one * s.burstRadius);
            int count = Mathf.Max(3, s.burstRays);
            var rays = new Image[count];
            var sparks = new Image[count / 2];
            var lengths = new float[count];
            for (int i = 0; i < count; i++)
            {
                rays[i] = Image(root, "Ray", Art.Ray, Color.clear, mid, Vector2.zero, Vector2.zero);
                rays[i].rectTransform.pivot = new Vector2(0.5f, 0f); // grows outward from the center
                rays[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 360f / count + UnityEngine.Random.Range(-8f, 8f));
                lengths[i] = s.burstRadius * UnityEngine.Random.Range(0.55f, 1f);
            }
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = Image(root, "Spark", Sparkle, Color.clear, mid, Vector2.zero, Vector2.one * s.burstRadius * 0.07f);
            Tween.Run(root, s.burstTime, Ease.Linear, k =>
            {
                float grow = Ease.OutCubic(k);
                float fade = k < 0.12f ? k / 0.12f : 1f - Ease.InOutSine((k - 0.12f) / 0.88f);
                flash.color = WithAlpha(color, 0.35f * fade);
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, grow);
                for (int i = 0; i < count; i++)
                {
                    rays[i].rectTransform.sizeDelta = new Vector2(s.burstRadius * 0.03f * (1f - 0.5f * k), lengths[i] * grow);
                    rays[i].color = WithAlpha(color, fade);
                }
                for (int i = 0; i < sparks.Length; i++)
                {
                    float a = (i * 2 + 1) * Mathf.PI / sparks.Length;
                    sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * lengths[i] * 0.8f * grow;
                    sparks[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                    sparks[i].color = WithAlpha(color, fade);
                }
            }, () => UnityEngine.Object.Destroy(root.gameObject));
        }
    }

    /// <summary>Press feedback: shrink on press, overshoot on release, soft click + light haptic.</summary>
    public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public UIStyle style;
        Tween tween;

        public void OnPointerDown(PointerEventData e)
        {
            if (!style) return;
            tween?.Kill();
            float from = transform.localScale.x;
            tween = Tween.Run(this, style.pressTime, Ease.OutQuad,
                k => transform.localScale = Vector3.one * Mathf.Lerp(from, style.pressScale, k));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!style) return;
            tween?.Kill();
            float from = transform.localScale.x;
            tween = Tween.Run(this, style.releaseTime, Ease.Linear, k =>
            {
                float s = k < 0.4f
                    ? Mathf.Lerp(from, style.releaseOvershoot, Ease.OutQuad(k / 0.4f))
                    : Mathf.Lerp(style.releaseOvershoot, 1f, Ease.OutCubic((k - 0.4f) / 0.6f));
                transform.localScale = Vector3.one * s;
            });
            UIKit.Click();
            UIKit.LightHaptic();
        }
    }

    /// <summary>Swallows clicks so they don't reach a parent's click handler.</summary>
    public class ClickBlocker : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) { }
    }

    /// <summary>Keeps a full-screen RectTransform inside Screen.safeArea (notches, home indicator).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        /// <summary>Editor testing: extra insets in pixels (top, bottom) to fake a notch.</summary>
        public static Vector2 DebugInsets;

        RectTransform rt;
        Rect applied;
        Vector2 appliedScreen, appliedDebug;

        public Rect Current { get; private set; }

        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update() => Apply();

        void Apply()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2(Screen.width, Screen.height);
            if (safe == applied && screen == appliedScreen && DebugInsets == appliedDebug) return;
            applied = safe;
            appliedScreen = screen;
            appliedDebug = DebugInsets;
            safe.yMax = Mathf.Min(safe.yMax, screen.y - DebugInsets.x);
            safe.yMin = Mathf.Max(safe.yMin, DebugInsets.y);
            Current = safe;
            if (screen.x <= 0 || screen.y <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
