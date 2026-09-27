using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Settings, in the celestial frame: Music, Sound Effects and Haptics as sun/moon switches (saved by SaveService and
    /// applied at once), Restore Purchases, language and privacy-policy placeholders, Restart level (in a level only),
    /// then the version and credits.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public UIStyle style;
        public AppConfig config;
        public CosmeticCatalog catalog;
        public PathManager pathManager;
        [Tooltip("Restart level is offered only while a level is on screen.")]
        public ScreenRouter router;

        CanvasGroup group;
        RectTransform card;
        CelestialSwitch music, sfx, haptics, reduceMotion;
        TextMeshProUGUI message;
        GameObject restartRow;
        bool restoring;

        public bool IsOpen => group && group.gameObject.activeSelf;

        void Awake()
        {
            if (!style) { enabled = false; return; }
            Build();
        }

        public void Open()
        {
            music.Set(SaveService.Music, false);
            sfx.Set(SaveService.Sfx, false);
            haptics.Set(SaveService.Haptics, false);
            reduceMotion.Set(SaveService.ReduceMotion, false);
            restartRow.SetActive(pathManager && pathManager.Level && (!router || router.Current == AppScreen.Game));
            message.text = "";
            UIKit.Open(group, card, style);
        }

        public void Close() => UIKit.Close(group, card, style);

        void Restore(Transform row)
        {
            if (restoring) return;
            restoring = true;
            message.text = "Checking the stars…";
            PurchaseService.Current.Restore(restored =>
            {
                restoring = false;
                if (restored) Entitlements.GrantPass(config, catalog);
                if (message) message.text = restored ? "Purchases restored" : "Nothing to restore";
                if (!restored && row) UIKit.Wiggle(row, style);
            });
        }

        void Privacy(Transform row)
        {
            if (config && !string.IsNullOrEmpty(config.privacyPolicyUrl)) Application.OpenURL(config.privacyPolicyUrl);
            else { message.text = "The privacy policy is coming soon"; UIKit.Wiggle(row, style); }
        }

        // ---------- building ----------

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Settings Canvas", 25, true);
            (group, card) = UIKit.Modal(canvas.transform, "Settings", style, style.settingsSize, Close);
            var frame = card.GetComponent<Cartouche>();
            UIKit.DrawAstrolabe(frame.Glyph, frame.MedallionRadius * 0.62f, style.Zodiac.gold);
            UIKit.PanelTitle(card, style, "Settings", style.text);
            UIKit.CloseButton(card, style, Close);

            // Switches.
            music = SwitchRow("Music", 0, v => SaveService.Music = v);
            sfx = SwitchRow("Sound Effects", 1, v => SaveService.Sfx = v);
            haptics = SwitchRow("Haptics", 2, v => { SaveService.Haptics = v; if (v) UIKit.LightHaptic(); });
            reduceMotion = SwitchRow("Reduce Motion", 3, v => SaveService.ReduceMotion = v);

            // Links.
            RectTransform restore = null, privacy = null, language = null;
            restore = Row("Restore Purchases", 4, () => Restore(restore), "›");
            language = Row("Language", 5, () => { message.text = "More languages are coming"; UIKit.Wiggle(language, style); }, "English");
            privacy = Row("Privacy Policy", 6, () => Privacy(privacy), "›");
            var restart = Row("Restart level", 7, () => { if (pathManager) pathManager.ResetPath(); Close(); }, "›");
            restartRow = restart.gameObject;

            message = UIKit.Text(card, "Message", UIKit.BodyFont(style), style.footerTextSize + 4f, style.Gold, new Vector2(0.5f, 1f),
                new Vector2(0f, -(RowY(8) + style.settingsRowHeight * 0.1f)), new Vector2(style.settingsRowWidth, 44f), 2f);

            // Footer.
            var dim = UIKit.WithAlpha(style.text, style.labelAlpha * 0.8f);
            var version = UIKit.Text(card, "Version", UIKit.BodyFont(style), style.footerTextSize, dim, new Vector2(0.5f, 0f),
                new Vector2(0f, style.settingsFooterY + style.footerTextSize * 1.6f), new Vector2(style.settingsRowWidth, 36f), 3f);
            version.text = $"Version {Application.version}";
            var credits = UIKit.Text(card, "Credits", UIKit.BodyFont(style), style.footerTextSize, dim, new Vector2(0.5f, 0f),
                new Vector2(0f, style.settingsFooterY), new Vector2(style.settingsRowWidth, 36f), 1f);
            credits.text = config ? config.credits : "";
        }

        float RowY(int index) =>
            style.settingsFirstRowY + index * style.settingsRowHeight + (index >= SwitchCount ? style.settingsSectionGap : 0f);

        const int SwitchCount = 4;

        CelestialSwitch SwitchRow(string label, int index, System.Action<bool> changed)
        {
            CelestialSwitch sw = null;
            var row = Row(label, index, () => sw.Toggle(), null);
            sw = CelestialSwitch.Create(row, style, new Vector2(1f, 0.5f), new Vector2(-style.switchSize.x * 0.5f, 0f), changed);
            return sw;
        }

        // A full-width tappable row: label on the left, an optional value on the right, a hairline underneath.
        RectTransform Row(string label, int index, UnityAction onClick, string value)
        {
            var size = new Vector2(style.settingsRowWidth, style.settingsRowHeight);
            var hit = UIKit.Image(card, label, null, Color.clear, new Vector2(0.5f, 1f), new Vector2(0f, -RowY(index)), size);
            hit.raycastTarget = true;
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(UIKit.Click);

            var text = UIKit.Text(hit.transform, "Label", UIKit.BodyFont(style), style.settingsTextSize, style.text,
                new Vector2(0f, 0.5f), new Vector2(size.x * 0.35f, 0f), new Vector2(size.x * 0.7f, size.y), 1f,
                FontStyles.Normal, TextAlignmentOptions.Left);
            text.text = label;
            if (value != null)
            {
                var v = UIKit.Text(hit.transform, "Value", UIKit.BodyFont(style), style.settingsTextSize,
                    UIKit.WithAlpha(style.text, style.labelAlpha), new Vector2(1f, 0.5f), new Vector2(-size.x * 0.2f, 0f),
                    new Vector2(size.x * 0.4f, size.y), 1f, FontStyles.Normal, TextAlignmentOptions.Right);
                v.text = value;
            }
            UIKit.Image(hit.transform, "Line", null, UIKit.WithAlpha(style.Zodiac.gold, style.rowLineAlpha), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(size.x, style.hairline));
            return hit.rectTransform;
        }
    }
}
