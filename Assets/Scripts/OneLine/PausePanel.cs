using TMPro;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The small pause menu of timed levels (opened by the HUD's astrolabe menu button, which replaces the Settings +
    /// Map buttons there): Resume, Map, Settings, Restart, with the time left under the title. While it (or anything
    /// it opens) is up, LevelTimer is paused and the board is hidden. Restart clears the drawn line only — like every
    /// retry it doesn't refill the hourglass.
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        public UIStyle style;
        public LevelTimer timer;
        public ScreenRouter router;
        public SettingsPanel settings;

        CanvasGroup group;
        RectTransform card;
        TextMeshProUGUI timeText;

        TimerSettings S => timer ? timer.settings : null;
        PathManager Path => timer && timer.levelManager ? timer.levelManager.pathManager : null;
        public bool IsOpen => group && group.gameObject.activeSelf;

        void Awake()
        {
            if (!style || !timer || !timer.settings) { Debug.LogError("PausePanel: assign a UIStyle and a LevelTimer.", this); enabled = false; return; }
            Build();
        }

        public void Open()
        {
            if (!group || timer.State == TimerState.Done) return; // the completion is playing
            int sec = Mathf.CeilToInt(timer.Remaining);
            if (timer.IsTimed) timeText.text = $"{sec / 60}:{sec % 60:00} left in the hourglass"; // once per open
            else timeText.text = "";
            UIKit.Open(group, card, style);
        }

        public void Close()
        {
            if (IsOpen) UIKit.Close(group, card, style);
        }

        /// <summary>Closes at once (leaving the game screen).</summary>
        public void Hide()
        {
            if (group) group.gameObject.SetActive(false);
        }

        void OpenMap()
        {
            Hide();
            if (router) router.ShowMap();
        }

        void OpenSettings()
        {
            if (settings) settings.Open(); // on top; closing it comes back here
        }

        void Restart()
        {
            if (Path) Path.ResetPath();
            Close();
        }

        void Build()
        {
            var s = S;
            var canvas = UIKit.MakeCanvas(transform, "Pause Canvas", 18, true);
            (group, card) = UIKit.Modal(canvas.transform, "Pause", style, s.pauseSize, Close);
            var frame = card.GetComponent<Cartouche>();
            if (frame.Glyph) UIKit.DrawAstrolabe(frame.Glyph, frame.MedallionRadius * 0.62f, style.Zodiac.gold, 1.6f);
            var top = new Vector2(0.5f, 1f);

            UIKit.PanelTitle(card, style, "Paused", style.text);
            UIKit.CloseButton(card, style, Close);
            float y = style.panelTitleY + 76f;
            timeText = UIKit.Text(card, "Time Left", UIKit.BodyFont(style), 28f, UIKit.WithAlpha(style.text, style.labelAlpha), top,
                new Vector2(0f, -y), new Vector2(620f, 44f), 4f, FontStyles.SmallCaps);

            var size = s.panelButtonSize;
            float step = size.y + s.panelButtonGap, by = y + 110f;
            PanelKit.Button(card, style, "Resume", "Resume", top, new Vector2(0f, -by), size, true, Close, out _);
            PanelKit.Button(card, style, "Map", "Map", top, new Vector2(0f, -(by + step)), size, false, OpenMap, out _);
            PanelKit.Button(card, style, "Settings", "Settings", top, new Vector2(0f, -(by + 2f * step)), size, false, OpenSettings, out _);
            PanelKit.Button(card, style, "Restart", "Restart", top, new Vector2(0f, -(by + 3f * step)), size, false, Restart, out _);
        }
    }
}
