using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Short "house transition" when the player enters a new zodiac sign: an astrolabe ring turns the new sign to the
    /// top, its big glyph and "ENTERING TAURUS · The Bull" fade in at the center, then everything fades out (~2 s).
    /// A tap skips it. The HUD starts it (so the title waits for it); ZodiacBackground turns the big wheel.
    /// </summary>
    public class HouseTransition : MonoBehaviour
    {
        public UIStyle style;

        public bool Playing { get; private set; }

        ZodiacTheme theme;
        CanvasGroup group;
        RectTransform center;
        VectorGraphic ring, glyph;
        Image glow;
        TextMeshProUGUI title, subtitle;
        Image ornamentL, ornamentR;
        Tween tween;
        Action done;
        int lastSign = -1;

        void Awake()
        {
            if (!style) { enabled = false; return; }
            theme = style.Zodiac;
            Build();
        }

        /// <summary>
        /// Called for every loaded level. Plays when the level is the first of a sign the player wasn't in before;
        /// returns whether it plays. <paramref name="onDone"/> runs when it ends (only if it plays).
        /// </summary>
        public bool OnLevel(int levelNumber, Action onDone)
        {
            if (!group) return false;
            int sign = Zodiac.SignOf(levelNumber);
            bool play = sign != lastSign && Zodiac.IsFirstOfSign(levelNumber);
            lastSign = sign;
            if (!play) return false;
            Play(sign, onDone);
            return true;
        }

        public void Play(int sign, Action onDone)
        {
            Finish(false);
            done = onDone;
            Playing = true;
            Fill(sign);
            group.gameObject.SetActive(true);
            group.blocksRaycasts = true;

            float tIn = theme.transitionIn, hold = theme.transitionHold, tOut = theme.transitionOut;
            float total = tIn + hold + tOut;
            tween = Tween.Run(group, total, Ease.Linear, k =>
            {
                float t = k * total;
                float a = t < tIn ? Ease.OutCubic(t / tIn) : t < tIn + hold ? 1f : 1f - Ease.InOutSine((t - tIn - hold) / tOut);
                group.alpha = a;
                float grow = t < tIn ? Mathf.Lerp(0.92f, 1f, Ease.OutCubic(t / tIn)) : 1f + 0.04f * Mathf.Clamp01((t - tIn - hold) / tOut);
                center.localScale = Vector3.one * grow;
                // The ring turns the new sign up to the top.
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f * (1f - Ease.OutCubic(Mathf.Clamp01(t / (tIn + hold * 0.7f)))));
            }, () => Finish(true));
        }

        /// <summary>Ends a transition at once without continuing to the title (leaving the game screen).</summary>
        public void Stop() => Finish(false);

        void Skip()
        {
            if (!Playing) return;
            tween?.Kill();
            float from = group.alpha;
            group.blocksRaycasts = false;
            tween = Tween.Run(group, theme.transitionSkipOut, Ease.OutCubic, k => group.alpha = from * (1f - k), () => Finish(true));
        }

        void Finish(bool notify)
        {
            tween?.Kill();
            tween = null;
            if (group) group.gameObject.SetActive(false);
            bool was = Playing;
            Playing = false;
            var callback = done;
            done = null;
            if (notify && was) callback?.Invoke();
        }

        // Ring with the 12 glyphs (the new sign at the top, brightest), big glyph, texts.
        void Fill(int sign)
        {
            var gold = theme.gold;
            float R = theme.transitionGlyphSize;
            ring.Clear();
            ring.Circle(Vector2.zero, R * 0.95f, 1.5f, UIKit.WithAlpha(gold, 0.8f), 160);
            ring.Circle(Vector2.zero, R * 0.75f, 0.75f, UIKit.WithAlpha(gold, 0.6f), 140);
            for (int i = 0; i < Zodiac.SignCount; i++)
            {
                float angle = 90f + 30f * (i - sign);
                var d = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                float divider = angle + 15f;
                var e = new Vector2(Mathf.Cos(divider * Mathf.Deg2Rad), Mathf.Sin(divider * Mathf.Deg2Rad));
                ring.Line(e * R * 0.75f, e * R * 0.95f, 0.75f, UIKit.WithAlpha(gold, 0.5f));
                ring.Glyph(ZodiacGlyphs.Sign(i), d * R * 0.85f, R * 0.055f, 1.4f,
                    UIKit.WithAlpha(gold, i == sign ? 1f : 0.45f), angle - 90f);
            }
            glyph.Clear();
            glyph.Glyph(ZodiacGlyphs.Sign(sign), Vector2.zero, R * 0.42f, 4f, gold);
            glow.color = UIKit.WithAlpha(theme.Accent(Zodiac.ElementOf(sign)), 0.16f);

            title.text = "Entering " + Zodiac.Names[sign];
            subtitle.text = Zodiac.Epithets[sign];
            float half = subtitle.GetPreferredValues(subtitle.text).x * 0.5f + 22f + style.ornamentLength * 0.5f;
            ornamentL.rectTransform.anchoredPosition = new Vector2(-half, subtitle.rectTransform.anchoredPosition.y);
            ornamentR.rectTransform.anchoredPosition = new Vector2(half, subtitle.rectTransform.anchoredPosition.y);
        }

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "House Transition Canvas", 25, true);
            var overlay = UIKit.Stretch(canvas.transform, "House Transition");
            var catcher = overlay.gameObject.AddComponent<Image>(); // full-screen: dims a little and catches the skip tap
            var edge = style.starStyle ? style.starStyle.backgroundEdge : Color.black;
            catcher.color = UIKit.WithAlpha(edge, theme.transitionDim * 0.75f);
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(Skip);
            group = overlay.gameObject.AddComponent<CanvasGroup>();

            var mid = new Vector2(0.5f, 0.5f);
            UIKit.Image(overlay, "Vignette", Art.SoftCircle, UIKit.WithAlpha(edge, theme.transitionDim), mid, new Vector2(0f, 20f),
                new Vector2(1500f, 1500f));
            center = UIKit.Rect(overlay, "Center", mid, new Vector2(0f, 60f), new Vector2(600f, 600f));
            float R = theme.transitionGlyphSize;
            glow = UIKit.Image(center, "Glow", Art.SoftCircle, Color.clear, mid, Vector2.zero, new Vector2(R * 2.4f, R * 2.4f));
            ring = Vector(center, "Ring", R * 2.2f);
            glyph = Vector(center, "Glyph", R * 1.2f);

            // Soft dark band so the board's stars don't read through the lettering.
            UIKit.Image(center, "Text Shade", Art.SoftCircle, UIKit.WithAlpha(edge, theme.transitionDim), mid,
                new Vector2(0f, -R - 100f), new Vector2(1100f, 330f));
            var font = UIKit.TitleFont(style);
            title = UIKit.Text(center, "Title", font, 50f, UIKit.WithAlpha(theme.gold, 1f), mid, new Vector2(0f, -R - 70f),
                new Vector2(1000f, 70f), 12f);
            subtitle = UIKit.Text(center, "Subtitle", font, 34f, UIKit.WithAlpha(style.text, 0.75f), mid, new Vector2(0f, -R - 130f),
                new Vector2(800f, 50f), 6f);
            ornamentL = UIKit.Image(center, "Ornament L", UIKit.FadeLine, UIKit.WithAlpha(theme.gold, 0.6f), mid, Vector2.zero,
                new Vector2(style.ornamentLength, 3f));
            ornamentR = UIKit.Image(center, "Ornament R", UIKit.FadeLine, UIKit.WithAlpha(theme.gold, 0.6f), mid, Vector2.zero,
                new Vector2(style.ornamentLength, 3f));
            ornamentR.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            overlay.gameObject.SetActive(false);
        }

        static VectorGraphic Vector(Transform parent, string name, float size)
        {
            var rt = UIKit.Rect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<VectorGraphic>();
            g.raycastTarget = false;
            return g;
        }
    }
}
