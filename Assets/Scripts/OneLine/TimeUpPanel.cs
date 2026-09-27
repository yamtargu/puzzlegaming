using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Shown when a timed level runs out of time (after the hourglass cracks), in the celestial frame with Saturn's
    /// glyph in the medallion, worded as a second chance: "Saturn can grant you more time". The gold button uses a
    /// Saturn's Gift (+15 s) — or, with none owned, buys one for coins and uses it at once (the missing amount shows if
    /// the balance is short); at most 2 per level (TimerSettings). "Try again" restarts the level with a full timer.
    /// </summary>
    public class TimeUpPanel : MonoBehaviour
    {
        public UIStyle style;
        public LevelTimer timer;

        CanvasGroup group;
        RectTransform card;
        Button gift;
        CanvasGroup giftGroup;
        TextMeshProUGUI giftLabel, ownedText, messageText, secondChance;
        RectTransform sparkle;
        Tween showDelay;
        bool subscribed;

        TimerSettings S => timer ? timer.settings : null;
        public bool IsOpen => group && group.gameObject.activeSelf;

        void Awake()
        {
            if (!style || !timer || !timer.settings) { Debug.LogError("TimeUpPanel: assign a UIStyle and a LevelTimer.", this); enabled = false; return; }
            Build();
        }

        void OnEnable()
        {
            if (subscribed || !timer) return;
            subscribed = true;
            timer.TimeUp += OnTimeUp;
            timer.Configured += OnConfigured;
            timer.Extended += OnExtended;
            Boosts.Changed += OnBoostsChanged;
            CoinManager.BalanceChanged += OnBalanceChanged;
        }

        void OnDisable()
        {
            if (!subscribed) return;
            subscribed = false;
            timer.TimeUp -= OnTimeUp;
            timer.Configured -= OnConfigured;
            timer.Extended -= OnExtended;
            Boosts.Changed -= OnBoostsChanged;
            CoinManager.BalanceChanged -= OnBalanceChanged;
        }

        void OnTimeUp()
        {
            // Let the crack and the red flash play first.
            showDelay?.Kill();
            showDelay = Tween.Run(this, S.crackTime + 0.25f, Ease.Linear, null, () =>
            {
                if (timer.State == TimerState.TimeUp && timer.levelManager && !timer.levelManager.pathManager.suspended) Show();
            });
        }

        void OnConfigured(bool timed) => Hide(true);
        void OnExtended(float seconds) => Hide(false);
        void OnBoostsChanged(BoostType type, int count) { if (IsOpen) Refresh(); }
        void OnBalanceChanged(int balance, int delta) { if (IsOpen) Refresh(); }

        public void Show()
        {
            messageText.text = "";
            Refresh();
            UIKit.Open(group, card, style);
            // The sparkle sits right after the line (measured once the panel is active).
            float w = secondChance.GetPreferredValues(secondChance.text).x;
            sparkle.anchoredPosition = new Vector2(secondChance.rectTransform.anchoredPosition.x + w * 0.5f + 26f,
                secondChance.rectTransform.anchoredPosition.y + 4f);
        }

        /// <summary>Closes (at once when leaving the level).</summary>
        public void Hide(bool instant)
        {
            showDelay?.Kill();
            if (!IsOpen) return;
            if (instant) group.gameObject.SetActive(false);
            else UIKit.Close(group, card, style);
        }

        void Refresh()
        {
            int owned = Boosts.Count(BoostType.SaturnsGift);
            int seconds = Mathf.RoundToInt(S.saturnSeconds);
            bool capped = timer.SaturnUsed >= S.maxSaturnPerLevel;
            if (capped) giftLabel.text = "Saturn's gifts are spent";
            else if (owned > 0) giftLabel.SetText("Use Saturn's Gift (+{0}s)", seconds);
            else giftLabel.SetText("Buy Saturn's Gift  ·  {0}", Boosts.Price(BoostType.SaturnsGift, 1));
            gift.interactable = !capped;
            giftGroup.alpha = capped ? S.boostDisabledAlpha : 1f;
            int left = Mathf.Max(0, S.maxSaturnPerLevel - timer.SaturnUsed);
            ownedText.SetText("You have {0}  ·  {1} left on this level", (float)owned, left);
        }

        void UseGift()
        {
            if (timer.SaturnUsed >= S.maxSaturnPerLevel) { UIKit.Wiggle(gift.transform, style); return; }
            if (Boosts.Count(BoostType.SaturnsGift) <= 0 && !Boosts.TryBuy(BoostType.SaturnsGift, 1))
            {
                messageText.SetText("{0} more coins needed", Boosts.Missing(BoostType.SaturnsGift, 1));
                UIKit.Wiggle(gift.transform, style);
                return;
            }
            if (!timer.TryExtend()) UIKit.Wiggle(gift.transform, style); // closes itself through Extended
        }

        void TryAgain()
        {
            Hide(false);
            timer.Restart();
        }

        void Build()
        {
            var s = S;
            var canvas = UIKit.MakeCanvas(transform, "Time Up Canvas", 17, true);
            (group, card) = UIKit.Modal(canvas.transform, "Time Up", style, s.timeUpSize, null);
            var frame = card.GetComponent<Cartouche>();
            frame.SetGlyph(ZodiacGlyphs.Planet(6), style.Zodiac.glyphStroke);
            var top = new Vector2(0.5f, 1f);
            var body = UIKit.BodyFont(style);

            UIKit.PanelTitle(card, style, "Time's up", style.text);
            float y = style.panelTitleY + 84f;
            secondChance = UIKit.Text(card, "Second Chance", body, 34f, UIKit.WithAlpha(style.text, 0.85f), top, new Vector2(-18f, -y),
                new Vector2(s.timeUpSize.x - 120f, 50f));
            secondChance.text = "Saturn can grant you more time";
            // A drawn sparkle instead of an emoji.
            sparkle = UIKit.Image(card, "Sparkle", UIKit.Sparkle, style.Gold, top, new Vector2(0f, -y + 4f), Vector2.one * 30f).rectTransform;

            var size = s.panelButtonSize;
            float by = y + 130f;
            gift = PanelKit.Button(card, style, "Saturn's Gift", "", top, new Vector2(0f, -by), size, true, UseGift, out giftLabel);
            giftGroup = gift.gameObject.AddComponent<CanvasGroup>();
            var glyph = UIKit.Vector(gift.transform, "Saturn", new Vector2(0f, 0.5f), new Vector2(56f, 0f), 44f);
            glyph.Glyph(ZodiacGlyphs.Planet(6), Vector2.zero, 16f, style.Zodiac.glyphStroke, style.Gold);
            giftLabel.rectTransform.anchoredPosition = new Vector2(24f, 0f);
            giftLabel.rectTransform.sizeDelta = new Vector2(size.x - 120f, size.y);

            ownedText = UIKit.Text(card, "Owned", body, 26f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(0f, -(by + size.y * 0.5f + 34f)), new Vector2(700f, 40f), 4f, FontStyles.SmallCaps);
            PanelKit.Button(card, style, "Try Again", "Try again", top, new Vector2(0f, -(by + size.y + 90f)), size, false, TryAgain, out _);
            messageText = UIKit.Text(card, "Message", body, 28f, s.sandRed, new Vector2(0.5f, 0f), new Vector2(0f, 96f),
                new Vector2(700f, 44f), 3f);
        }
    }
}
