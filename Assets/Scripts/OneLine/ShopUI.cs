using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Shop in the celestial frame (opened from the home screen and the HUD coin counter): a "Boosts" section (the two
    /// time boosts in packs of 1 and 5 — the only purchases that affect gameplay) and the cosmetics.
    /// It only talks to CoinManager, Boosts and Cosmetics — never to PathManager or any gameplay code. Exclusive items
    /// come with the Celestial Pass, so their button opens the pass instead of spending coins.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public CosmeticCatalog catalog;
        public UIStyle style;
        [Tooltip("Opened by the button of exclusive items.")]
        public PremiumPanel premium;
        [Tooltip("Boost prices and pack size. Without it the Boosts section is left out.")]
        public TimerSettings timerSettings;

        class Row
        {
            public CosmeticCatalog.Item item;
            public Image outline;
            public TextMeshProUGUI label;
        }

        CanvasGroup group;
        RectTransform card;
        TextMeshProUGUI balanceText, messageText;
        readonly List<Row> rows = new();

        class BoostRow
        {
            public BoostType type;
            public TextMeshProUGUI owned;
            public readonly TextMeshProUGUI[] prices = new TextMeshProUGUI[2];
            public readonly Image[] outlines = new Image[2];
        }

        readonly List<BoostRow> boostRows = new();

        public bool IsOpen => group && group.gameObject.activeSelf;
        Color Gold => style.Gold;

        void Awake()
        {
            if (!catalog || !style)
            {
                Debug.LogError("ShopUI: assign a CosmeticCatalog and a UIStyle.", this);
                enabled = false;
                return;
            }
            Boosts.Configure(timerSettings);
            Build();
            Refresh();
        }

        void OnEnable()
        {
            CoinManager.BalanceChanged += OnBalanceChanged;
            Cosmetics.Changed += Refresh;
            Boosts.Changed += OnBoostsChanged;
        }

        void OnDisable()
        {
            CoinManager.BalanceChanged -= OnBalanceChanged;
            Cosmetics.Changed -= Refresh;
            Boosts.Changed -= OnBoostsChanged;
        }

        public void Open()
        {
            messageText.text = "";
            Refresh();
            UIKit.Open(group, card, style);
        }

        public void Close() => UIKit.Close(group, card, style);

        void OnBalanceChanged(int balance, int delta) => Refresh();
        void OnBoostsChanged(BoostType type, int count) => Refresh();

        void OnBoostClicked(BoostRow row, int amount)
        {
            if (!Boosts.TryBuy(row.type, amount))
            {
                messageText.SetText("{0} more coins needed", Boosts.Missing(row.type, amount));
                return;
            }
            messageText.text = $"{Boosts.DisplayName(row.type)} ×{amount} added";
        }

        void OnRowClicked(Row row)
        {
            if (row.item.exclusive && !Cosmetics.IsOwned(row.item))
            {
                messageText.text = "Included with the Celestial Pass";
                if (premium) premium.Open();
                return;
            }
            if (!Cosmetics.IsOwned(row.item) && !Cosmetics.TryBuy(row.item))
            {
                messageText.text = $"{row.item.price - CoinManager.GetBalance()} more coins needed";
                return;
            }
            Cosmetics.Equip(row.item);
            messageText.text = "";
        }

        void Refresh()
        {
            if (!balanceText) return;
            balanceText.SetText("{0} coins", CoinManager.GetBalance());
            foreach (var row in rows)
            {
                bool owned = Cosmetics.IsOwned(row.item);
                bool equipped = owned && IsShownAsEquipped(row.item);
                row.label.text = equipped ? "Equipped" : owned ? "Equip" : row.item.exclusive ? "Pass" : $"{row.item.price}";
                // Gold = costs coins, white = yours, faint = in use.
                var c = equipped ? UIKit.WithAlpha(style.text, 0.45f) : owned ? style.text : Gold;
                row.label.color = c;
                row.outline.color = UIKit.WithAlpha(c, equipped ? 0.15f : 0.55f);
            }
            int balance = CoinManager.GetBalance();
            foreach (var row in boostRows)
            {
                row.owned.SetText("You have {0}", Boosts.Count(row.type));
                for (int i = 0; i < 2; i++)
                {
                    int amount = i == 0 ? 1 : timerSettings.packSize;
                    int price = Boosts.Price(row.type, amount);
                    row.prices[i].SetText("{0} for {1}", (float)amount, price);
                    var c = price <= balance ? Gold : UIKit.WithAlpha(style.text, 0.55f);
                    row.prices[i].color = c;
                    row.outlines[i].color = UIKit.WithAlpha(c, 0.55f);
                }
            }
        }

        // With nothing equipped yet, the default (first) item of each list counts as equipped.
        bool IsShownAsEquipped(CosmeticCatalog.Item item) => item is CosmeticCatalog.LineSkin
            ? catalog.EquippedSkin() == item
            : catalog.EquippedBackground() == item;

        // ---------- building ----------

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Shop Canvas", 20, true);
            (group, card) = UIKit.Modal(canvas.transform, "Shop", style, new Vector2(940f, 1560f), Close);
            card.GetComponent<Cartouche>().SetIcon(UIKit.Coin);
            var top = new Vector2(0.5f, 1f);

            UIKit.PanelTitle(card, style, "Shop", style.text);
            float y0 = style.panelTitleY + style.shopHeaderGap;
            var note = UIKit.Text(card, "Note", UIKit.BodyFont(style), 26f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(0f, -y0), new Vector2(800f, 40f), 6f, FontStyles.SmallCaps);
            note.text = timerSettings ? "Boosts add time on timed levels · cosmetics are visual only"
                                      : "Cosmetic only — no effect on gameplay";
            balanceText = UIKit.Text(card, "Balance", UIKit.BodyFont(style), 36f, Gold, top, new Vector2(0f, -(y0 + 54f)),
                new Vector2(800f, 50f), 3f, FontStyles.Bold);
            UIKit.CloseButton(card, style, Close);

            // Scrolling list, so it fits any screen.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(card, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(30f, 110f);
            srt.offsetMax = new Vector2(-30f, -(y0 + 104f));
            scrollGo.GetComponent<Image>().color = Color.clear; // catches drags
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(srt, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            float y = -30f;
            if (timerSettings) y = BoostSection(content, y) - 20f;
            y = Section(content, "Line & stars", catalog.lineSkins, y);
            y = Section(content, "Sky", catalog.backgrounds, y - 20f);
            content.sizeDelta = new Vector2(0f, -y);

            messageText = UIKit.Text(card, "Message", UIKit.BodyFont(style), 30f, Gold, new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(800f, 50f), 3f);
        }

        float Section<T>(Transform parent, string title, List<T> items, float y) where T : CosmeticCatalog.Item
        {
            var top = new Vector2(0.5f, 1f);
            var header = UIKit.Text(parent, title, UIKit.BodyFont(style), 26f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(-230f, y), new Vector2(380f, 44f), 12f, FontStyles.SmallCaps | FontStyles.Bold, TextAlignmentOptions.Left);
            header.text = title;
            y -= 64f;
            foreach (var item in items)
            {
                var rowCard = UIKit.Card(parent, item.displayName, style, top, new Vector2(0f, y), new Vector2(820f, 112f));
                rowCard.GetComponent<Image>().color = UIKit.WithAlpha(style.panelFill, 0.5f);
                rowCard.Find("Outline").GetComponent<Image>().color = UIKit.WithAlpha(style.Zodiac.gold, style.rowLineAlpha * 2f);
                var (a, b) = Swatches(item);
                UIKit.Image(rowCard, "Swatch A", Art.Star, a, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(52f, 52f));
                UIKit.Image(rowCard, "Swatch B", Art.Circle, b, new Vector2(0f, 0.5f), new Vector2(122f, 0f), new Vector2(40f, 40f));
                var name = UIKit.Text(rowCard, "Name", UIKit.BodyFont(style), 36f, style.text, new Vector2(0f, 0.5f),
                    new Vector2(330f, 0f), new Vector2(360f, 60f), 2f, FontStyles.Bold, TextAlignmentOptions.Left);
                name.text = item.displayName;

                var row = new Row { item = item };
                var button = UIKit.PillButton(rowCard, "Action", style, "", new Vector2(1f, 0.5f), new Vector2(-130f, 0f),
                    new Vector2(210f, 80f), () => OnRowClicked(row), out row.label);
                row.outline = button.transform.Find("Outline").GetComponent<Image>();
                button.GetComponent<Image>().color = UIKit.WithAlpha(style.panelFill, 0.4f);
                row.label.fontSize = 30f;
                rows.Add(row);
                y -= 128f;
            }
            return y;
        }

        // Saturn's Gift and Lunar Stillness: glyph in a small dial, what it does, how many you have, packs of 1 and 5.
        float BoostSection(Transform parent, float y)
        {
            var top = new Vector2(0.5f, 1f);
            var header = UIKit.Text(parent, "Boosts", UIKit.BodyFont(style), 26f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(-230f, y), new Vector2(380f, 44f), 12f, FontStyles.SmallCaps | FontStyles.Bold, TextAlignmentOptions.Left);
            header.text = "Boosts";
            y -= 64f;
            var t = timerSettings;
            foreach (BoostType type in new[] { BoostType.SaturnsGift, BoostType.LunarStillness })
            {
                bool saturn = type == BoostType.SaturnsGift;
                var rowCard = UIKit.Card(parent, Boosts.DisplayName(type), style, top, new Vector2(0f, y), new Vector2(820f, 128f));
                rowCard.GetComponent<Image>().color = UIKit.WithAlpha(style.panelFill, 0.5f);
                rowCard.Find("Outline").GetComponent<Image>().color = UIKit.WithAlpha(style.Zodiac.gold, style.rowLineAlpha * 2f);
                var left = new Vector2(0f, 0.5f);
                var dial = UIKit.Vector(rowCard, "Dial", left, new Vector2(70f, 0f), 96f);
                dial.Dial(36f, style.Zodiac.dialWidth, style.Zodiac.Line, 4f);
                dial.Glyph(ZodiacGlyphs.Planet(saturn ? 6 : 1), Vector2.zero, 15f, style.Zodiac.glyphStroke,
                    saturn ? Gold : Color.Lerp(Gold, t.sandSilver, 0.7f));
                var name = UIKit.Text(rowCard, "Name", UIKit.BodyFont(style), 32f, style.text, left, new Vector2(300f, 22f),
                    new Vector2(340f, 44f), 1f, FontStyles.Bold, TextAlignmentOptions.Left);
                name.text = Boosts.DisplayName(type);
                var row = new BoostRow { type = type };
                row.owned = UIKit.Text(rowCard, "Owned", UIKit.BodyFont(style), 22f, UIKit.WithAlpha(style.text, style.labelAlpha), left,
                    new Vector2(300f, -22f), new Vector2(340f, 36f), 2f, FontStyles.Normal, TextAlignmentOptions.Left);
                var what = UIKit.Text(rowCard, "What", UIKit.BodyFont(style), 22f, UIKit.WithAlpha(style.text, style.labelAlpha), left,
                    new Vector2(300f, -46f), new Vector2(340f, 30f), 1f, FontStyles.Italic, TextAlignmentOptions.Left);
                what.text = saturn ? $"+{Mathf.RoundToInt(t.saturnSeconds)} seconds"
                                   : $"time at {Mathf.RoundToInt(t.lunarSpeed * 100f)}% for {Mathf.RoundToInt(t.lunarSeconds)} s";
                row.owned.rectTransform.anchoredPosition = new Vector2(300f, -12f);
                what.rectTransform.anchoredPosition = new Vector2(300f, -40f);
                for (int i = 0; i < 2; i++)
                {
                    int amount = i == 0 ? 1 : t.packSize;
                    int index = i;
                    var button = UIKit.PillButton(rowCard, i == 0 ? "Buy One" : "Buy Pack", style, "", new Vector2(1f, 0.5f),
                        new Vector2(i == 0 ? -265f : -95f, 0f), new Vector2(160f, 76f), () => OnBoostClicked(row, amount),
                        out row.prices[index]);
                    row.outlines[index] = button.transform.Find("Outline").GetComponent<Image>();
                    button.GetComponent<Image>().color = UIKit.WithAlpha(style.panelFill, 0.4f);
                    row.prices[index].fontSize = 26f;
                }
                boostRows.Add(row);
                y -= 144f;
            }
            return y;
        }

        // Two color dots for a row; default items show the default look.
        static (Color, Color) Swatches(CosmeticCatalog.Item item)
        {
            if (item.useTierColors)
                return item is CosmeticCatalog.LineSkin
                    ? (new Color(0.35f, 0.82f, 1f), new Color(0.65f, 0.55f, 1f))
                    : (new Color(0.07f, 0.09f, 0.16f), new Color(0.3f, 0.35f, 0.5f));
            return item switch
            {
                CosmeticCatalog.LineSkin s => (s.line, s.node),
                CosmeticCatalog.Background g => (g.background, g.edge),
                _ => (Color.white, Color.gray),
            };
        }
    }
}
