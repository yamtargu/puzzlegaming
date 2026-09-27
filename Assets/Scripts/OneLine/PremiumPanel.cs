using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// "Celestial Pass": the one-time No Ads purchase. Lists what it includes, buys through
    /// <see cref="PurchaseService.Current"/> (a mock store for now) and, on success, grants it (Entitlements), plays a
    /// gold burst and closes. Nothing it grants affects gameplay.
    /// </summary>
    public class PremiumPanel : MonoBehaviour
    {
        public UIStyle style;
        public AppConfig config;
        public CosmeticCatalog catalog;

        /// <summary>Raised after the pass has been bought.</summary>
        public event Action Purchased;

        CanvasGroup group;
        RectTransform card, buyButton;
        TextMeshProUGUI buyLabel, message;
        bool busy;

        public bool IsOpen => group && group.gameObject.activeSelf;

        void Awake()
        {
            if (!style || !config) { enabled = false; return; }
            if (PurchaseService.Current is MockPurchaseService mock) mock.delay = config.mockStoreDelay;
            Build();
        }

        public void Open()
        {
            if (!enabled) return;
            message.text = "";
            RefreshButton();
            UIKit.Open(group, card, style);
        }

        public void Close()
        {
            if (busy) return; // wait for the store's answer
            UIKit.Close(group, card, style);
        }

        void RefreshButton() => buyLabel.text = Entitlements.HasPass ? "Unlocked" : $"Unlock  ·  {config.passPriceLabel}";

        void Buy()
        {
            if (busy) return;
            if (Entitlements.HasPass) { UIKit.Wiggle(buyButton, style); return; }
            busy = true;
            buyLabel.text = "Opening the store…";
            PurchaseService.Current.Purchase(config.passProductId, OnPurchase);
        }

        void OnPurchase(PurchaseResult result)
        {
            busy = false;
            if (!this) return;
            if (result != PurchaseResult.Success)
            {
                message.text = result == PurchaseResult.Cancelled ? "" : "The store didn't answer — please try again";
                RefreshButton();
                return;
            }
            Entitlements.GrantPass(config, catalog);
            RefreshButton();
            message.text = "The sky is yours — thank you!";
            UIKit.Burst(card, buyButton.anchoredPosition + card.rect.height * 0.5f * Vector2.down, style.Gold, style);
            Purchased?.Invoke();
            Tween.Run(this, style.burstTime, Ease.Linear, null, Close);
        }

        // ---------- building ----------

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Premium Canvas", 22, true);
            (group, card) = UIKit.Modal(canvas.transform, "Celestial Pass", style, style.premiumSize, Close);
            var frame = card.GetComponent<Cartouche>();
            UIKit.DrawSun(frame.Glyph, frame.MedallionRadius * 0.7f, style.Zodiac.gold);
            UIKit.PanelTitle(card, style, config.passTitle, style.Gold);
            UIKit.CloseButton(card, style, Close);
            var top = new Vector2(0.5f, 1f);

            float y = style.panelTitleY + style.panelTitleSize;
            var subtitle = UIKit.Text(card, "Subtitle", UIKit.BodyFont(style), style.levelLabelSize,
                UIKit.WithAlpha(style.text, style.labelAlpha), top, new Vector2(0f, -y), new Vector2(style.settingsRowWidth, 44f),
                style.levelLabelSpacing * 0.5f, FontStyles.SmallCaps);
            subtitle.text = config.passSubtitle;

            // What's included: one line each, with a small gold sparkle.
            float width = style.settingsRowWidth;
            y += style.perkSpacing * 1.2f;
            foreach (var perk in config.passPerks)
            {
                UIKit.Image(card, "Bullet", UIKit.Sparkle, style.Gold, top, new Vector2(-width * 0.5f + style.perkTextSize * 0.4f, -y),
                    Vector2.one * style.perkTextSize * 0.8f);
                var line = UIKit.Text(card, "Perk", UIKit.BodyFont(style), style.perkTextSize, style.text, top,
                    new Vector2(style.perkTextSize * 0.6f, -y), new Vector2(width - style.perkTextSize * 1.2f, style.perkSpacing), 0.5f,
                    FontStyles.Normal, TextAlignmentOptions.Left);
                line.textWrappingMode = TextWrappingModes.Normal;
                line.text = string.Format(perk, config.passBonusCoins);
                y += style.perkSpacing;
            }

            // Price button: gold, with a soft glow.
            var bottom = new Vector2(0.5f, 0f);
            float buttonY = style.buyButtonSize.y * 2.1f;
            UIKit.Image(card, "Glow", Art.SoftCircle, UIKit.WithAlpha(style.Gold, 0.14f), bottom, new Vector2(0f, buttonY),
                style.buyButtonSize * 1.35f);
            var button = UIKit.PillButton(card, "Buy", style, "", bottom, new Vector2(0f, buttonY), style.buyButtonSize, Buy,
                out buyLabel, style.Gold);
            buyButton = (RectTransform)button.transform;
            var fill = button.GetComponent<Image>();
            fill.color = UIKit.WithAlpha(style.Gold, style.switchOnFillAlpha);
            fill.pixelsPerUnitMultiplier = UIKit.PillCorners(style.buyButtonSize.y);
            buyButton.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = UIKit.PillCorners(style.buyButtonSize.y);
            buyLabel.font = UIKit.TitleFont(style);
            buyLabel.fontSize = style.playTextSize * 0.9f;
            buyLabel.color = style.Gold;

            var note = UIKit.Text(card, "Note", UIKit.BodyFont(style), style.footerTextSize, UIKit.WithAlpha(style.text, style.labelAlpha),
                bottom, new Vector2(0f, buttonY - style.buyButtonSize.y * 0.85f), new Vector2(width, 36f), 2f);
            note.text = "One-time purchase · restore it anytime in Settings";
            message = UIKit.Text(card, "Message", UIKit.BodyFont(style), style.footerTextSize + 4f, style.Gold, bottom,
                new Vector2(0f, buttonY + style.buyButtonSize.y * 0.9f), new Vector2(width, 44f), 2f);
        }
    }
}
