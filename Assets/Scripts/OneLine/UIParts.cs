using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// The celestial frame shared by the level title and every panel: a dark fill inside a double gold line with
    /// notched star-chart corners, corner stars, a faint star-map texture, an optional medallion on the top line (with
    /// an astrolabe dial and a glyph or icon) and an optional crescent on the bottom line.
    /// </summary>
    public class Cartouche : MonoBehaviour
    {
        public struct Options
        {
            /// <summary>Medallion radius on the top line; 0 = none.</summary>
            public float medallionRadius;
            public bool crescent;
            public bool sideMotif;
            public Color fill;
        }

        public CelestialFrame Frame { get; private set; }
        public Image StarMap { get; private set; }
        public Image[] CornerStars { get; } = new Image[4];
        public Image Crescent { get; private set; }
        public RectTransform Medallion { get; private set; }
        public CanvasGroup MedallionGroup { get; private set; }
        public RectTransform Dial { get; private set; }
        public Image MedallionGlow { get; private set; }
        /// <summary>Vector glyph in the medallion (zodiac sign, astrolabe, sun…).</summary>
        public VectorGraphic Glyph { get; private set; }
        /// <summary>Sprite icon in the medallion, used instead of the glyph.</summary>
        public Image Icon { get; private set; }

        ZodiacTheme theme;
        float radius, dialAngle;
        Tween drawTween, glowTween, turnTween;

        public static Cartouche Create(RectTransform root, UIStyle style, Options options)
        {
            var c = root.gameObject.AddComponent<Cartouche>();
            c.Build(style, options);
            return c;
        }

        void Build(UIStyle style, Options o)
        {
            theme = style.Zodiac;
            radius = o.medallionRadius;
            var root = (RectTransform)transform;
            var mid = new Vector2(0.5f, 0.5f);
            var line = theme.Line;

            var lines = UIKit.Stretch(root, "Frame");
            lines.gameObject.AddComponent<CanvasRenderer>();
            Frame = lines.gameObject.AddComponent<CelestialFrame>();
            Frame.theme = theme;
            Frame.color = line;
            Frame.fillColor = o.fill;
            Frame.medallionRadius = o.medallionRadius;
            Frame.crescent = o.crescent;
            Frame.sideMotif = o.sideMotif;
            Frame.raycastTarget = false;

            // Faint star-chart texture inside the frame.
            StarMap = UIKit.Image(root, "Star Map", UIKit.StarMap, UIKit.WithAlpha(style.text, theme.starMapAlpha), mid,
                Vector2.zero, Vector2.zero);
            StarMap.type = Image.Type.Tiled;
            var map = StarMap.rectTransform;
            map.anchorMin = Vector2.zero;
            map.anchorMax = Vector2.one;
            map.offsetMin = Vector2.one * theme.starMapInset;
            map.offsetMax = -Vector2.one * theme.starMapInset;

            for (int i = 0; i < CornerStars.Length; i++)
                CornerStars[i] = UIKit.Image(root, "Corner Star", UIKit.Sparkle, line, mid, Vector2.zero,
                    Vector2.one * theme.cornerStarSize);
            if (o.crescent)
                Crescent = UIKit.Image(root, "Crescent", UIKit.Crescent, line, new Vector2(0.5f, 0f), Vector2.zero,
                    Vector2.one * theme.crescentSize);
            if (radius > 0f) BuildMedallion(style, line);
            LayoutCorners();
        }

        // Dark disc, soft glow, astrolabe dial (turns on a win), glyph / icon.
        void BuildMedallion(UIStyle style, Color line)
        {
            var mid = new Vector2(0.5f, 0.5f);
            float box = 2f * (radius + theme.dialTick * 2f);
            Medallion = UIKit.Rect(transform, "Medallion", new Vector2(0.5f, 1f), Vector2.zero, Vector2.one * box);
            MedallionGroup = Medallion.gameObject.AddComponent<CanvasGroup>();
            var disc = style.starStyle ? style.starStyle.backgroundCenter : new Color(0.07f, 0.09f, 0.16f);
            UIKit.Image(Medallion, "Disc", Art.Circle, UIKit.WithAlpha(disc, theme.medallionDiscAlpha), mid, Vector2.zero,
                Vector2.one * (2f * radius));
            MedallionGlow = UIKit.Image(Medallion, "Glow", Art.SoftCircle, UIKit.WithAlpha(theme.gold, theme.medallionGlowAlpha),
                mid, Vector2.zero, Vector2.one * (2.3f * radius));
            var dial = UIKit.Vector(Medallion, "Dial", mid, Vector2.zero, box);
            dial.Dial(radius, theme.dialWidth, line, theme.dialTick);
            Dial = dial.rectTransform;
            Glyph = UIKit.Vector(Medallion, "Glyph", mid, Vector2.zero, 2f * radius);
            Icon = UIKit.Image(Medallion, "Icon", null, theme.gold, mid, Vector2.zero, Vector2.one * radius);
            Icon.gameObject.SetActive(false);
        }

        public float MedallionRadius => radius;

        /// <summary>Puts glyph strokes (in -1..1) in the medallion.</summary>
        public void SetGlyph(IReadOnlyList<Vector2[]> strokes, float strokeWidth)
        {
            if (!Glyph) return;
            Icon.gameObject.SetActive(false);
            Glyph.Clear();
            Glyph.Glyph(strokes, Vector2.zero, radius * 0.5f, strokeWidth, theme.gold);
        }

        /// <summary>Puts a sprite icon in the medallion.</summary>
        public void SetIcon(Sprite sprite)
        {
            if (!Icon) return;
            Glyph.Clear();
            Icon.sprite = sprite;
            Icon.gameObject.SetActive(true);
        }

        public void LayoutCorners()
        {
            if (!Frame) return;
            for (int i = 0; i < CornerStars.Length; i++)
                if (CornerStars[i]) CornerStars[i].rectTransform.anchoredPosition = Frame.CornerStar(i);
        }

        void OnRectTransformDimensionsChange() => LayoutCorners();

        /// <summary>Everything hidden, ready to draw.</summary>
        public void Hide()
        {
            drawTween?.Kill();
            Frame.SetProgress(0f);
            Frame.SetGlow(0f);
            if (MedallionGroup) MedallionGroup.alpha = 0f;
            if (MedallionGlow) MedallionGlow.color = UIKit.WithAlpha(theme.gold, theme.medallionGlowAlpha);
            if (Crescent) Crescent.color = UIKit.WithAlpha(theme.gold, 0f);
            foreach (var s in CornerStars) s.rectTransform.localScale = Vector3.zero;
        }

        /// <summary>Everything drawn, at once (ends a draw that was cut short).</summary>
        public void Show()
        {
            drawTween?.Kill();
            Frame.SetProgress(1f);
            if (MedallionGroup) MedallionGroup.alpha = 1f;
            if (Medallion) Medallion.localScale = Vector3.one;
            if (Crescent) Crescent.color = theme.Line;
            foreach (var s in CornerStars)
            {
                s.rectTransform.localScale = Vector3.one;
                s.rectTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// The medallion appears, the lines draw themselves from it outward and around, then the corner stars twinkle
        /// in one by one.
        /// </summary>
        public Tween PlayDraw(float drawTime, Action done = null)
        {
            Hide();
            float starsAt = drawTime * 0.5f;
            float total = Mathf.Max(drawTime, starsAt + (CornerStars.Length - 1) * theme.cornerStagger + theme.cornerTwinkleTime);
            var lineColor = theme.Line;
            drawTween = Tween.Run(this, total, Ease.Linear, k =>
            {
                float t = k * total;
                if (Medallion)
                {
                    float m = Ease.OutCubic(Mathf.Clamp01(t / theme.medallionInTime));
                    MedallionGroup.alpha = m;
                    Medallion.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, m);
                }
                float p = Ease.OutCubic(Mathf.Clamp01(t / drawTime));
                Frame.SetProgress(p);
                if (Crescent) Crescent.color = UIKit.WithAlpha(lineColor, lineColor.a * Mathf.Clamp01((p - 0.9f) / 0.1f));
                for (int i = 0; i < CornerStars.Length; i++)
                {
                    float u = Mathf.Clamp01((t - starsAt - i * theme.cornerStagger) / theme.cornerTwinkleTime);
                    float s = u < 0.5f ? Mathf.Lerp(0f, 1.35f, Ease.OutQuad(u / 0.5f)) : Mathf.Lerp(1.35f, 1f, (u - 0.5f) / 0.5f);
                    var star = CornerStars[i].rectTransform;
                    star.localScale = Vector3.one * s;
                    star.localRotation = Quaternion.Euler(0f, 0f, 45f * (1f - u));
                }
            }, done);
            return drawTween;
        }

        /// <summary>Win: the frame glows gold briefly and the dial turns one sign.</summary>
        public void Celebrate()
        {
            glowTween?.Kill();
            glowTween = Tween.Run(Frame, theme.winGlowTime, Ease.Linear, k =>
            {
                float boost = k < 0.25f ? Ease.OutQuad(k / 0.25f) : 1f - Ease.InOutSine((k - 0.25f) / 0.75f);
                Frame.SetGlow(boost);
                if (MedallionGlow) MedallionGlow.color = UIKit.WithAlpha(theme.gold, Mathf.Lerp(theme.medallionGlowAlpha, 0.4f, boost));
            });
            if (!Dial) return;
            float from = dialAngle, to = dialAngle - theme.medallionTurn;
            dialAngle = to;
            turnTween?.Kill();
            turnTween = Tween.Run(Dial, theme.medallionTurnTime, Ease.OutCubic,
                k => Dial.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, to, k)));
        }
    }

    /// <summary>"— ♉ TAURUS · 38% —": the sign's glyph and a label between two fading ornaments, centered as a group.</summary>
    public sealed class SignLine
    {
        public RectTransform Root { get; }
        /// <summary>Width of glyph + gap + label (without the ornaments).</summary>
        public float ContentWidth { get; private set; }

        readonly UIStyle style;
        readonly VectorGraphic glyph;
        readonly TextMeshProUGUI label;
        readonly Image left, right;
        readonly float glyphSize;
        readonly Color color;
        float ornamentLength;
        int sign = -1;

        public SignLine(Transform parent, UIStyle s, Vector2 anchor, Vector2 pos, float fontSize, float spacing, float glyphSize,
            Color color)
        {
            style = s;
            this.glyphSize = glyphSize;
            this.color = color;
            ornamentLength = s.ornamentLength;
            var mid = new Vector2(0.5f, 0.5f);
            Root = UIKit.Rect(parent, "Sign Line", anchor, pos, new Vector2(0f, glyphSize));
            glyph = UIKit.Vector(Root, "Glyph", mid, Vector2.zero, glyphSize);
            label = UIKit.Text(Root, "Label", UIKit.TitleFont(s), fontSize, color, mid, Vector2.zero,
                new Vector2(s.signLabelBox, glyphSize * 1.5f), spacing);
            var ornament = UIKit.WithAlpha(color, color.a * s.ornamentAlpha);
            left = UIKit.Image(Root, "Ornament L", UIKit.FadeLine, ornament, mid, Vector2.zero, new Vector2(ornamentLength, 3f));
            right = UIKit.Image(Root, "Ornament R", UIKit.FadeLine, ornament, mid, Vector2.zero, new Vector2(ornamentLength, 3f));
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // fades toward the outside
        }

        public TextMeshProUGUI Label => label;

        public void Set(int zodiacSign, string text)
        {
            if (zodiacSign != sign)
            {
                sign = zodiacSign;
                glyph.Clear();
                glyph.Glyph(ZodiacGlyphs.Sign(sign), Vector2.zero, glyphSize * 0.42f, style.signGlyphStroke,
                    UIKit.WithAlpha(color, 1f));
            }
            label.text = text;
            float textWidth = label.GetPreferredValues(text).x;
            ContentWidth = glyphSize + style.signGlyphGap + textWidth;
            float x0 = -ContentWidth * 0.5f;
            glyph.rectTransform.anchoredPosition = new Vector2(x0 + glyphSize * 0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(x0 + glyphSize + style.signGlyphGap + textWidth * 0.5f, 0f);
            SetOrnamentLength(ornamentLength);
        }

        public void SetOrnamentLength(float length)
        {
            ornamentLength = length;
            left.rectTransform.sizeDelta = right.rectTransform.sizeDelta = new Vector2(length, 3f);
            float x = ContentWidth * 0.5f + style.ornamentGap + length * 0.5f;
            left.rectTransform.anchoredPosition = new Vector2(-x, 0f);
            right.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }
    }

    /// <summary>A coin balance that counts up (with a small punch) whenever coins change.</summary>
    public class CoinCounter : MonoBehaviour
    {
        UIStyle style;
        TextMeshProUGUI text;
        Transform punch;
        int shown;
        Tween tween;

        /// <summary>The glass coin pill of the HUD and the home screen (tap = <paramref name="onClick"/>).</summary>
        public static CoinCounter CreatePill(Transform parent, UIStyle s, Vector2 anchor, Vector2 pos, UnityAction onClick)
        {
            var size = new Vector2(s.coinPillWidth, s.topButton * 0.8f);
            var pill = UIKit.Card(parent, "Coins", s, anchor, pos, size);
            var image = pill.GetComponent<Image>();
            image.raycastTarget = true;
            var button = pill.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            pill.gameObject.AddComponent<PressFeedback>().style = s;
            float icon = s.iconSize;
            UIKit.Image(pill, "Icon", UIKit.Coin, s.Gold, new Vector2(0f, 0.5f), new Vector2(icon, 0f), Vector2.one * icon);
            var text = UIKit.Text(pill, "Count", UIKit.BodyFont(s), s.coinTextSize, s.text, new Vector2(0.5f, 0.5f),
                new Vector2(icon * 0.5f, 0f), new Vector2(size.x - 2f * icon, size.y), 2f, FontStyles.Bold);
            return Attach(pill.gameObject, text, pill, s);
        }

        /// <summary>Makes an existing text show the balance.</summary>
        public static CoinCounter Attach(GameObject host, TextMeshProUGUI text, Transform punchTarget, UIStyle s)
        {
            var c = host.AddComponent<CoinCounter>();
            c.style = s;
            c.text = text;
            c.punch = punchTarget;
            c.Snap();
            return c;
        }

        void OnEnable() => CoinManager.BalanceChanged += OnBalanceChanged;
        void OnDisable() => CoinManager.BalanceChanged -= OnBalanceChanged;

        /// <summary>Shows the current balance at once.</summary>
        public void Snap()
        {
            tween?.Kill();
            shown = CoinManager.GetBalance();
            if (text) text.SetText("{0}", shown);
        }

        void OnBalanceChanged(int balance, int delta)
        {
            if (!text) return;
            tween?.Kill();
            int from = shown;
            tween = Tween.Run(text, style.coinCountTime, Ease.OutCubic, k =>
            {
                int v = Mathf.RoundToInt(Mathf.Lerp(from, balance, k));
                if (v != shown) { shown = v; text.SetText("{0}", v); }
                // Punch: up quickly, settle back.
                float s = k < 0.25f ? Mathf.Lerp(1f, style.coinPunch, k / 0.25f) : Mathf.Lerp(style.coinPunch, 1f, (k - 0.25f) / 0.75f);
                if (punch) punch.localScale = Vector3.one * s;
            });
        }
    }

    /// <summary>On/off switch drawn as a small celestial dial: a crescent moon when off, a sun when on.</summary>
    public class CelestialSwitch : MonoBehaviour
    {
        UIStyle style;
        RectTransform knob;
        Image fill, outline, moon;
        VectorGraphic sun;
        float travel, shown;
        bool on;
        Tween tween;
        Action<bool> changed;

        public bool On => on;

        public static CelestialSwitch Create(Transform parent, UIStyle s, Vector2 anchor, Vector2 pos, Action<bool> changed)
        {
            var size = s.switchSize;
            var mid = new Vector2(0.5f, 0.5f);
            float corners = UIKit.PillCorners(size.y);
            var fill = UIKit.Image(parent, "Switch", UIKit.RoundFill, s.glassFill, anchor, pos, size, true);
            fill.pixelsPerUnitMultiplier = corners;
            fill.raycastTarget = true;
            var outline = UIKit.Image(fill.transform, "Outline", UIKit.RoundLine, s.outline, mid, Vector2.zero, size, true);
            outline.pixelsPerUnitMultiplier = corners;
            float knobSize = size.y - 2f * s.switchKnobInset;
            var disc = s.starStyle ? s.starStyle.backgroundCenter : new Color(0.07f, 0.09f, 0.16f);
            var knob = UIKit.Image(fill.transform, "Knob", Art.Circle, disc, mid, Vector2.zero, Vector2.one * knobSize);
            var moon = UIKit.Image(knob.transform, "Moon", UIKit.Crescent, s.moonColor, mid, Vector2.zero, Vector2.one * knobSize * 0.62f);
            moon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            var sun = UIKit.Vector(knob.transform, "Sun", mid, Vector2.zero, knobSize);
            UIKit.DrawSun(sun, knobSize * 0.36f, s.Zodiac.gold);

            var sw = fill.gameObject.AddComponent<CelestialSwitch>();
            sw.style = s;
            sw.fill = fill;
            sw.outline = outline;
            sw.knob = knob.rectTransform;
            sw.moon = moon;
            sw.sun = sun;
            sw.travel = (size.x - size.y) * 0.5f;
            sw.changed = changed;
            var button = fill.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(sw.Toggle);
            fill.gameObject.AddComponent<PressFeedback>().style = s;
            sw.Apply(0f);
            return sw;
        }

        public void Toggle()
        {
            Set(!on, true);
            changed?.Invoke(on);
        }

        public void Set(bool value, bool animate)
        {
            on = value;
            tween?.Kill();
            float from = shown, to = on ? 1f : 0f;
            if (!animate) { Apply(to); return; }
            tween = Tween.Run(this, style.switchTime, Ease.OutCubic, k => Apply(Mathf.Lerp(from, to, k)));
        }

        void Apply(float k)
        {
            shown = k;
            var gold = style.Zodiac.gold;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, k), 0f);
            knob.localRotation = Quaternion.Euler(0f, 0f, -180f * k); // rolls across
            moon.color = UIKit.WithAlpha(style.moonColor, 1f - k);
            sun.color = UIKit.WithAlpha(Color.white, k);
            fill.color = Color.Lerp(style.glassFill, UIKit.WithAlpha(gold, style.switchOnFillAlpha), k);
            outline.color = Color.Lerp(style.outline, UIKit.WithAlpha(gold, style.switchOnOutlineAlpha), k);
        }
    }
}
