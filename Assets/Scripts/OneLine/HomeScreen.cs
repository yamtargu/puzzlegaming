using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// The home screen, over the zodiac sky (ScreenRouter moves the wheel up behind the logo):
    /// the title with a small constellation that draws itself through the letters and then twinkles, the current
    /// house and level ("— ♉ TAURUS · Level 64 —"), a progress bar of stars, Play ("Continue · Level 64" or "Begin"),
    /// Map (with the current house under it), Shop (with the coin balance), the Celestial Pass badge (hidden once bought),
    /// the coin counter (top-left) and Settings (top-right).
    /// </summary>
    public class HomeScreen : MonoBehaviour
    {
        public UIStyle style;
        public LevelManager levelManager;
        public ScreenRouter router;
        public ShopUI shop;
        public SettingsPanel settings;
        public PremiumPanel premium;

        /// <summary>Fades the whole screen (ScreenRouter).</summary>
        public CanvasGroup Group { get; private set; }
        /// <summary>The safe-area root — scaled a little during screen transitions.</summary>
        public RectTransform Content { get; private set; }

        // logo
        TextMeshProUGUI title;
        ConstellationLine logoLine;
        readonly List<Image> logoStars = new();
        readonly List<Vector2> logoPoints = new();
        Image logoHead;
        bool logoDrawn;
        Tween logoTween;

        // progress
        SignLine signLine;
        readonly List<Image> barStars = new();
        TextMeshProUGUI barLabel;
        int barLit;

        // buttons
        TextMeshProUGUI playLabel;
        Image playGlow;
        readonly List<CoinCounter> coinCounters = new();
        RectTransform mapButton, premiumButton;
        TextMeshProUGUI mapHouse;

        void Awake()
        {
            if (!style) { enabled = false; return; }
            Build();
        }

        void OnEnable() => Entitlements.Changed += Refresh;
        void OnDisable() => Entitlements.Changed -= Refresh;

        /// <summary>Updates progress, the Play label and the pass badge from the save.</summary>
        public void Refresh()
        {
            if (!levelManager || !playLabel) return;
            int number = levelManager.NextUnfinishedIndex + 1;
            int sign = Zodiac.SignOf(number);
            signLine.Set(sign, $"{Zodiac.UpperNames[sign]}  ·  Level {number}");
            mapHouse.text = Zodiac.Names[sign];
            playLabel.text = SaveService.HasProgress ? $"Continue  ·  Level {number}" : "Begin";

            int total = Mathf.Max(1, levelManager.LevelCount);
            int done = Mathf.Clamp(SaveService.HighestUnlocked, 0, total);
            barLit = Mathf.Clamp(Mathf.FloorToInt(barStars.Count * done / (float)total), 0, barStars.Count);
            for (int i = 0; i < barStars.Count; i++)
            {
                barStars[i].color = i < barLit ? style.Gold : UIKit.WithAlpha(style.text, style.homeDimStarAlpha);
                barStars[i].rectTransform.localScale = Vector3.one;
            }
            barLabel.SetText("{0} / {1}", (float)done, (float)total); // float overload: no allocation
            premiumButton.gameObject.SetActive(!Entitlements.HasPass);
            foreach (var c in coinCounters) c.Snap();
        }

        /// <summary>The launch moment: the logo's constellation draws itself (once per session).</summary>
        public void PlayIntro()
        {
            if (logoDrawn || logoLine.Count < 2) return;
            logoTween?.Kill();
            logoLine.SetProgress(0f);
            foreach (var s in logoStars) s.rectTransform.localScale = Vector3.zero;
            float total = style.logoDelay + style.logoDrawTime + style.logoStarPopTime;
            logoTween = Tween.Run(logoLine, total, Ease.Linear, k =>
            {
                float t = k * total;
                float p = Ease.InOutSine(Mathf.Clamp01((t - style.logoDelay) / style.logoDrawTime));
                logoLine.SetProgress(p);
                // Wand-trail head: a bright point at the moving tip.
                logoHead.rectTransform.anchoredPosition = logoLine.Head;
                logoHead.color = UIKit.WithAlpha(style.Gold, p > 0f && p < 1f ? 1f : 0f);
                // Stars pop as the line reaches them.
                for (int i = 0; i < logoStars.Count; i++)
                {
                    float at = style.logoDelay + LineTime(logoLine.ShareAt(i)) * style.logoDrawTime;
                    float u = Mathf.Clamp01((t - at) / style.logoStarPopTime);
                    float s = u < 0.5f ? Mathf.Lerp(0f, 1.4f, Ease.OutQuad(u / 0.5f)) : Mathf.Lerp(1.4f, 1f, (u - 0.5f) / 0.5f);
                    logoStars[i].rectTransform.localScale = Vector3.one * s;
                }
            }, () => logoDrawn = true);
        }

        // Inverse of InOutSine: when (0..1 of the draw time) the line reaches a share of its length.
        static float LineTime(float share) => Mathf.Acos(1f - 2f * Mathf.Clamp01(share)) / Mathf.PI;

        // ---------- per frame (no allocations) ----------

        void Update()
        {
            if (!Group || Group.alpha <= 0f) return;
            float t = Time.time;

            // Play glow breathes.
            float b = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / style.playGlowPeriod);
            playGlow.color = UIKit.WithAlpha(style.Gold, 0.1f + 0.16f * b);
            playGlow.rectTransform.localScale = Vector3.one * (1f + 0.05f * b);

            // Logo stars twinkle once drawn.
            if (logoDrawn)
                for (int i = 0; i < logoStars.Count; i++)
                {
                    float w = Mathf.Sin(t * style.logoTwinkleSpeed + i * 1.7f);
                    logoStars[i].rectTransform.localScale = Vector3.one * (0.9f + 0.18f * w);
                    logoStars[i].color = UIKit.WithAlpha(style.Gold, 0.75f + 0.25f * w);
                }

            // The next star of the progress bar twinkles.
            if (barLit < barStars.Count)
            {
                float w = 0.5f + 0.5f * Mathf.Sin(t * 3f);
                var star = barStars[barLit];
                star.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, w);
                star.color = Color.Lerp(UIKit.WithAlpha(style.text, style.homeDimStarAlpha), style.Gold, w);
            }
        }

        // ---------- building ----------

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Home Canvas", 12, true);
            Group = canvas.gameObject.AddComponent<CanvasGroup>();
            Content = UIKit.Stretch(canvas.transform, "Safe Area");
            Content.gameObject.AddComponent<SafeArea>();
            var safe = Content;
            float m = style.edgeMargin;

            // Top-left coins (tap = shop), top-right settings (an astrolabe dial).
            coinCounters.Add(CoinCounter.CreatePill(safe, style, new Vector2(0f, 1f),
                new Vector2(m + style.coinPillWidth * 0.5f, -(m + style.topButton * 0.5f)), OpenShop));
            var gear = UIKit.GlassButton(safe, "Settings", style, null, new Vector2(1f, 1f),
                new Vector2(-(m + style.topButton * 0.5f), -(m + style.topButton * 0.5f)), style.topButton,
                () => { if (settings) settings.Open(); });
            UIKit.DrawAstrolabe(UIKit.Vector(gear.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, style.iconSize),
                style.iconSize * 0.5f, UIKit.WithAlpha(style.text, 0.9f));

            BuildLogo(safe);
            BuildProgress(safe);
            BuildButtons(safe);
        }

        void BuildLogo(RectTransform safe)
        {
            var mid = new Vector2(0.5f, 0.5f);
            var block = UIKit.Rect(safe, "Logo", new Vector2(0.5f, style.homeLogoY), Vector2.zero,
                new Vector2(UIKit.ReferenceResolution.x, style.logoSize * 2.5f));
            var gold = style.Gold;

            title = UIKit.Text(block, "Title", UIKit.TitleFont(style), style.logoSize, style.text, mid,
                new Vector2(0f, style.logoSize * 0.3f), new Vector2(UIKit.ReferenceResolution.x, style.logoSize * 1.3f), style.logoSpacing);
            title.text = style.gameTitle;
            UIKit.Glow(title, UIKit.WithAlpha(gold, style.titleGlow), 0.3f);

            // Constellation through the letters (positions from the laid-out glyphs).
            var line = UIKit.Stretch(title.transform, "Constellation");
            line.gameObject.AddComponent<CanvasRenderer>();
            logoLine = line.gameObject.AddComponent<ConstellationLine>();
            logoLine.raycastTarget = false;
            logoLine.color = UIKit.WithAlpha(gold, 0.85f);
            logoLine.glowColor = UIKit.WithAlpha(Color.white, 0.25f);
            logoLine.lineWidth = style.logoLineWidth;
            logoLine.glowWidth = style.logoGlowWidth;
            title.ForceMeshUpdate();
            var info = title.textInfo;
            foreach (var p in style.logoConstellation)
            {
                int i = (int)p.x;
                if (i < 0 || i >= info.characterCount || !info.characterInfo[i].isVisible) continue;
                var c = info.characterInfo[i];
                logoPoints.Add(new Vector2(Mathf.Lerp(c.bottomLeft.x, c.topRight.x, p.y), Mathf.Lerp(c.bottomLeft.y, c.topRight.y, p.z)));
            }
            logoLine.SetPoints(logoPoints);
            for (int i = 0; i < logoPoints.Count; i++)
                logoStars.Add(UIKit.Image(line, "Star", i % 2 == 0 ? UIKit.Sparkle : Art.Star, gold, mid, logoPoints[i],
                    Vector2.one * style.logoStarSize * (i % 2 == 0 ? 1f : 0.6f)));
            logoHead = UIKit.Image(line, "Head", Art.SoftCircle, Color.clear, mid, Vector2.zero, Vector2.one * style.logoGlowWidth * 2.5f);

            // "— PATH —"
            var subtitle = UIKit.Text(block, "Subtitle", UIKit.TitleFont(style), style.logoSubtitleSize, UIKit.WithAlpha(gold, 0.9f), mid,
                new Vector2(0f, style.logoSize * 0.3f - style.logoSubtitleGap), new Vector2(UIKit.ReferenceResolution.x, style.logoSubtitleSize * 1.5f),
                style.logoSubtitleSpacing);
            subtitle.text = style.gameSubtitle;
            float half = subtitle.GetPreferredValues(style.gameSubtitle).x * 0.5f + style.ornamentGap + style.ornamentLength * 0.5f;
            var y = subtitle.rectTransform.anchoredPosition.y;
            var ornament = UIKit.WithAlpha(gold, style.ornamentAlpha);
            UIKit.Image(block, "Ornament L", UIKit.FadeLine, ornament, mid, new Vector2(-half, y), new Vector2(style.ornamentLength, 3f));
            UIKit.Image(block, "Ornament R", UIKit.FadeLine, ornament, mid, new Vector2(half, y), new Vector2(style.ornamentLength, 3f))
                .rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        void BuildProgress(RectTransform safe)
        {
            var anchor = new Vector2(0.5f, style.homeProgressY);
            signLine = new SignLine(safe, style, anchor, Vector2.zero, style.homeSignSize, style.tierLineSpacing * 0.6f,
                style.homeSignSize * 1.2f, UIKit.WithAlpha(style.Gold, 0.9f));

            // A thin progress bar made of stars.
            int count = Mathf.Max(2, style.progressBarStars);
            float step = style.progressBarWidth / (count - 1);
            for (int i = 0; i < count; i++)
                barStars.Add(UIKit.Image(safe, "Bar Star", Art.Star, Color.white, anchor,
                    new Vector2(-style.progressBarWidth * 0.5f + i * step, style.progressBarY), Vector2.one * style.progressBarStarSize));
            barLabel = UIKit.Text(safe, "Bar Label", UIKit.BodyFont(style), style.progressLabelSize,
                UIKit.WithAlpha(style.text, style.labelAlpha), anchor,
                new Vector2(0f, style.progressBarY - style.progressBarStarSize * 1.8f), new Vector2(style.progressBarWidth, 40f), 4f);
        }

        void BuildButtons(RectTransform safe)
        {
            var gold = style.Gold;
            var mid = new Vector2(0.5f, 0.5f);

            // Play: gold, large, breathing glow.
            var playAnchor = new Vector2(0.5f, style.homePlayY);
            playGlow = UIKit.Image(safe, "Play Glow", Art.SoftCircle, Color.clear, playAnchor, Vector2.zero, style.playButtonSize * 1.4f);
            var play = Pill(safe, "Play", playAnchor, Vector2.zero, style.playButtonSize, () => { if (router) router.Play(); },
                out playLabel, gold);
            play.GetComponent<Image>().color = UIKit.WithAlpha(gold, style.switchOnFillAlpha);
            playLabel.font = UIKit.TitleFont(style);
            playLabel.fontSize = style.playTextSize;
            playLabel.color = gold;

            // Map (the current house under the label) and Shop (the balance under it).
            var rowAnchor = new Vector2(0.5f, style.homeSecondaryY);
            float dx = (style.secondaryButtonSize.x + style.secondaryGap) * 0.5f;
            var map = Pill(safe, "Map", rowAnchor, new Vector2(-dx, 0f), style.secondaryButtonSize,
                () => { if (router) router.ShowMap(); }, out var mapLabel, null);
            mapButton = (RectTransform)map.transform;
            SecondaryContent(mapButton, mapLabel, "Map");
            UIKit.DrawChart(IconSlot(mapButton), style.homeIconSize * 0.45f, UIKit.WithAlpha(style.text, 0.9f));
            mapHouse = UIKit.Text(mapButton, "House", UIKit.BodyFont(style), style.secondaryTextSize * 0.62f, gold,
                new Vector2(0.5f, 0f), new Vector2(style.homeIconSize * 0.35f, style.secondaryButtonSize.y * 0.25f),
                new Vector2(style.secondaryButtonSize.x * 0.6f, 40f), 2f, FontStyles.Bold);

            var shopButton = Pill(safe, "Shop", rowAnchor, new Vector2(dx, 0f), style.secondaryButtonSize, OpenShop, out var shopLabel, null);
            SecondaryContent((RectTransform)shopButton.transform, shopLabel, "Shop");
            var coin = IconSlot((RectTransform)shopButton.transform);
            UIKit.Image(coin.transform, "Coin", UIKit.Coin, gold, mid, Vector2.zero, Vector2.one * style.homeIconSize * 0.9f);
            var balance = UIKit.Text(shopButton.transform, "Balance", UIKit.BodyFont(style), style.secondaryTextSize * 0.7f, gold,
                new Vector2(0.5f, 0f), new Vector2(style.homeIconSize * 0.35f, style.secondaryButtonSize.y * 0.25f),
                new Vector2(style.secondaryButtonSize.x * 0.6f, 40f), 2f, FontStyles.Bold);
            coinCounters.Add(CoinCounter.Attach(shopButton.gameObject, balance, balance.transform, style));

            // Celestial Pass badge.
            var pass = Pill(safe, "Celestial Pass", new Vector2(0.5f, style.homePremiumY), Vector2.zero, style.premiumButtonSize,
                () => { if (premium) premium.Open(); }, out var passLabel, UIKit.WithAlpha(gold, 0.6f));
            premiumButton = (RectTransform)pass.transform;
            passLabel.text = "No Ads";
            passLabel.fontSize = style.secondaryTextSize * 0.85f;
            passLabel.color = gold;
            passLabel.rectTransform.anchoredPosition = new Vector2(style.homeIconSize * 0.4f, 0f);
            var sun = UIKit.Vector(premiumButton, "Sun", new Vector2(0f, 0.5f), new Vector2(style.premiumButtonSize.y * 0.6f, 0f),
                style.homeIconSize);
            UIKit.DrawSun(sun, style.homeIconSize * 0.42f, gold);
        }

        void OpenShop()
        {
            if (shop) shop.Open();
        }

        // Rounded glass pill (UIKit.PillButton) with pill-round ends.
        Button Pill(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick,
            out TextMeshProUGUI label, Color? outline)
        {
            var button = UIKit.PillButton(parent, name, style, "", anchor, pos, size, onClick ?? (() => { }), out label, outline);
            float corners = UIKit.PillCorners(size.y);
            button.GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            button.transform.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            return button;
        }

        // Icon on the left of a secondary button, label next to it.
        void SecondaryContent(RectTransform button, TextMeshProUGUI label, string text)
        {
            label.text = text;
            label.font = UIKit.TitleFont(style);
            label.fontSize = style.secondaryTextSize;
            label.rectTransform.anchoredPosition = new Vector2(style.homeIconSize * 0.35f, style.secondaryButtonSize.y * 0.08f);
        }

        VectorGraphic IconSlot(RectTransform button) =>
            UIKit.Vector(button, "Icon", new Vector2(0f, 0.5f), new Vector2(style.secondaryButtonSize.y * 0.55f, 0f), style.homeIconSize);
    }
}
