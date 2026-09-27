using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Night-sky HUD, built in code from UIStyle (+ its ZodiacTheme):
    /// top-left settings and map (on timed levels: one astrolabe menu button that opens the pause panel; the hourglass
    /// sits bottom-left, above restart), top-right star-coin counter (opens the shop; on timed levels the two time boosts stack under it),
    /// top-center title cartouche — a celestial
    /// double-line frame with the sign's glyph in a medallion, "LEVEL 12", the level's name and "— ♈ ARIES · 42% —",
    /// plus the Hard/Boss badge under it — bottom-left restart, bottom-right hint, and a bottom row that ProgressDots
    /// fills. Keeps the board clear of it by reporting top/bottom reserves to PathManager, inside the safe area.
    /// On timed levels it also hides the board while the timer is paused (or time is up), so pausing can't be used to
    /// think, and shows the one-time "The stars wait for no one" hint on the first timed level.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public LevelManager levelManager;
        public UIStyle style;
        public ShopUI shop;
        public SettingsPanel settings;
        public HintSystem hints;
        [Tooltip("Plays when a new zodiac house begins; the title waits for it.")]
        public HouseTransition transition;
        [Tooltip("The map button (top-left, next to settings) pauses the level and opens the map.")]
        public ScreenRouter router;
        [Tooltip("Level-complete animation: the title celebrates when its zodiac sign rises.")]
        public CompletionSequence completion;
        [Tooltip("Timed levels: the hourglass and the boosts follow it. Optional.")]
        public LevelTimer timer;
        [Tooltip("Opened by the menu button of timed levels.")]
        public PausePanel pause;

        /// <summary>The time boosts under the coin counter (null without a LevelTimer).</summary>
        public BoostBar BoostButtons { get; private set; }
        public HourglassView Hourglass { get; private set; }

        /// <summary>Bottom-center strip between the buttons; ProgressDots builds in here.</summary>
        public RectTransform BottomRow { get; private set; }
        /// <summary>Fades the whole HUD (ScreenRouter).</summary>
        public CanvasGroup Group { get; private set; }
        /// <summary>The safe-area root — scaled a little during screen transitions.</summary>
        public RectTransform Content => safeRect;

        Canvas canvas;
        SafeArea safeArea;
        RectTransform safeRect;
        float reportedTop = -1f, reportedBottom = -1f;
        ZodiacTheme theme;

        // title cartouche
        RectTransform titleBlock;
        Cartouche cartouche;
        CanvasGroup titleGroup;
        TextMeshProUGUI levelLabel, nameText;
        SignLine tierLine;
        readonly Image[] sparkles = new Image[2];
        Color accent = Color.white;
        int glyphSign = -1;
        Tween entranceTween;

        // timed levels
        GameObject settingsButton, mapButton, menuButton;
        bool timedLayout, veiled;
        TextMeshProUGUI introHint;
        Tween introTween;
        TimerSettings TimerStyle => timer ? timer.settings : null;

        // badge
        RectTransform badge;
        CanvasGroup badgeGroup;
        Image badgeFill, badgeLine, badgeIcon, badgeGlow;
        TextMeshProUGUI badgeText;
        readonly Image[] orbiters = new Image[3];
        bool bossBadge;

        PathManager Path => levelManager ? levelManager.pathManager : null;

        void Awake()
        {
            if (!style) { Debug.LogError("HUD: assign a UIStyle.", this); enabled = false; return; }
            theme = style.Zodiac;
            Build();
        }

        void OnEnable()
        {
            if (!style) return;
            if (levelManager) levelManager.LevelLoaded += OnLevelLoaded;
            if (completion) completion.Arrived += OnCompleted;
            else if (Path) Path.Completed += OnCompleted;
            if (timer) timer.Configured += OnTimerConfigured;
        }

        void OnDisable()
        {
            if (levelManager) levelManager.LevelLoaded -= OnLevelLoaded;
            if (completion) completion.Arrived -= OnCompleted;
            else if (Path) Path.Completed -= OnCompleted;
            if (timer) timer.Configured -= OnTimerConfigured;
        }


        // ---------- per frame (no allocations) ----------

        void LateUpdate()
        {
            if (!theme) return; // not built (e.g. scripts reloaded during Play mode)
            ReportReserves();
            if (bossBadge) AnimateBoss();
            UpdateVeil();
        }

        /// <summary>Title block height below the safe-area top: buttons, cartouche, badge (reference units).</summary>
        float TopBlock
        {
            get
            {
                float block = BadgeBottom + 18f;
                if (!timedLayout || !TimerStyle) return block;
                float boosts = BoostButtons ? BoostButtons.Bottom + 18f : 0f;
                return Mathf.Max(block, boosts);
            }
        }
        /// <summary>Bottom block height above the safe-area bottom: the button band, or on timed levels the hourglass column.</summary>
        float BottomBlock
        {
            get
            {
                if (!timedLayout || !TimerStyle) return style.bottomBand;
                // Top of the glass, or of its flip if that swings higher.
                float column = HourglassBottom + Mathf.Max(TimerStyle.hourglassSize.y, TimerStyle.hourglassSize.y * 0.5f + Hourglass.FlipReach);
                return Mathf.Max(style.bottomBand, column + 18f);
            }
        }
        float FrameTop => style.edgeMargin + style.topButton + theme.frameTopBelowButtons;
        /// <summary>Bottom of the HARD / BOSS badge below the safe-area top (reference units).</summary>
        float BadgeBottom => FrameTop + theme.frameHeight + theme.badgeBelowFrame + style.badgeHeight * 0.5f;
        /// <summary>Bottom of the hourglass rect above the safe-area bottom: over Restart, clear of it with the label and the flip.</summary>
        float HourglassBottom
        {
            get
            {
                var t = TimerStyle;
                float below = Mathf.Max(Hourglass.LabelDrop, Hourglass.FlipReach - t.hourglassSize.y * 0.5f);
                return style.edgeMargin + style.roundButton + t.hourglassAboveRestart + below;
            }
        }
        /// <summary>Left edge of the hourglass rect: the HUD margin, or more if the flip would swing past the safe area.</summary>
        float HourglassLeft => Mathf.Max(style.edgeMargin, Hourglass.FlipReach - TimerStyle.hourglassSize.x * 0.5f);

        // Screen space the HUD occupies (safe-area insets + bands), as fractions of the screen height.
        void ReportReserves()
        {
            if (!Path || Screen.height == 0) return;
            float scale = canvas.scaleFactor;
            var safe = safeArea.Current;
            float top = (Screen.height - safe.yMax + Mathf.Max(style.topBand, TopBlock) * scale) / Screen.height;
            float bottom = (safe.yMin + BottomBlock * scale) / Screen.height;
            if (Mathf.Abs(top - reportedTop) > 0.001f) { reportedTop = top; Path.SetTopReserve(top); }
            if (Mathf.Abs(bottom - reportedBottom) > 0.001f) { reportedBottom = bottom; Path.SetBottomReserve(bottom); }
        }

        void AnimateBoss()
        {
            float t = Time.time;
            float pulse = 0.5f - 0.5f * Mathf.Cos(t / style.badgePulsePeriod * 2f * Mathf.PI);
            var gold = Gold;
            gold.a = 0.18f + 0.22f * pulse;
            badgeGlow.color = gold;
            float w = badge.sizeDelta.x * 0.5f + 14f, h = badge.sizeDelta.y * 0.5f + 12f;
            for (int i = 0; i < orbiters.Length; i++)
            {
                float a = (t * style.orbitSpeed + i * 120f) * Mathf.Deg2Rad;
                orbiters[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * w, Mathf.Sin(a) * h);
                float twinkle = 0.5f + 0.5f * Mathf.Sin(t * 2.3f + i * 2.1f);
                orbiters[i].rectTransform.localScale = Vector3.one * (0.6f + 0.5f * twinkle);
                var c = gold;
                c.a = 0.4f + 0.5f * twinkle;
                orbiters[i].color = c;
            }
        }

        Color Gold => style.Gold;

        // ---------- events ----------

        void OnLevelLoaded(int _)
        {
            int number = levelManager.CurrentNumber;
            int sign = levelManager.CurrentSign;
            accent = theme.Accent(Zodiac.ElementOf(sign));

            levelLabel.SetText("Level {0}", number);
            nameText.text = style.LevelTitle(number);
            nameText.color = style.text;
            UIKit.Glow(nameText, UIKit.WithAlpha(accent, style.titleGlow), 0.3f);
            tierLine.Set(sign, $"{Zodiac.UpperNames[sign]}  ·  {levelManager.CurrentPercent}%");
            SetSignGlyph(sign);
            if (Hourglass) Hourglass.SetSign(sign);
            LayoutTitle();
            RefreshBadge();

            // A new house: the transition plays first, then the title draws itself.
            HideTitle();
            if (!(transition && transition.OnLevel(number, PlayTitleEntrance))) PlayTitleEntrance();
        }

        void OnCompleted()
        {
            // The title turns gold under a sweep of light, the frame glows and the medallion's dial turns one sign (30°).
            var from = nameText.color;
            var gold = Gold;
            var shine = Color.Lerp(gold, Color.white, 0.6f);
            float time = Mathf.Max(style.winTitleGoldTime, style.titleShimmerTime);
            float goldShare = style.winTitleGoldTime / time;
            Tween.Run(nameText, time, Ease.Linear, k =>
            {
                float c = Ease.OutCubic(Mathf.Clamp01(k / goldShare));
                nameText.color = Color.Lerp(from, gold, c);
                UIKit.SetGlowColor(nameText, UIKit.WithAlpha(gold, Mathf.Lerp(style.titleGlow, 0.7f, c)));
                UIKit.Shimmer(nameText, Mathf.Lerp(-0.3f, 1.3f, k), shine, style.shimmerWidth);
            }, () => nameText.ForceMeshUpdate()); // the sweep leaves no trace
            cartouche.Celebrate();
        }

        // ---------- timed levels ----------

        // Timed: hourglass + one menu button on the left, boosts under the coins. Untimed: today's two buttons.
        void OnTimerConfigured(bool timed)
        {
            timedLayout = timed && Hourglass;
            if (settingsButton) settingsButton.SetActive(!timedLayout);
            if (mapButton) mapButton.SetActive(!timedLayout);
            if (menuButton) menuButton.SetActive(timedLayout);
            if (nameText && !string.IsNullOrEmpty(nameText.text)) LayoutTitle();
            reportedTop = -1f; // re-report the reserve
            if (timedLayout && timer.ShowIntro) ShowIntroHint();
            else HideIntroHint();
        }

        // Hide the board while a started timer is paused (panels, menu) or time is up, on the game screen only.
        void UpdateVeil()
        {
            if (!timer || !router || !router.board) return;
            bool hide = timer.IsTimed && router.Current == AppScreen.Game &&
                        (timer.State == TimerState.TimeUp || timer.IsPaused && timer.State == TimerState.Running);
            if (hide == veiled) return;
            veiled = hide;
            if (hide) router.board.Show(false, false);
            else if (router.Current == AppScreen.Game) router.board.Show(true, false); // leaving the screen hides it anyway
        }

        void ShowIntroHint()
        {
            introTween?.Kill();
            var t = TimerStyle;
            introHint.text = t.introHint;
            introHint.rectTransform.anchoredPosition = new Vector2(0f, -(TopBlock + 24f));
            introHint.gameObject.SetActive(true);
            float delay = theme.drawTime + style.titleInTime, total = delay + t.introHintTime;
            introTween = Tween.Run(introHint, total, Ease.Linear, k =>
            {
                float time = k * total - delay;
                float a = time < 0f ? 0f : time < 0.5f ? time / 0.5f : Mathf.Clamp01((t.introHintTime - time) / 0.6f);
                introHint.alpha = a;
            }, HideIntroHint);
        }

        void HideIntroHint()
        {
            introTween?.Kill();
            if (introHint) introHint.gameObject.SetActive(false);
        }

        // ---------- title cartouche ----------

        void SetSignGlyph(int sign)
        {
            if (sign == glyphSign) return;
            glyphSign = sign;
            cartouche.SetGlyph(ZodiacGlyphs.Sign(sign), theme.glyphStroke);
        }

        // Frame width follows the text (TMP preferred width) with padding, clamped to the safe area.
        void LayoutTitle()
        {
            float pad = theme.framePadding, gap = style.ornamentGap;
            nameText.fontSize = style.titleSize;
            nameText.characterSpacing = style.titleSpacing;
            float nameW = nameText.GetPreferredValues(nameText.text).x;
            float tierW = tierLine.ContentWidth;

            float side = style.edgeMargin + theme.sideMotifLength;
            if (timedLayout && TimerStyle) // clear of the boost column on the right (kept symmetric)
                side = Mathf.Max(side, style.edgeMargin + TimerStyle.boostButtonSize + TimerStyle.hourglassGap + theme.sideMotifLength);
            float maxW = safeRect.rect.width - 2f * side;
            float want = Mathf.Max(nameW, tierW + 2f * (gap + style.ornamentLength)) + 2f * pad;
            float width = Mathf.Clamp(want, Mathf.Min(theme.minFrameWidth, maxW), maxW);
            if (nameW > width - 2f * pad) nameText.fontSize = style.titleSize * (width - 2f * pad) / nameW;
            titleBlock.sizeDelta = new Vector2(width, theme.frameHeight);

            // Ornaments fill the rest of the tier row.
            tierLine.SetOrnamentLength(Mathf.Clamp((width - 2f * pad - tierW) * 0.5f - gap, 0f, style.ornamentLength));
            cartouche.LayoutCorners();
        }

        void HideTitle()
        {
            entranceTween?.Kill();
            cartouche.Hide();
            titleGroup.alpha = 0f;
            badgeGroup.alpha = 0f;
            foreach (var s in sparkles) s.gameObject.SetActive(false);
        }

        // Medallion appears, the lines draw from it outward and around, the corner stars twinkle in one by one,
        // then the text fades in (rising, letter spacing tightening) and a letter sparkles.
        void PlayTitleEntrance()
        {
            HideTitle();
            float draw = theme.drawTime, textIn = style.titleInTime;
            cartouche.PlayDraw(draw);
            float total = draw + textIn;
            entranceTween = Tween.Run(titleBlock, total, Ease.Linear, k =>
            {
                float t = k * total;
                float x = Ease.OutCubic(Mathf.Clamp01((t - draw) / textIn));
                titleGroup.alpha = x;
                badgeGroup.alpha = x;
                ((RectTransform)titleGroup.transform).anchoredPosition = new Vector2(0f, -style.titleRise * (1f - x));
                nameText.characterSpacing = Mathf.Lerp(style.titleSpacingStart, style.titleSpacing, x);
            }, Twinkle);
        }

        // A subtle sparkle on one or two letters of the name.
        void Twinkle()
        {
            nameText.ForceMeshUpdate();
            var info = nameText.textInfo;
            if (info.characterCount == 0) return;
            for (int s = 0; s < sparkles.Length; s++)
            {
                int i = Random.Range(0, info.characterCount);
                if (!info.characterInfo[i].isVisible) continue;
                var sparkle = sparkles[s];
                sparkle.rectTransform.anchoredPosition = info.characterInfo[i].topRight;
                sparkle.gameObject.SetActive(true);
                float delay = s * 0.35f;
                Tween.Run(sparkle, 0.9f + delay, Ease.Linear, k =>
                {
                    float t = Mathf.Clamp01((k * (0.9f + delay) - delay) / 0.9f);
                    float a = Mathf.Sin(t * Mathf.PI);
                    sparkle.rectTransform.localScale = Vector3.one * a;
                    sparkle.rectTransform.localRotation = Quaternion.Euler(0, 0, t * 90f);
                    sparkle.color = UIKit.WithAlpha(style.text, a);
                }, () => sparkle.gameObject.SetActive(false));
            }
        }

        void RefreshBadge()
        {
            bool hard = levelManager.IsHard;
            bossBadge = levelManager.IsBoss;
            badge.gameObject.SetActive(hard);
            if (!hard) return;
            var c = bossBadge ? Gold : accent;
            badgeText.text = bossBadge ? "BOSS" : "HARD";
            badgeText.color = c;
            badgeIcon.color = c;
            badgeLine.color = c;
            badgeFill.color = UIKit.WithAlpha(c, style.badgeFillAlpha);
            badgeGlow.gameObject.SetActive(bossBadge);
            foreach (var o in orbiters) o.gameObject.SetActive(bossBadge);
            float width = badgeText.GetPreferredValues(badgeText.text).x + style.badgeHeight + 36f;
            badge.sizeDelta = new Vector2(width, style.badgeHeight);
            badgeIcon.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + style.badgeHeight * 0.55f, 0f);
            badgeText.rectTransform.anchoredPosition = new Vector2(style.badgeHeight * 0.3f, 0f);
            badgeGlow.rectTransform.sizeDelta = badge.sizeDelta + new Vector2(70f, 60f);
        }

        // ---------- building ----------

        void Build()
        {
            canvas = UIKit.MakeCanvas(transform, "HUD Canvas", 10, true);
            Group = canvas.gameObject.AddComponent<CanvasGroup>();
            safeRect = UIKit.Stretch(canvas.transform, "Safe Area");
            safeArea = safeRect.gameObject.AddComponent<SafeArea>();
            var safe = safeRect;
            float m = style.edgeMargin;
            var topLeft = new Vector2(0, 1);
            var topRight = new Vector2(1, 1);

            // Top-left: settings (an astrolabe dial).
            var settingsGlass = UIKit.GlassButton(safe, "Settings", style, null, topLeft,
                new Vector2(m + style.topButton * 0.5f, -(m + style.topButton * 0.5f)), style.topButton,
                () => { if (settings) settings.Open(); });
            UIKit.DrawAstrolabe(UIKit.Vector(settingsGlass.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, style.iconSize),
                style.iconSize * 0.5f, UIKit.WithAlpha(style.text, 0.9f));
            settingsButton = settingsGlass.gameObject;

            // Next to it: the map (a star chart) — pauses the level and goes back to the map.
            var mapGlass = UIKit.GlassButton(safe, "Map", style, null, topLeft,
                new Vector2(m + style.topButton * 1.5f + style.hudButtonGap, -(m + style.topButton * 0.5f)), style.topButton,
                () => { if (router) router.ShowMap(); });
            UIKit.DrawChart(UIKit.Vector(mapGlass.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, style.iconSize),
                style.iconSize * 0.45f, UIKit.WithAlpha(style.text, 0.9f));
            mapButton = mapGlass.gameObject;

            // Top-right: star-coin counter (tap -> shop).
            CoinCounter.CreatePill(safe, style, topRight,
                new Vector2(-(m + style.coinPillWidth * 0.5f), -(m + style.topButton * 0.5f)), () => { if (shop) shop.Open(); });

            BuildTitle(safe);
            BuildTimed(safe);

            // Bottom: restart (left), hint (right), progress row (center).
            var bottomLeft = new Vector2(0, 0);
            var bottomRight = new Vector2(1, 0);
            float r = style.roundButton * 0.5f;
            UIKit.GlassButton(safe, "Restart", style, UIKit.Restart, bottomLeft, new Vector2(m + r, m + r), style.roundButton,
                () => { if (Path) Path.ResetPath(); });
            UIKit.GlassButton(safe, "Hint", style, UIKit.Sparkle, bottomRight, new Vector2(-(m + r), m + r), style.roundButton,
                () => { if (hints) hints.Show(); });
            BottomRow = UIKit.Rect(safe, "Bottom Row", new Vector2(0.5f, 0f), new Vector2(0f, m + r),
                new Vector2(1080f - 2f * (m + style.roundButton) - 40f, style.roundButton));
        }

        // Timed levels: the menu button (top-left corner), the hourglass bottom-left above Restart, the boosts under the coin
        // counter, the hint. Anchored to the safe area's corners, so it holds on any aspect ratio.
        void BuildTimed(RectTransform safe)
        {
            var t = TimerStyle;
            if (!t) return;
            float m = style.edgeMargin;
            var topLeft = new Vector2(0f, 1f);
            var feedback = Path ? Path.feedback : null;
            var bottomLeft = new Vector2(0f, 0f);
            Hourglass = HourglassView.Create(safe, bottomLeft, Vector2.zero, style, t, timer, feedback, safe);
            var glassRect = (RectTransform)Hourglass.transform;
            glassRect.pivot = bottomLeft; // stands on its bottom edge; the body inside still flips about its own center
            glassRect.anchoredPosition = new Vector2(HourglassLeft, HourglassBottom);

            float size = t.menuButtonSize;
            var menu = UIKit.GlassButton(safe, "Menu", style, null, topLeft,
                new Vector2(m + size * 0.5f, -(m + size * 0.5f)), size,
                () => { if (pause) pause.Open(); });
            float icon = style.iconSize * size / style.topButton;
            UIKit.DrawAstrolabe(UIKit.Vector(menu.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, icon),
                icon * 0.5f, UIKit.WithAlpha(style.text, 0.9f));
            menuButton = menu.gameObject;
            menuButton.SetActive(false);

            BoostButtons = BoostBar.Create(safe, transform, style, t, timer, feedback, Hourglass,
                router ? router.sky : null, router ? router.parallax : null, m + style.topButton * 0.9f);

            introHint = UIKit.Text(safe, "Timer Hint", UIKit.TitleFont(style), 34f, UIKit.WithAlpha(style.Gold, 0.95f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 50f), 6f);
            UIKit.Glow(introHint, UIKit.WithAlpha(style.Gold, 0.4f), 0.3f);
            introHint.gameObject.SetActive(false);
        }

        // The cartouche: its rect is the frame (pivot at the top line); the medallion sits on the top line.
        void BuildTitle(RectTransform safe)
        {
            var top = new Vector2(0.5f, 1f);
            var mid = new Vector2(0.5f, 0.5f);
            var gold = theme.gold;

            titleBlock = UIKit.Rect(safe, "Title", top, new Vector2(0f, -FrameTop), new Vector2(theme.minFrameWidth, theme.frameHeight));
            titleBlock.pivot = top;
            var chart = style.starStyle ? style.starStyle.backgroundEdge : new Color(0.04f, 0.05f, 0.09f);
            cartouche = Cartouche.Create(titleBlock, style, new Cartouche.Options
            {
                medallionRadius = theme.medallionRadius,
                crescent = true,
                sideMotif = true,
                fill = UIKit.WithAlpha(chart, theme.titleFillAlpha),
            });

            // Text inside the frame (fades in after the lines).
            var text = UIKit.Stretch(titleBlock, "Text");
            titleGroup = text.gameObject.AddComponent<CanvasGroup>();
            var font = UIKit.TitleFont(style);
            levelLabel = UIKit.Text(text, "Level", font, style.levelLabelSize,
                UIKit.WithAlpha(style.text, style.labelAlpha), top, new Vector2(0f, -theme.labelY), new Vector2(600f, 44f),
                style.levelLabelSpacing, FontStyles.SmallCaps);
            nameText = UIKit.Text(text, "Name", font, style.titleSize, style.text, top,
                new Vector2(0f, -theme.nameY), new Vector2(900f, 100f), style.titleSpacing);
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = UIKit.Image(nameText.transform, "Sparkle", UIKit.Sparkle, style.text, mid, Vector2.zero,
                    new Vector2(26f, 26f));
                sparkles[i].gameObject.SetActive(false);
            }
            tierLine = new SignLine(text, style, top, new Vector2(0f, -theme.tierY), style.tierLineSize, style.tierLineSpacing * 0.6f,
                theme.tierGlyphSize, UIKit.WithAlpha(gold, 0.9f));

            BuildBadge(titleBlock);
        }

        void BuildBadge(Transform parent)
        {
            badge = UIKit.Rect(parent, "Badge", new Vector2(0.5f, 0f), new Vector2(0f, -theme.badgeBelowFrame),
                new Vector2(200f, style.badgeHeight));
            badgeGroup = badge.gameObject.AddComponent<CanvasGroup>(); // appears with the title text
            badgeGlow = UIKit.Image(badge, "Glow", Art.SoftCircle, Color.clear, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            badgeFill = UIKit.Image(badge, "Fill", UIKit.RoundFill, Color.clear, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            badgeLine = UIKit.Image(badge, "Outline", UIKit.RoundLine, Color.clear, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            foreach (var img in new[] { badgeFill, badgeLine })
            {
                img.rectTransform.anchorMin = Vector2.zero;
                img.rectTransform.anchorMax = Vector2.one;
                img.rectTransform.sizeDelta = Vector2.zero;
                img.pixelsPerUnitMultiplier = 1.2f; // pill-round ends at this height
            }
            badgeIcon = UIKit.Image(badge, "Star", Art.Star, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(style.badgeHeight * 0.5f, style.badgeHeight * 0.5f));
            badgeText = UIKit.Text(badge, "Text", UIKit.BodyFont(style), style.badgeTextSize, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, style.badgeHeight), 10f, FontStyles.Bold);
            for (int i = 0; i < orbiters.Length; i++)
                orbiters[i] = UIKit.Image(badge, "Orbiter", UIKit.Sparkle, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(14f, 14f));
            badge.gameObject.SetActive(false);
        }
    }
}
