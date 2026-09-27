using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// The two time boosts of timed levels, stacked under the HUD coin counter: round glass buttons with a gold
    /// medallion dial (like the title frame), a planet glyph (♄ Saturn's Gift, ☽ Lunar Stillness — vector strokes, never
    /// emoji) and a crisp count badge. Usable boosts stand out — a bright gold ring, a light glyph, a soft glow behind and a
    /// very slow breath (paused with the timer); unusable ones (none left, or not allowed now) keep the faded look.
    /// Disabled while paused, after time up, during the completion and past the per-level cap.
    /// With none left the button shakes and opens a small offer card (packs of 1 and 5 for coins; shows what's missing).
    /// Effects: Saturn's Gift spirals a ring of gold light into the hourglass with a deep chime; Lunar Stillness lays a
    /// faint moonlight vignette and a cool tint over the sky, slows the sky's twinkle/drift and runs a thin countdown
    /// ring around its button. Uses LevelTimer only through TryExtend / TrySlow; the timer does the rules.
    /// </summary>
    public class BoostBar : MonoBehaviour
    {
        sealed class Slot
        {
            public BoostType type;
            public RectTransform rt;
            public CanvasGroup group;
            public VectorGraphic glyph, dial;
            public Image badge, countdown, glow;
            public float prominence, phase;
            public TextMeshProUGUI count;
            public int shownCount = -1;
        }

        TimerSettings s;
        UIStyle style;
        LevelTimer timer;
        GameFeedback feedback;
        HourglassView hourglass;
        ZodiacBackground sky;
        ParallaxBackground parallax;
        RectTransform flyLayer;
        bool subscribed;

        readonly Slot[] slots = new Slot[2];
        Image spiralRing;
        readonly Image[] spiralSparks = new Image[3];
        Image vignette, tint;
        float moon, breathTime;
        Tween moonTween, spiralTween;

        // offer card
        CanvasGroup offerGroup;
        RectTransform offerCard;
        Cartouche offerFrame;
        TextMeshProUGUI offerTitle, offerText, offerOwned, offerBalance, offerMessage;
        readonly TextMeshProUGUI[] offerPrices = new TextMeshProUGUI[2];
        BoostType offerType;

        public bool OfferOpen => offerGroup && offerGroup.gameObject.activeSelf;
        /// <summary>Bottom of the column below the top-right corner (reference units), for the HUD's top reserve.</summary>
        public float Bottom { get; private set; }

        public static BoostBar Create(RectTransform safe, Transform canvasHost, UIStyle style, TimerSettings settings, LevelTimer timer,
            GameFeedback feedback, HourglassView hourglass, ZodiacBackground sky, ParallaxBackground parallax, float top)
        {
            var rt = UIKit.Rect(safe, "Boosts", new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var bar = rt.gameObject.AddComponent<BoostBar>();
            bar.s = settings;
            bar.style = style;
            bar.timer = timer;
            bar.feedback = feedback;
            bar.hourglass = hourglass;
            bar.sky = sky;
            bar.parallax = parallax;
            bar.flyLayer = safe;
            bar.Build(rt, canvasHost, top);
            bar.Subscribe();
            rt.gameObject.SetActive(false);
            return bar;
        }

        void OnEnable() => Subscribe();
        void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            if (subscribed || !timer) return;
            subscribed = true;
            timer.Configured += OnConfigured;
            timer.Extended += OnExtended;
            timer.SlowStarted += OnSlowStarted;
            timer.SlowEnded += OnSlowEnded;
            Boosts.Changed += OnBoostsChanged;
            CoinManager.BalanceChanged += OnBalanceChanged;
        }

        void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            if (timer)
            {
                timer.Configured -= OnConfigured;
                timer.Extended -= OnExtended;
                timer.SlowStarted -= OnSlowStarted;
                timer.SlowEnded -= OnSlowEnded;
            }
            Boosts.Changed -= OnBoostsChanged;
            CoinManager.BalanceChanged -= OnBalanceChanged;
            SetMoon(0f);
        }

        // ---------- events ----------

        void OnConfigured(bool timed)
        {
            gameObject.SetActive(timed);
            if (OfferOpen && !timed) offerGroup.gameObject.SetActive(false);
            if (!timer.IsSlowed) FadeMoon(0f);
            RefreshCounts();
        }

        void OnBoostsChanged(BoostType type, int count)
        {
            RefreshCounts();
            if (OfferOpen) RefreshOffer();
        }

        void OnBalanceChanged(int balance, int delta)
        {
            if (OfferOpen) RefreshOffer();
        }

        void OnExtended(float seconds)
        {
            if (feedback) feedback.PlaySaturn();
            if (gameObject.activeInHierarchy) Spiral();
        }

        void OnSlowStarted()
        {
            if (feedback) feedback.PlayLunar();
            FadeMoon(1f);
        }

        void OnSlowEnded() => FadeMoon(0f);

        // ---------- taps ----------

        void OnTap(Slot slot)
        {
            if (!timer) return;
            bool allowed = slot.type == BoostType.SaturnsGift ? timer.CanExtend && timer.State != TimerState.TimeUp : timer.CanSlow;
            if (!allowed)
            {
                UIKit.Wiggle(slot.rt, style);
                return;
            }
            if (Boosts.Count(slot.type) <= 0)
            {
                // None left: a gentle shake, then the offer card.
                UIKit.Wiggle(slot.rt, style);
                OpenOffer(slot.type);
                return;
            }
            bool used = slot.type == BoostType.SaturnsGift ? timer.TryExtend() : timer.TrySlow();
            if (!used) UIKit.Wiggle(slot.rt, style);
        }

        // ---------- per frame (no allocations) ----------

        void Update()
        {
            if (!timer) return;
            float dt = Time.unscaledDeltaTime;
            if (!timer.IsPaused) breathTime += dt; // the breath rests while the game is paused
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                bool saturn = slot.type == BoostType.SaturnsGift;
                bool active = !saturn && timer.IsSlowed;
                bool allowed = saturn ? timer.CanExtend && timer.State != TimerState.TimeUp : timer.CanSlow;
                float alpha = active || allowed ? (slot.shownCount > 0 || active ? 1f : 0.75f) : s.boostDisabledAlpha;
                slot.group.alpha = Mathf.MoveTowards(slot.group.alpha, alpha, dt * 4f);

                // Prominent only when it can be used right now (or Lunar Stillness is running).
                float target = active || allowed && slot.shownCount > 0 ? 1f : 0f;
                slot.prominence = Mathf.MoveTowards(slot.prominence, target, dt * 3f);
                float k = slot.prominence;
                float breath = 0.5f - 0.5f * Mathf.Cos((breathTime / Mathf.Max(0.5f, s.boostBreathPeriod) + slot.phase) * 2f * Mathf.PI);
                // Breathe the ring and glyph (the button itself keeps its press animation).
                var breathe = Vector3.one * (1f + s.boostBreathScale * breath * k);
                slot.dial.rectTransform.localScale = breathe;
                slot.glyph.rectTransform.localScale = breathe;
                slot.glow.color = UIKit.WithAlpha(slot.glow.color, s.boostGlowAlpha * k * (0.7f + 0.3f * breath));
                slot.dial.color = UIKit.WithAlpha(Color.white, Mathf.Lerp(0.55f, 1f, k));
                slot.glyph.color = Color.Lerp(new Color(0.8f, 0.8f, 0.8f, 0.55f), Color.white, k);
                if (slot.countdown)
                {
                    slot.countdown.gameObject.SetActive(active);
                    if (active) slot.countdown.fillAmount = timer.SlowLeft01;
                }
            }
        }

        void RefreshCounts()
        {
            foreach (var slot in slots)
            {
                int n = Boosts.Count(slot.type);
                if (n == slot.shownCount) continue;
                slot.shownCount = n;
                if (n > 0) slot.count.SetText("{0}", n);
                else slot.count.SetText("+");
                slot.badge.color = n > 0 ? Color.Lerp(style.Gold, Color.white, 0.15f) : UIKit.WithAlpha(style.text, 0.35f);
            }
        }

        // ---------- effects ----------

        // A ring of gold light spirals from Saturn's button into the hourglass, a few sparks trailing it.
        void Spiral()
        {
            if (!hourglass || !hourglass.isActiveAndEnabled) return;
            Vector2 from = flyLayer.InverseTransformPoint(slots[0].rt.position);
            Vector2 to = flyLayer.InverseTransformPoint(hourglass.Body.position);
            spiralTween?.Kill();
            spiralRing.gameObject.SetActive(true);
            foreach (var sp in spiralSparks) sp.gameObject.SetActive(true);
            var gold = style.Gold;
            float radius = Vector2.Distance(from, to) * 0.18f;
            spiralTween = Tween.Run(spiralRing, s.spiralTime, Ease.Linear, k =>
            {
                for (int i = -1; i < spiralSparks.Length; i++)
                {
                    float lag = (i + 1) * 0.06f;
                    float e = Ease.InOutSine(Mathf.Clamp01((k - lag) / (1f - lag)));
                    float angle = e * 3f * Mathf.PI;
                    var p = Vector2.Lerp(from, to, e) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (1f - e);
                    float fade = Mathf.Sin(Mathf.Clamp01(e * 1.1f) * Mathf.PI * 0.5f + (e > 0.85f ? (e - 0.85f) * 10f : 0f));
                    if (i < 0)
                    {
                        spiralRing.rectTransform.anchoredPosition = p;
                        spiralRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.45f, e);
                        spiralRing.color = UIKit.WithAlpha(gold, 0.9f * (1f - Mathf.Pow(e, 6f)));
                    }
                    else
                    {
                        spiralSparks[i].rectTransform.anchoredPosition = p;
                        spiralSparks[i].color = UIKit.WithAlpha(gold, 0.8f * fade * (1f - e));
                    }
                }
            }, () =>
            {
                spiralRing.gameObject.SetActive(false);
                foreach (var sp in spiralSparks) sp.gameObject.SetActive(false);
            });
        }

        void FadeMoon(float to)
        {
            moonTween?.Kill();
            float from = moon;
            if (Mathf.Approximately(from, to)) { SetMoon(to); return; }
            moonTween = Tween.Run(vignette, s.moonFadeTime, Ease.InOutSine, k => SetMoon(Mathf.Lerp(from, to, k)));
        }

        // Moonlight over the sky; the sky's twinkle and drift slow down with it.
        void SetMoon(float k)
        {
            moon = k;
            if (vignette)
            {
                vignette.gameObject.SetActive(k > 0.001f);
                tint.gameObject.SetActive(k > 0.001f);
                vignette.color = UIKit.WithAlpha(s.moonVignette, s.moonVignetteAlpha * k);
                tint.color = UIKit.WithAlpha(s.moonTint, s.moonTintAlpha * k);
            }
            float scale = Mathf.Lerp(1f, s ? s.skySlowdown : 1f, k);
            if (sky) sky.timeScale = scale;
            if (parallax) parallax.timeScale = scale;
        }

        // ---------- offer card ----------

        void OpenOffer(BoostType type)
        {
            offerType = type;
            offerFrame.SetGlyph(ZodiacGlyphs.Planet(type == BoostType.SaturnsGift ? 6 : 1), style.Zodiac.glyphStroke);
            offerTitle.text = Boosts.DisplayName(type);
            offerText.text = type == BoostType.SaturnsGift
                ? $"Saturn, ruler of time, adds {Mathf.RoundToInt(s.saturnSeconds)} seconds to the hourglass"
                : $"The moon stills the sand: {Mathf.RoundToInt(s.lunarSpeed * 100f)}% speed for {Mathf.RoundToInt(s.lunarSeconds)} seconds";
            offerMessage.text = "";
            RefreshOffer();
            UIKit.Open(offerGroup, offerCard, style);
        }

        public void CloseOffer()
        {
            if (OfferOpen) UIKit.Close(offerGroup, offerCard, style);
        }

        void RefreshOffer()
        {
            offerOwned.SetText("You have {0}", Boosts.Count(offerType));
            offerBalance.SetText("{0} coins", CoinManager.GetBalance());
            offerPrices[0].SetText("Buy 1  ·  {0}", Boosts.Price(offerType, 1));
            offerPrices[1].SetText("Buy {0}  ·  {1}", (float)s.packSize, Boosts.Price(offerType, s.packSize));
        }

        void Buy(int amount)
        {
            if (Boosts.TryBuy(offerType, amount))
            {
                UIKit.Burst(offerCard, new Vector2(0f, 120f), style.Gold, style);
                offerMessage.text = "";
                CloseOffer();
                return;
            }
            offerMessage.SetText("{0} more coins needed", Boosts.Missing(offerType, amount));
        }

        // ---------- building ----------

        void Build(RectTransform root, Transform canvasHost, float top)
        {
            float size = s.boostButtonSize, m = style.edgeMargin;
            float y = -top - s.boostTopGap - size * 0.5f;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = BuildSlot(root, i == 0 ? BoostType.SaturnsGift : BoostType.LunarStillness,
                    new Vector2(-(m + size * 0.5f), y));
                y -= size + s.boostGap;
            }
            Bottom = -(y + size * 0.5f + s.boostGap) + s.boostBadgeSize * 0.25f;

            var mid = new Vector2(0.5f, 0.5f);
            spiralRing = UIKit.Image(flyLayer, "Saturn Ring", UIKit.ThinRing, Color.clear, mid, Vector2.zero, Vector2.one * size * 0.9f);
            spiralRing.gameObject.SetActive(false);
            for (int i = 0; i < spiralSparks.Length; i++)
            {
                spiralSparks[i] = UIKit.Image(flyLayer, "Saturn Spark", UIKit.Sparkle, Color.clear, mid, Vector2.zero, Vector2.one * 20f);
                spiralSparks[i].gameObject.SetActive(false);
            }

            // Moonlight: its own canvas under the HUD, over the sky and board.
            var moonCanvas = UIKit.MakeCanvas(canvasHost, "Moonlight Canvas", 9, false);
            tint = UIKit.Image(moonCanvas.transform, "Cool Tint", Art.Square, Color.clear, mid, Vector2.zero, Vector2.zero);
            Fill(tint.rectTransform);
            vignette = UIKit.Image(moonCanvas.transform, "Vignette", Vignette, Color.clear, mid, Vector2.zero, Vector2.zero);
            Fill(vignette.rectTransform);
            SetMoon(0f);

            BuildOffer(canvasHost);
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
        }

        Slot BuildSlot(RectTransform root, BoostType type, Vector2 pos)
        {
            float size = s.boostButtonSize;
            var mid = new Vector2(0.5f, 0.5f);
            var slot = new Slot { type = type, phase = type == BoostType.SaturnsGift ? 0f : 0.5f };
            bool saturn = type == BoostType.SaturnsGift;
            var tone = saturn ? style.Gold : s.sandSilver;
            // Soft glow behind the button (active state only; animated in Update).
            slot.glow = UIKit.Image(root, "Glow", Art.SoftCircle, UIKit.WithAlpha(tone, 0f), new Vector2(1f, 1f), pos,
                Vector2.one * size * 1.75f);
            var button = UIKit.GlassButton(root, type.ToString(), style, null, new Vector2(1f, 1f), pos, size, () => OnTap(slot));
            slot.rt = (RectTransform)button.transform;
            slot.rt.anchorMin = slot.rt.anchorMax = new Vector2(1f, 1f);
            slot.rt.anchoredPosition = pos;
            slot.group = button.gameObject.AddComponent<CanvasGroup>();

            var gold = style.Zodiac.gold;
            // Gold medallion dial, like the title frame's — full-strength gold with a second fine ring for contrast
            // (dimmed through its color when the boost can't be used).
            var bright = Color.Lerp(gold, style.Gold, 0.6f);
            slot.dial = UIKit.Vector(slot.rt, "Dial", mid, Vector2.zero, size + 16f);
            slot.dial.Dial(size * 0.5f - 4f, style.Zodiac.dialWidth * 1.7f, bright, 5f);
            slot.dial.Circle(Vector2.zero, size * 0.5f - 13f, 1f, UIKit.WithAlpha(bright, 0.45f), 72);
            slot.glyph = UIKit.Vector(slot.rt, "Glyph", mid, Vector2.zero, size);
            // Lighter icon for contrast against the navy glass.
            slot.glyph.Glyph(ZodiacGlyphs.Planet(saturn ? 6 : 1), Vector2.zero, s.boostGlyphSize * 0.5f, style.Zodiac.glyphStroke * 1.25f,
                saturn ? Color.Lerp(style.Gold, Color.white, 0.35f) : Color.Lerp(s.sandSilver, Color.white, 0.4f));

            if (type == BoostType.LunarStillness)
            {
                slot.countdown = UIKit.Image(slot.rt, "Countdown", UIKit.ThinRing, s.sandSilver, mid, Vector2.zero, Vector2.one * (size + 22f));
                slot.countdown.type = Image.Type.Filled;
                slot.countdown.fillMethod = Image.FillMethod.Radial360;
                slot.countdown.fillOrigin = (int)Image.Origin360.Top;
                slot.countdown.fillClockwise = false;
                slot.countdown.gameObject.SetActive(false);
            }

            float b = s.boostBadgeSize;
            var badgePos = new Vector2(size * 0.36f, -size * 0.36f);
            // A dark rim around the gold disc keeps the badge's edge crisp over the ring and the glow.
            UIKit.Image(slot.rt, "Badge Rim", Art.Circle, DarkText, mid, badgePos, Vector2.one * (b + 6f));
            slot.badge = UIKit.Image(slot.rt, "Badge", Art.Circle, style.Gold, mid, badgePos, Vector2.one * b);
            slot.count = UIKit.Text(slot.badge.transform, "Count", UIKit.BodyFont(style), b * 0.66f, DarkText, mid, new Vector2(0f, 1f),
                Vector2.one * b, 0f, FontStyles.Bold);
            return slot;
        }

        Color DarkText => style.starStyle ? style.starStyle.backgroundEdge : new Color(0.04f, 0.05f, 0.09f);

        void BuildOffer(Transform canvasHost)
        {
            var canvas = UIKit.MakeCanvas(canvasHost, "Boost Offer Canvas", 21, true);
            (offerGroup, offerCard) = UIKit.Modal(canvas.transform, "Boost Offer", style, s.offerSize, CloseOffer);
            offerFrame = offerCard.GetComponent<Cartouche>();
            UIKit.CloseButton(offerCard, style, CloseOffer);
            var top = new Vector2(0.5f, 1f);
            var body = UIKit.BodyFont(style);
            offerTitle = UIKit.PanelTitle(offerCard, style, "", style.text);
            float y = style.panelTitleY + 80f;
            offerText = UIKit.Text(offerCard, "Text", body, 30f, UIKit.WithAlpha(style.text, 0.8f), top, new Vector2(0f, -y),
                new Vector2(s.offerSize.x - 140f, 90f));
            offerText.textWrappingMode = TextWrappingModes.Normal;
            offerOwned = UIKit.Text(offerCard, "Owned", body, 28f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(0f, -(y + 86f)), new Vector2(600f, 44f), 6f, FontStyles.SmallCaps);
            float by = y + 190f;
            var size = s.panelButtonSize;
            for (int i = 0; i < 2; i++)
            {
                int amount = i == 0 ? 1 : s.packSize;
                var button = UIKit.PillButton(offerCard, i == 0 ? "Buy One" : "Buy Pack", style, "", top,
                    new Vector2(0f, -(by + i * (size.y + s.panelButtonGap))), size, () => Buy(amount), out offerPrices[i],
                    i == 1 ? style.Gold : (Color?)null);
                PanelKit.Pill(button, size.y);
                if (i == 1)
                {
                    button.GetComponent<Image>().color = UIKit.WithAlpha(style.Gold, style.switchOnFillAlpha);
                    offerPrices[i].color = style.Gold;
                }
                UIKit.Image(button.transform, "Coin", UIKit.Coin, style.Gold, new Vector2(1f, 0.5f), new Vector2(-50f, 0f),
                    Vector2.one * style.iconSize * 0.8f);
            }
            offerBalance = UIKit.Text(offerCard, "Balance", body, 30f, style.Gold, new Vector2(0.5f, 0f), new Vector2(0f, 150f),
                new Vector2(600f, 44f), 3f, FontStyles.Bold);
            offerMessage = UIKit.Text(offerCard, "Message", body, 28f, s.sandRed, new Vector2(0.5f, 0f), new Vector2(0f, 100f),
                new Vector2(600f, 44f), 3f);
        }

        static Sprite vignetteSprite;

        // Clear center, soft opaque rim; stretched over the screen it becomes an ellipse.
        static Sprite Vignette
        {
            get
            {
                if (vignetteSprite) return vignetteSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, p.magnitude));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(px);
                tex.Apply();
                return vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }

    /// <summary>Small helpers shared by the timer panels.</summary>
    public static class PanelKit
    {
        /// <summary>Pill-round ends on a PillButton of this height.</summary>
        public static void Pill(Button button, float height)
        {
            float corners = UIKit.PillCorners(height);
            button.GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            button.transform.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = corners;
        }

        /// <summary>A gold (primary) or glass pill button with a title-font label.</summary>
        public static Button Button(Transform parent, UIStyle s, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size,
            bool gold, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI text)
        {
            var b = UIKit.PillButton(parent, name, s, label, anchor, pos, size, onClick, out text, gold ? s.Gold : (Color?)null);
            Pill(b, size.y);
            text.font = UIKit.TitleFont(s);
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = 38f;
            text.rectTransform.sizeDelta = new Vector2(size.x - 48f, size.y);
            if (gold)
            {
                b.GetComponent<Image>().color = UIKit.WithAlpha(s.Gold, s.switchOnFillAlpha);
                text.color = s.Gold;
            }
            return b;
        }
    }
}
