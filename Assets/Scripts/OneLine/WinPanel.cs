using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// Win screen, shown after the completion burst: "Constellation complete", the level name in gold,
    /// a 1–3 star rating (LevelManager.LastStars: by the time left on timed levels, 3 otherwise) and a gold "Next" button with a gentle
    /// breathing glow, with "Map" beside it. Elements animate in with a small stagger. Next loads the next level —
    /// except after the last level of a zodiac house, when it becomes "Next House" and goes to the map, which plays
    /// the new house's reveal. Set LevelManager.autoAdvance = false when this panel is used.
    /// </summary>
    public class WinPanel : MonoBehaviour
    {
        public UIStyle style;
        public LevelManager levelManager;
        [Tooltip("Map button, and the map's house reveal after the last level of a house.")]
        public ScreenRouter router;
        [Tooltip("Level-complete animation: the panel appears when it finishes.")]
        public CompletionSequence completion;

        CanvasGroup group;
        RectTransform card;
        Cartouche cartouche;
        TextMeshProUGUI nameText;
        Image nextGlow;
        TextMeshProUGUI nextLabel;
        bool houseDone;
        readonly CanvasGroup[] parts = new CanvasGroup[4];
        readonly Vector2[] partPos = new Vector2[4];
        readonly Image[] ratingStars = new Image[3];
        bool showing;

        PathManager Path => levelManager ? levelManager.pathManager : null;
        Color Gold => style.Gold;

        void Awake()
        {
            if (!style) { enabled = false; return; }
            Build();
        }

        void OnEnable()
        {
            if (!style) return;
            if (completion) completion.Finished += OnSequenceFinished;
            else if (Path) Path.Completed += OnCompleted;
            if (levelManager) levelManager.LevelLoaded += OnLevelLoaded;
        }

        void OnDisable()
        {
            if (completion) completion.Finished -= OnSequenceFinished;
            else if (Path) Path.Completed -= OnCompleted;
            if (levelManager) levelManager.LevelLoaded -= OnLevelLoaded;
        }

        // Next button breathes softly while the panel is up (no allocations).
        void Update()
        {
            if (!showing) return;
            float k = 0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / 2.4f);
            nextGlow.color = UIKit.WithAlpha(Gold, 0.12f + 0.2f * k);
            nextGlow.rectTransform.localScale = Vector3.one * (1f + 0.04f * k);
        }

        void OnCompleted()
        {
            // Let the completion burst play first.
            Tween.Run(this, style.winScreenDelay, Ease.Linear, null, () =>
            {
                if (Path && !Path.suspended) Show(); // not if the player went back to the home screen meanwhile
            });
        }

        void OnSequenceFinished()
        {
            if (Path && !Path.suspended) Show();
        }

        void OnLevelLoaded(int _)
        {
            if (group.gameObject.activeSelf) UIKit.Close(group, card, style);
            showing = false;
        }

        public void Show()
        {
            int number = levelManager.CurrentNumber;
            nameText.text = style.LevelTitle(number);
            // The last level of a house: Next leads to the map, where the new house is revealed.
            houseDone = router && router.map && Zodiac.IsFirstOfSign(number + 1) && number < levelManager.LevelCount;
            nextLabel.text = houseDone ? "Next House" : "Next";
            if (cartouche) cartouche.SetGlyph(ZodiacGlyphs.Sign(levelManager.CurrentSign), style.Zodiac.glyphStroke);
            // Earned stars in gold, the rest as faint outlines of light.
            for (int i = 0; i < ratingStars.Length; i++)
                ratingStars[i].color = i < levelManager.LastStars ? Gold : UIKit.WithAlpha(style.text, 0.14f);
            showing = true;
            UIKit.Open(group, card, style);
            // Staggered entrance: each part fades in and rises a little.
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var target = partPos[i];
                part.alpha = 0f;
                float delay = style.stagger * (i + 1);
                float total = delay + style.titleInTime;
                Tween.Run(part, total, Ease.Linear, k =>
                {
                    float t = Ease.OutCubic(Mathf.Clamp01((k * total - delay) / style.titleInTime));
                    part.alpha = t;
                    ((RectTransform)part.transform).anchoredPosition = target + new Vector2(0f, -style.titleRise * (1f - t));
                });
            }
        }

        public bool IsOpen => group && group.gameObject.activeSelf;

        /// <summary>Closes at once (leaving the game screen).</summary>
        public void Hide()
        {
            showing = false;
            if (group) group.gameObject.SetActive(false);
        }

        void Next()
        {
            showing = false;
            UIKit.Close(group, card, style);
            if (houseDone) { router.ShowMap(); return; }
            if (levelManager) levelManager.LoadNext();
        }

        void OpenMap()
        {
            showing = false;
            UIKit.Close(group, card, style);
            if (router) router.ShowMap();
        }

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Win Canvas", 30, true);
            (group, card) = UIKit.Modal(canvas.transform, "Win", style, new Vector2(800f, 720f), null);
            cartouche = card.GetComponent<Cartouche>();
            var mid = new Vector2(0.5f, 0.5f);

            parts[0] = Part(card, "Label", new Vector2(0f, 250f), new Vector2(700f, 50f), 0);
            var label = UIKit.Text(parts[0].transform, "Text", UIKit.BodyFont(style), 30f, UIKit.WithAlpha(style.text, style.labelAlpha),
                mid, Vector2.zero, new Vector2(700f, 50f), 16f, FontStyles.SmallCaps | FontStyles.Bold);
            label.text = "Constellation complete";

            parts[1] = Part(card, "Name", new Vector2(0f, 150f), new Vector2(760f, 110f), 1);
            nameText = UIKit.Text(parts[1].transform, "Text", UIKit.TitleFont(style), 80f, Gold, mid, Vector2.zero,
                new Vector2(760f, 110f), 4f);
            UIKit.Glow(nameText, UIKit.WithAlpha(Gold, 0.5f), 0.35f);

            parts[2] = Part(card, "Rating", new Vector2(0f, 10f), new Vector2(400f, 110f), 2);
            for (int i = 0; i < ratingStars.Length; i++)
            {
                float size = i == 1 ? 96f : 76f;
                ratingStars[i] = UIKit.Image(parts[2].transform, "Star", Art.Star, Gold, mid,
                    new Vector2((i - 1) * 110f, i == 1 ? 12f : 0f), new Vector2(size, size));
            }

            // Map (glass) and Next (gold, glowing) side by side.
            var button = style.winButtonSize;
            float dx = (button.x + style.winButtonGap) * 0.5f;
            parts[3] = Part(card, "Buttons", new Vector2(0f, -200f), new Vector2(2f * button.x + style.winButtonGap, 130f), 3);
            var map = UIKit.PillButton(parts[3].transform, "Map", style, "Map", mid, new Vector2(-dx, 0f), button, OpenMap,
                out var mapLabel);
            mapLabel.font = UIKit.TitleFont(style);
            mapLabel.fontSize = 40f;
            Pill(map.gameObject, button.y);
            // Soft elliptical glow behind Next (not a box).
            nextGlow = UIKit.Image(parts[3].transform, "Glow", Art.SoftCircle, UIKit.WithAlpha(Gold, 0.15f), mid, new Vector2(dx, 0f),
                new Vector2(button.x * 1.5f, button.y * 1.9f));
            var next = UIKit.PillButton(parts[3].transform, "Next", style, "Next", mid, new Vector2(dx, 0f), button, Next,
                out nextLabel, Gold);
            Pill(next.gameObject, button.y);
            next.GetComponent<Image>().color = UIKit.WithAlpha(Gold, style.switchOnFillAlpha);
            nextLabel.color = Gold;
            nextLabel.font = UIKit.TitleFont(style);
            nextLabel.enableAutoSizing = true;
            nextLabel.fontSizeMin = 26f;
            nextLabel.fontSizeMax = 44f;
            nextLabel.rectTransform.sizeDelta = new Vector2(button.x - 40f, button.y);
        }

        // Pill-round ends at this height.
        static void Pill(GameObject button, float height)
        {
            float corners = UIKit.PillCorners(height);
            button.GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            button.transform.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = corners;
        }

        CanvasGroup Part(Transform parent, string name, Vector2 pos, Vector2 size, int index)
        {
            var rt = UIKit.Rect(parent, name, new Vector2(0.5f, 0.5f), pos, size);
            partPos[index] = pos;
            return rt.gameObject.AddComponent<CanvasGroup>();
        }
    }
}
