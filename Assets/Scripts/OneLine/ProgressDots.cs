using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// The bottom row of the HUD, drawn as a tiny constellation (small stars joined by a faint line).
    /// TierProgress (default): the 10 levels of the current tier — done ones lit, the current one twinkling,
    /// the boss star gold. StarsInLevel: one star per node of the level, lit as the path reaches it.
    /// Builds inside HUD.BottomRow.
    /// </summary>
    public class ProgressDots : MonoBehaviour
    {
        public PathManager pathManager;
        public LevelManager levelManager;
        public HUD hud;
        public UIStyle style;

        static readonly float[] Zigzag = { 0f, 1f, -0.6f, 0.8f, -1f, 0.4f, 1f, -0.4f, 0.6f, 0f };

        RectTransform root;
        readonly List<Image> stars = new();
        readonly List<Image> links = new();
        int current = -1;
        Color accent = Color.white;

        void Start()
        {
            if (!hud || !hud.BottomRow || !style) { enabled = false; return; }
            root = hud.BottomRow;
            if (levelManager && levelManager.CurrentIndex >= 0 && pathManager && pathManager.Level) OnLevelLoaded(0);
        }

        void OnEnable()
        {
            if (levelManager) levelManager.LevelLoaded += OnLevelLoaded;
            if (!pathManager) return;
            pathManager.NodeVisited += OnNodeVisited;
            pathManager.PathCleared += OnPathCleared;
        }

        void OnDisable()
        {
            if (levelManager) levelManager.LevelLoaded -= OnLevelLoaded;
            if (!pathManager) return;
            pathManager.NodeVisited -= OnNodeVisited;
            pathManager.PathCleared -= OnPathCleared;
        }

        bool TierMode => style.bottomRow == UIStyle.BottomRowMode.TierProgress;
        Color Gold => style.starStyle ? style.starStyle.finalColor : Color.yellow;
        Color Dim => UIKit.WithAlpha(style.text, 0.2f);

        // Current tier star twinkles (math only, no allocations).
        void Update()
        {
            if (!style || !TierMode || current < 0 || current >= stars.Count) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            var star = stars[current];
            star.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.35f, k) * BaseScale(current);
            var c = current == stars.Count - 1 && TierMode ? Gold : accent;
            star.color = Color.Lerp(c, style.text, k * 0.6f);
        }

        float BaseScale(int i) => TierMode && i == stars.Count - 1 ? 1.35f : 1f; // boss star is bigger

        void OnLevelLoaded(int _)
        {
            if (!root || !style) return;
            var tier = levelManager.CurrentTier;
            accent = tier != null ? tier.path : style.text;
            int count = TierMode ? Difficulty.LevelsPerTier : pathManager.Nodes.Count;
            Rebuild(count);
            if (!TierMode) { current = -1; return; }
            current = (levelManager.CurrentNumber - 1) % Difficulty.LevelsPerTier;
            for (int i = 0; i < stars.Count; i++)
            {
                bool boss = i == stars.Count - 1;
                stars[i].color = i < current ? (boss ? Gold : UIKit.WithAlpha(accent, 0.9f)) : boss ? UIKit.WithAlpha(Gold, 0.35f) : Dim;
                stars[i].rectTransform.localScale = Vector3.one * BaseScale(i);
            }
            for (int i = 0; i < links.Count; i++)
                links[i].color = UIKit.WithAlpha(i < current ? accent : style.text, i < current ? 0.35f : 0.12f);
        }

        void OnNodeVisited(Node node, int visitIndex, Color color)
        {
            if (!style || TierMode || visitIndex >= stars.Count) return;
            var star = stars[visitIndex];
            star.color = color;
            Tween.Run(star, 0.35f, Ease.OutCubic, k =>
                star.rectTransform.localScale = Vector3.one * (k < 0.3f ? Mathf.Lerp(1f, 1.4f, k / 0.3f) : Mathf.Lerp(1.4f, 1.15f, (k - 0.3f) / 0.7f)));
            if (visitIndex > 0) links[visitIndex - 1].color = UIKit.WithAlpha(color, 0.4f);
        }

        void OnPathCleared()
        {
            if (!style || TierMode) return;
            foreach (var s in stars) { s.color = Dim; s.rectTransform.localScale = Vector3.one; }
            foreach (var l in links) l.color = UIKit.WithAlpha(style.text, 0.12f);
        }

        void Rebuild(int count)
        {
            if (stars.Count == count) return;
            foreach (var s in stars) if (s) Destroy(s.gameObject);
            foreach (var l in links) if (l) Destroy(l.gameObject);
            stars.Clear();
            links.Clear();

            float width = root.rect.width > 0 ? root.rect.width : 600f;
            float spacing = Mathf.Min(style.progressSpacing, width / Mathf.Max(1, count));
            float size = Mathf.Min(style.progressStarSize, spacing * 0.7f);
            var positions = new Vector2[count];
            for (int i = 0; i < count; i++)
                positions[i] = new Vector2((i - (count - 1) * 0.5f) * spacing, Zigzag[i % Zigzag.Length] * size * 0.45f);

            var mid = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < count - 1; i++) // faint joining lines, under the stars
            {
                Vector2 a = positions[i], b = positions[i + 1];
                var link = UIKit.Image(root, "Link", null, Dim, mid, (a + b) * 0.5f, new Vector2((b - a).magnitude, 2f));
                link.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                links.Add(link);
            }
            for (int i = 0; i < count; i++)
                stars.Add(UIKit.Image(root, "Star", Art.Star, Dim, mid, positions[i], new Vector2(size, size)));
        }
    }
}
