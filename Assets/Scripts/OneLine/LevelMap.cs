using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// "The Zodiac Path": the level map as an antique star chart. Every level is a star on one long winding path that
    /// climbs through the 12 houses in order (bottom = level 1). Each house has a cartouche header ("ARIES — The Ram")
    /// and its large faint constellation behind it, which scrolls slower than the path (parallax).
    /// Completed levels are gold stars with rating pips joined by glowing lines with a traveling shimmer; the current
    /// level pulses with a comet circling it; locked levels are dim dots on a dashed line. HARD levels wear a thin ring,
    /// BOSS levels a medallion with the house glyph. Tap a reached level for its card (name, stars, Play); locked ones
    /// wiggle. Opens gliding to the current level; plays a reveal the first time a new house is reached.
    ///
    /// The layout is deterministic (a seeded sine per house) and computed once. Only the nodes and path pieces near the
    /// view exist (pooled), and scrolling allocates nothing.
    /// </summary>
    public class LevelMap : MonoBehaviour
    {
        public UIStyle style;
        public LevelManager levelManager;
        public ScreenRouter router;
        public ShopUI shop;

        /// <summary>Fades the whole screen (ScreenRouter).</summary>
        public CanvasGroup Group { get; private set; }
        /// <summary>The safe-area root — scaled a little during screen transitions.</summary>
        public RectTransform Content { get; private set; }
        public bool PopupOpen => popupGroup && popupGroup.gameObject.activeSelf;
        /// <summary>The house in the middle of the view changed (the sky follows it).</summary>
        public event Action<int> SignInView;

        // layout (content space: x from the center, y up from the bottom)
        int count;
        Vector2[] positions;
        float[] pathLength;              // path length from level 1 to each level
        readonly float[] headerY = new float[Zodiac.SignCount];
        readonly Vector2[] figurePos = new Vector2[Zodiac.SignCount];
        int houses;                      // houses that have levels
        float contentHeight;

        // scrolling
        ScrollRect scroll;
        RectTransform viewport, content, far;
        float viewHeight = -1f;
        Tween scrollTween;
        int shownSign = -1;

        // pooled nodes and path pieces
        Node[] nodeAt;
        readonly Stack<Node> nodePool = new();
        PathPiece[] pieceAt;
        readonly Stack<PathPiece> piecePool = new();
        RectTransform pathLayer, shimmerLayer, headerLayer, nodeLayer;
        int boundFrom = 0, boundTo = -1, boundUnlocked = -1;

        // progress
        int unlocked;                    // first unfinished level index (= levels finished)
        int current;                     // the level Continue opens, or -1 when everything is done
        public int Unlocked => unlocked;

        // houses
        readonly House[] house = new House[Zodiac.SignCount];

        // current level: halo, comet; shimmer along the lit path
        Image halo;
        readonly Image[] comet = new Image[5];
        Image[] shimmers;

        // popup
        CanvasGroup popupGroup;
        RectTransform popupCard;
        Cartouche popupFrame;
        TextMeshProUGUI popupLevel, popupName, popupTag, popupPlayLabel;
        readonly Image[] popupStars = new Image[3];
        Image popupGlow;
        int popupIndex = -1;

        bool revealing;
        bool OnScreen => !router || router.Current == AppScreen.Map;

        sealed class House
        {
            public RectTransform root;
            public CanvasGroup group, text;
            public Cartouche frame;
            public ConstellationFigure figure;
            public TextMeshProUGUI range;
        }

        void Awake()
        {
            if (!style || !levelManager) { enabled = false; return; }
            count = Mathf.Max(0, levelManager.levelPack || levelManager.testLevel ? levelManager.LevelCount : 0);
            ComputeLayout();
            Build();
        }

        // ---------- public ----------

        /// <summary>
        /// Called when the map screen opens: refreshes progress and glides to the current level, or plays the reveal
        /// of a house reached since the map was last seen. <paramref name="instant"/> jumps without gliding.
        /// </summary>
        public void Show(bool instant)
        {
            if (!enabled || count == 0) return;
            ClosePopup(true);
            RefreshProgress();
            int reachedHouse = Zodiac.SignOf(Mathf.Min(unlocked, count - 1) + 1);
            int revealed = SaveService.RevealedHouse;
            if (revealed < 0) SaveService.RevealedHouse = revealed = reachedHouse; // existing progress: no reveal
            for (int s = 0; s < houses; s++) SetHouseLit(s, s <= revealed);

            int focus = current >= 0 ? current : count - 1;
            if (reachedHouse > revealed && !instant)
            {
                SaveService.RevealedHouse = reachedHouse;
                PlayReveal(reachedHouse, focus);
            }
            else
            {
                if (reachedHouse > revealed) SaveService.RevealedHouse = reachedHouse;
                for (int s = 0; s < houses; s++) SetHouseLit(s, s <= reachedHouse);
                ScrollTo(focus, instant, null);
            }
        }

        /// <summary>Back: closes the level card if it is open.</summary>
        public bool CloseTop()
        {
            if (!PopupOpen) return false;
            ClosePopup(false);
            return true;
        }

        // ---------- layout ----------

        void ComputeLayout()
        {
            positions = new Vector2[count];
            pathLength = new float[count];
            nodeAt = new Node[count];
            int chunk = Mathf.Max(1, style.mapChunk);
            pieceAt = new PathPiece[Mathf.Max(1, (count + chunk - 1) / chunk)];
            var rnd = new System.Random(style.mapSeed);
            float y = style.mapBottomPad;
            int sign = -1, first = 0;
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                int s = Zodiac.SignOf(i + 1);
                if (s != sign)
                {
                    // A new house: space for its header between the last level of the previous one and its first.
                    y += style.mapHouseGap;
                    headerY[s] = y - style.mapHouseGap * 0.5f;
                    sign = s;
                    first = i;
                    phase = (float)(rnd.NextDouble() * 2.0 * Math.PI);
                    houses = s + 1;
                }
                else y += style.mapNodeSpacing;
                float wave = Mathf.Sin(phase + 2f * Mathf.PI * (i - first) / Mathf.Max(1f, style.mapWaveLevels));
                float jitter = ((float)rnd.NextDouble() * 2f - 1f) * style.mapJitter;
                positions[i] = new Vector2(style.mapAmplitude * (wave + jitter), y);
                pathLength[i] = i == 0 ? 0f : pathLength[i - 1] + Vector2.Distance(positions[i - 1], positions[i]);
            }
            contentHeight = (count > 0 ? positions[count - 1].y : style.mapBottomPad) + style.mapTopPad;

            // Each house's figure rises just above its header (so the reveal shows both), alternating sides.
            for (int s = 0; s < houses; s++)
                figurePos[s] = new Vector2((s % 2 == 0 ? -1f : 1f) * style.mapAmplitude * 0.45f,
                    headerY[s] + style.mapFigureSize * 0.5f);
        }

        public Vector2 Position(int index) => positions[index];

        // First level whose y is at or above y (binary search, no allocation).
        int IndexAtOrAbove(float y)
        {
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int m = (lo + hi) >> 1;
                if (positions[m].y < y) lo = m + 1; else hi = m;
            }
            return lo;
        }

        // ---------- per frame (no allocations) ----------

        void LateUpdate()
        {
            if (!Group || Group.alpha <= 0f || count == 0) return;

            // A touch takes over from the automatic glide.
            var pointer = Pointer.current;
            if (scrollTween != null && scrollTween.Alive && !revealing && pointer != null && pointer.press.wasPressedThisFrame)
                scrollTween.Kill();

            float h = viewport.rect.height;
            if (!Mathf.Approximately(h, viewHeight)) { viewHeight = h; PlaceFigures(); }
            float scrolled = -content.anchoredPosition.y;
            far.anchoredPosition = new Vector2(0f, content.anchoredPosition.y * style.mapParallax);

            // Nodes and path pieces near the view.
            float margin = style.mapPoolMargin;
            int from = Mathf.Max(0, IndexAtOrAbove(scrolled - margin) - 1);
            int to = Mathf.Min(count - 1, IndexAtOrAbove(scrolled + viewHeight + margin));
            if (from != boundFrom || to != boundTo || unlocked != boundUnlocked) Bind(from, to);

            // The house in the middle of the view.
            int center = Mathf.Clamp(IndexAtOrAbove(scrolled + viewHeight * 0.5f), 0, count - 1);
            int sign = Zodiac.SignOf(center + 1);
            if (sign != shownSign) { shownSign = sign; SignInView?.Invoke(sign); }

            Animate(from, to);
        }

        void Animate(int from, int to)
        {
            float t = Time.time;
            var gold = style.Gold;

            // The current level breathes; a small comet circles it ("you are here").
            bool here = current >= 0 && current >= from && current <= to;
            halo.enabled = here;
            foreach (var c in comet) c.enabled = here;
            if (here)
            {
                var p = positions[current];
                float b = 0.5f - 0.5f * Mathf.Cos(t * 2f * Mathf.PI / style.mapPulsePeriod);
                halo.rectTransform.anchoredPosition = p;
                halo.color = UIKit.WithAlpha(gold, 0.12f + 0.16f * b);
                halo.rectTransform.localScale = Vector3.one * (0.85f + 0.25f * b);
                var node = nodeAt[current];
                if (node != null) node.star.rectTransform.localScale = Vector3.one * (1f + 0.08f * b);
                for (int i = 0; i < comet.Length; i++)
                {
                    float a = (t * style.mapCometSpeed - i * 9f) * Mathf.Deg2Rad;
                    comet[i].rectTransform.anchoredPosition = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * style.mapCometOrbit;
                    comet[i].color = UIKit.WithAlpha(i == 0 ? Color.white : gold, (1f - i / (float)comet.Length) * 0.9f);
                }
            }

            // Shimmers travel up the lit part of the path in view.
            int litEnd = Mathf.Min(current >= 0 ? current : count - 1, to);
            float start = pathLength[from], span = pathLength[Mathf.Max(from, litEnd)] - start;
            for (int i = 0; i < shimmers.Length; i++)
            {
                var s = shimmers[i];
                if (span < style.mapNodeSpacing) { s.enabled = false; continue; }
                s.enabled = true;
                float d = Mathf.Repeat(t * style.mapShimmerSpeed + i * span / shimmers.Length, span);
                s.rectTransform.anchoredPosition = PointAt(start + d, from, litEnd);
                float edge = Mathf.Clamp01(Mathf.Min(d, span - d) / style.mapNodeSpacing);
                s.color = UIKit.WithAlpha(gold, 0.55f * edge);
            }

            // Completed stars twinkle a little.
            for (int i = from; i <= to; i++)
            {
                var n = nodeAt[i];
                if (n == null || i >= unlocked || i == current) continue;
                float w = Mathf.Sin(t * 1.3f + i * 2.17f);
                n.star.color = UIKit.WithAlpha(gold, 0.85f + 0.15f * w);
            }
        }

        // Point at a distance along the path, between levels a and b.
        Vector2 PointAt(float distance, int a, int b)
        {
            int lo = a, hi = b;
            while (lo < hi)
            {
                int m = (lo + hi + 1) >> 1;
                if (pathLength[m] <= distance) lo = m; else hi = m - 1;
            }
            if (lo >= b) return positions[b];
            float seg = pathLength[lo + 1] - pathLength[lo];
            return Vector2.Lerp(positions[lo], positions[lo + 1], seg > 0f ? (distance - pathLength[lo]) / seg : 0f);
        }

        // ---------- pooling ----------

        void Bind(int from, int to)
        {
            bool progressChanged = unlocked != boundUnlocked;
            // Release what left the range.
            for (int i = boundFrom; i <= boundTo && i < count; i++)
                if ((i < from || i > to || progressChanged) && nodeAt[i] != null)
                {
                    nodeAt[i].root.gameObject.SetActive(false);
                    nodePool.Push(nodeAt[i]);
                    nodeAt[i] = null;
                }
            int chunk = Mathf.Max(1, style.mapChunk);
            int cFrom = from / chunk, cTo = to / chunk;
            for (int c = 0; c < pieceAt.Length; c++)
                if (pieceAt[c] != null && (c < cFrom || c > cTo || progressChanged))
                {
                    pieceAt[c].gameObject.SetActive(false);
                    piecePool.Push(pieceAt[c]);
                    pieceAt[c] = null;
                }

            for (int i = from; i <= to; i++)
                if (nodeAt[i] == null) BindNode(nodePool.Count > 0 ? nodePool.Pop() : NewNode(), i);
            for (int c = cFrom; c <= cTo; c++)
                if (pieceAt[c] == null)
                {
                    var piece = piecePool.Count > 0 ? piecePool.Pop() : NewPiece();
                    piece.gameObject.SetActive(true);
                    piece.Set(c * chunk, Mathf.Min(count - 1, (c + 1) * chunk));
                    pieceAt[c] = piece;
                }
            boundFrom = from;
            boundTo = to;
            boundUnlocked = unlocked;
        }

        void BindNode(Node n, int i)
        {
            nodeAt[i] = n;
            n.index = i;
            n.root.gameObject.SetActive(true);
            n.root.anchoredPosition = positions[i];
            n.root.localRotation = Quaternion.identity;
            int number = i + 1;
            bool done = i < unlocked, isCurrent = i == current, locked = !done && !isCurrent;
            bool boss = Difficulty.IsBoss(number), hard = Difficulty.IsHard(number) && !boss;
            var gold = style.Gold;
            var tone = locked ? style.mapLockedColor : gold;

            float size = isCurrent ? style.mapCurrentSize : locked ? style.mapLockedSize : style.mapStarSize;
            if (boss && !locked) size *= 0.8f;
            n.star.sprite = locked ? Art.Circle : Art.Star;
            n.star.rectTransform.sizeDelta = Vector2.one * size;
            n.star.rectTransform.localScale = Vector3.one;
            n.star.color = isCurrent ? Color.Lerp(gold, style.text, 0.35f) : tone;

            n.ring.gameObject.SetActive(hard);
            if (hard) n.ring.color = UIKit.WithAlpha(tone, locked ? tone.a : 0.8f);

            n.medallion.gameObject.SetActive(boss);
            n.disc.gameObject.SetActive(boss);
            if (boss)
            {
                int sign = Zodiac.SignOf(number);
                if (n.glyphSign != sign)
                {
                    n.glyphSign = sign;
                    float r = style.mapBossRadius;
                    var disc = style.starStyle ? style.starStyle.backgroundCenter : new Color(0.07f, 0.09f, 0.16f);
                    n.medallion.Clear();
                    n.medallion.Dial(r, style.Zodiac.dialWidth, Color.white, style.Zodiac.dialTick);
                    n.medallion.Disc(new Vector2(0f, r + 4f), 15f, UIKit.WithAlpha(disc, 1f));
                    n.medallion.Circle(new Vector2(0f, r + 4f), 15f, 1f, Color.white, 32);
                    n.medallion.Glyph(ZodiacGlyphs.Sign(sign), new Vector2(0f, r + 4f), 8.5f, 1.4f, Color.white);
                }
                n.medallion.color = UIKit.WithAlpha(tone, locked ? tone.a : 0.9f);
            }

            // Rating pips under reached levels: gold for stars earned, dim for the rest.
            int stars = done ? SaveService.GetStars(i) : 0;
            for (int p = 0; p < n.pips.Length; p++)
            {
                n.pips[p].gameObject.SetActive(done);
                n.pips[p].color = p < stars ? gold : UIKit.WithAlpha(style.text, style.homeDimStarAlpha);
            }
            float pipY = style.mapPipY - (boss ? style.mapBossRadius - style.mapStarSize * 0.5f : 0f);
            for (int p = 0; p < n.pips.Length; p++)
                n.pips[p].rectTransform.anchoredPosition = new Vector2((p - 1) * style.mapPipGap, pipY);

            n.number.SetText("{0}", (float)number);
            n.number.color = UIKit.WithAlpha(style.text, locked ? style.labelAlpha * 0.5f : style.labelAlpha);
            float side = positions[i].x > 0f ? -1f : 1f; // toward the middle of the screen
            float offset = style.mapNumberX + (boss ? style.mapBossRadius - style.mapStarSize * 0.5f : 0f);
            n.number.rectTransform.anchoredPosition = new Vector2(side * offset, 0f);
        }

        sealed class Node
        {
            public int index, glyphSign = -1;
            public RectTransform root;
            public Image star, ring, disc;
            public VectorGraphic medallion;
            public readonly Image[] pips = new Image[3];
            public TextMeshProUGUI number;
        }

        Node NewNode()
        {
            var mid = new Vector2(0.5f, 0.5f);
            var n = new Node();
            float hit = Mathf.Max(style.mapCurrentSize, style.mapBossRadius * 2f) + 40f;
            var hitArea = UIKit.Image(nodeLayer, "Level", null, Color.clear, new Vector2(0.5f, 0f), Vector2.zero, Vector2.one * hit);
            hitArea.raycastTarget = true;
            n.root = hitArea.rectTransform;
            var disc = style.starStyle ? style.starStyle.backgroundCenter : new Color(0.07f, 0.09f, 0.16f);
            n.disc = UIKit.Image(n.root, "Disc", Art.Circle, UIKit.WithAlpha(disc, 0.9f), mid, Vector2.zero,
                Vector2.one * style.mapBossRadius * 2f);
            n.medallion = UIKit.Vector(n.root, "Medallion", mid, Vector2.zero, style.mapBossRadius * 2f + 40f);
            n.ring = UIKit.Image(n.root, "Ring", Art.Ring, Color.white, mid, Vector2.zero, Vector2.one * style.mapHardRing * 2f);
            n.star = UIKit.Image(n.root, "Star", Art.Star, Color.white, mid, Vector2.zero, Vector2.one * style.mapStarSize);
            for (int p = 0; p < n.pips.Length; p++)
                n.pips[p] = UIKit.Image(n.root, "Pip", Art.Star, Color.white, mid, Vector2.zero, Vector2.one * style.mapPipSize);
            n.number = UIKit.Text(n.root, "Number", UIKit.BodyFont(style), style.mapNumberSize, style.text, mid, Vector2.zero,
                new Vector2(90f, 40f), 1f, FontStyles.Bold);
            var button = hitArea.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnNodeTap(n));
            hitArea.gameObject.AddComponent<PressFeedback>().style = style;
            return n;
        }

        PathPiece NewPiece()
        {
            var rt = UIKit.Stretch(pathLayer, "Path Piece");
            rt.pivot = new Vector2(0.5f, 0f); // local space = content space (x from the center, y from the bottom)
            rt.gameObject.AddComponent<CanvasRenderer>();
            var piece = rt.gameObject.AddComponent<PathPiece>();
            piece.raycastTarget = false;
            piece.map = this;
            return piece;
        }

        void OnNodeTap(Node n)
        {
            if (revealing) return;
            int i = n.index;
            if (i > unlocked) { UIKit.Wiggle(n.star.rectTransform, style); return; }
            OpenPopup(i);
        }

        void RefreshProgress()
        {
            unlocked = Mathf.Clamp(SaveService.HighestUnlocked, 0, count);
            current = unlocked < count ? unlocked : -1;
        }

        // ---------- scrolling ----------

        float ScrollFor(float y)
        {
            float max = Mathf.Max(0f, contentHeight - viewport.rect.height);
            return Mathf.Clamp(y - viewport.rect.height * 0.45f, 0f, max);
        }

        void ScrollTo(int index, bool instant, Action done) => ScrollToY(positions[Mathf.Clamp(index, 0, count - 1)].y, instant, done);

        void ScrollToY(float y, bool instant, Action done)
        {
            if (count == 0) return;
            Canvas.ForceUpdateCanvases(); // the viewport's size must be known
            scrollTween?.Kill();
            scroll.StopMovement();
            float to = ScrollFor(y);
            if (instant)
            {
                content.anchoredPosition = new Vector2(0f, -to);
                done?.Invoke();
                return;
            }
            // Glide in from a little below (or from where the map was left).
            float from = -content.anchoredPosition.y;
            if (Mathf.Abs(from - to) > viewport.rect.height * 2f || from <= 0f)
                from = Mathf.Max(0f, to - viewport.rect.height * style.mapScrollLead);
            content.anchoredPosition = new Vector2(0f, -from);
            scrollTween = Tween.Run(content, style.mapScrollTime, Ease.OutCubic,
                k => content.anchoredPosition = new Vector2(0f, -Mathf.Lerp(from, to, k)), done);
        }

        void PlaceFigures()
        {
            // A figure lines up with its house when the house is in the middle of the view.
            float f = style.mapParallax, c = viewHeight * 0.5f;
            for (int s = 0; s < houses; s++)
                if (house[s] != null)
                    house[s].figure.rectTransform.anchoredPosition =
                        new Vector2(figurePos[s].x, f * figurePos[s].y + (1f - f) * c);
        }

        // ---------- houses ----------

        void SetHouseLit(int s, bool lit)
        {
            var h = house[s];
            if (h == null) return;
            h.group.alpha = lit ? 1f : style.mapHeaderLockedAlpha;
            h.text.alpha = 1f;
            h.frame.Show();
            h.figure.SetProgress(1f);
            h.figure.color = UIKit.WithAlpha(style.Zodiac.gold, lit ? style.mapFigureAlpha : style.mapFigureLockedAlpha);
        }

        // The new house: glide to it, its constellation draws itself, the header frame draws and its glyph lights up,
        // then the view settles on the current level and its card opens.
        void PlayReveal(int s, int focus)
        {
            var h = house[s];
            if (h == null) { ScrollTo(focus, false, null); return; }
            revealing = true;
            h.frame.Hide();
            h.text.alpha = 0f;
            h.group.alpha = 1f;
            h.figure.SetProgress(0f);
            var gold = style.Zodiac.gold;
            ScrollToY(headerY[s] + style.mapFigureSize * 0.3f, false, () =>
            {
                float total = style.mapRevealTime;
                h.frame.PlayDraw(total * 0.5f);
                Tween.Run(h.figure, total, Ease.Linear, k =>
                {
                    h.figure.SetProgress(Ease.InOutSine(k));
                    // Brighter while drawing, then settles.
                    float glow = k < 0.8f ? 1f : 1f - (k - 0.8f) / 0.2f;
                    h.figure.color = UIKit.WithAlpha(gold, Mathf.Lerp(style.mapFigureAlpha, style.mapFigureAlpha * 3f, glow));
                    h.text.alpha = Ease.OutCubic(Mathf.Clamp01((k - 0.35f) / 0.3f));
                }, () =>
                {
                    h.frame.Celebrate();
                    revealing = false;
                    if (!OnScreen) return; // the player left the map meanwhile
                    ScrollTo(focus, false, () => { if (OnScreen) OpenPopup(focus); });
                });
            });
        }

        // ---------- popup ----------

        void OpenPopup(int i)
        {
            if (i < 0 || i >= count) return;
            popupIndex = i;
            int number = i + 1;
            int sign = Zodiac.SignOf(number);
            popupFrame.SetGlyph(ZodiacGlyphs.Sign(sign), style.Zodiac.glyphStroke);
            popupLevel.SetText("Level {0}", (float)number);
            popupName.text = style.LevelTitle(number);
            popupTag.text = Difficulty.IsBoss(number) ? "BOSS" : Difficulty.IsHard(number) ? "HARD" : "";
            bool done = i < unlocked;
            int stars = done ? SaveService.GetStars(i) : 0;
            for (int s = 0; s < popupStars.Length; s++)
                popupStars[s].color = s < stars ? style.Gold : UIKit.WithAlpha(style.text, style.homeDimStarAlpha);
            popupPlayLabel.text = done ? "Replay" : "Play";
            UIKit.Open(popupGroup, popupCard, style);
        }

        void ClosePopup(bool instant)
        {
            if (!PopupOpen) return;
            if (instant) popupGroup.gameObject.SetActive(false);
            else UIKit.Close(popupGroup, popupCard, style);
        }

        void PlayPopupLevel()
        {
            int i = popupIndex;
            ClosePopup(true);
            if (router && i >= 0) router.PlayLevel(i);
        }

        void Update()
        {
            if (!PopupOpen) return;
            float k = 0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / style.playGlowPeriod);
            popupGlow.color = UIKit.WithAlpha(style.Gold, 0.1f + 0.18f * k);
        }

        // ---------- building ----------

        void Build()
        {
            var canvas = UIKit.MakeCanvas(transform, "Map Canvas", 13, true);
            Group = canvas.gameObject.AddComponent<CanvasGroup>();
            Content = UIKit.Stretch(canvas.transform, "Safe Area");
            Content.gameObject.AddComponent<SafeArea>();
            var safe = Content;
            BuildScroll(safe);
            BuildTopBar(safe);
            BuildPopup(UIKit.MakeCanvas(transform, "Map Popup Canvas", 19, true).transform);
        }

        void BuildTopBar(RectTransform safe)
        {
            float m = style.edgeMargin, y = -(m + style.topButton * 0.5f);
            var back = UIKit.GlassButton(safe, "Back", style, null, new Vector2(0f, 1f), new Vector2(m + style.topButton * 0.5f, y),
                style.topButton, () => { if (router) router.Back(); });
            var chevron = UIKit.Vector(back.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, style.iconSize);
            float c = style.iconSize * 0.24f;
            chevron.Polyline(new[] { new Vector2(c * 0.5f, c), new Vector2(-c * 0.5f, 0f), new Vector2(c * 0.5f, -c) }, 3f,
                UIKit.WithAlpha(style.text, 0.9f));

            CoinCounter.CreatePill(safe, style, new Vector2(1f, 1f), new Vector2(-(m + style.coinPillWidth * 0.5f), y),
                () => { if (shop) shop.Open(); });

            var title = UIKit.Text(safe, "Title", UIKit.TitleFont(style), style.mapTitleSize, style.Gold, new Vector2(0.5f, 1f),
                new Vector2(0f, y), new Vector2(UIKit.ReferenceResolution.x - 2f * (m + style.coinPillWidth), style.mapTitleSize * 1.5f), 4f);
            title.text = style.mapTitle;
            title.enableAutoSizing = true;
            title.fontSizeMin = style.mapTitleSize * 0.6f;
            title.fontSizeMax = style.mapTitleSize;
            UIKit.Glow(title, UIKit.WithAlpha(style.Gold, style.titleGlow), 0.3f);
        }

        void BuildScroll(RectTransform safe)
        {
            viewport = UIKit.Stretch(safe, "Chart");
            viewport.offsetMax = new Vector2(0f, -style.mapTopBar);
            var hit = viewport.gameObject.AddComponent<Image>(); // drags anywhere scroll the chart
            hit.color = Color.clear;
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, 60);

            far = BottomLayer(viewport, "Constellations");
            content = BottomLayer(viewport, "Path");
            pathLayer = UIKit.Stretch(content, "Lines");
            shimmerLayer = UIKit.Stretch(content, "Shimmer");
            headerLayer = UIKit.Stretch(content, "Houses");
            nodeLayer = UIKit.Stretch(content, "Levels");

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 40f;

            BuildHouses();
            BuildMarkers();
        }

        RectTransform BottomLayer(Transform parent, string name)
        {
            var rt = UIKit.Rect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0f, contentHeight));
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, contentHeight);
            return rt;
        }

        void BuildHouses()
        {
            var theme = style.Zodiac;
            var bottom = new Vector2(0.5f, 0f);
            var chart = style.starStyle ? style.starStyle.backgroundEdge : new Color(0.04f, 0.05f, 0.09f);
            for (int s = 0; s < houses; s++)
            {
                var h = new House();
                // The constellation, far behind.
                var fig = UIKit.Rect(far, Zodiac.Names[s], bottom, figurePos[s], Vector2.one * style.mapFigureSize);
                fig.gameObject.AddComponent<CanvasRenderer>();
                h.figure = fig.gameObject.AddComponent<ConstellationFigure>();
                h.figure.raycastTarget = false;
                h.figure.lineWidth = 1.5f;
                h.figure.starRadius = 7f;
                h.figure.SetFigure(theme.constellations ? theme.constellations.Get(s) : null);

                // The header cartouche where the house begins.
                h.root = UIKit.Rect(headerLayer, "House " + Zodiac.Names[s], bottom, new Vector2(0f, headerY[s]), style.mapHeaderSize);
                h.group = h.root.gameObject.AddComponent<CanvasGroup>();
                h.group.blocksRaycasts = false;
                h.frame = Cartouche.Create(h.root, style, new Cartouche.Options
                {
                    medallionRadius = style.mapHeaderMedallion,
                    crescent = false,
                    sideMotif = true,
                    fill = UIKit.WithAlpha(chart, style.modalCardAlpha), // the path passes behind it
                });
                h.frame.SetGlyph(ZodiacGlyphs.Sign(s), theme.glyphStroke * 0.8f);
                var text = UIKit.Stretch(h.root, "Text");
                h.text = text.gameObject.AddComponent<CanvasGroup>();
                var mid = new Vector2(0.5f, 0.5f);
                var name = UIKit.Text(text, "Name", UIKit.TitleFont(style), style.mapHeaderTextSize, theme.gold, mid,
                    new Vector2(0f, style.mapHeaderSize.y * 0.06f), new Vector2(style.mapHeaderSize.x - 60f, 50f), 6f);
                name.text = $"{Zodiac.UpperNames[s]}  —  {Zodiac.Epithets[s]}";
                name.enableAutoSizing = true;
                name.fontSizeMin = style.mapHeaderTextSize * 0.6f;
                name.fontSizeMax = style.mapHeaderTextSize;
                h.range = UIKit.Text(text, "Levels", UIKit.BodyFont(style), style.mapHeaderSubSize,
                    UIKit.WithAlpha(style.text, style.labelAlpha), mid, new Vector2(0f, -style.mapHeaderSize.y * 0.24f),
                    new Vector2(style.mapHeaderSize.x - 60f, 36f), 4f);
                int first = Zodiac.FirstLevel(s), last = Mathf.Min(Zodiac.FirstLevel(s + 1) - 1, count);
                h.range.text = $"Levels {first} – {last}";
                house[s] = h;
            }

            // Above the last level: the rest of the sky is still to come.
            if (count > 0 && count < Zodiac.TotalLevels)
            {
                var soon = UIKit.Text(headerLayer, "Coming Soon", UIKit.TitleFont(style), style.mapHeaderSubSize,
                    UIKit.WithAlpha(style.Gold, 0.6f), bottom, new Vector2(0f, positions[count - 1].y + style.mapTopPad * 0.45f),
                    new Vector2(800f, 40f), 6f);
                soon.text = style.mapComingSoon;
            }
        }

        void BuildMarkers()
        {
            var bottom = new Vector2(0.5f, 0f);
            halo = UIKit.Image(pathLayer, "Halo", Art.SoftCircle, Color.clear, bottom, Vector2.zero, Vector2.one * style.mapHaloSize);
            shimmers = new Image[Mathf.Max(0, style.mapShimmers)];
            for (int i = 0; i < shimmers.Length; i++)
                shimmers[i] = UIKit.Image(shimmerLayer, "Shimmer", Art.SoftCircle, Color.clear, bottom, Vector2.zero,
                    Vector2.one * style.mapShimmerSize);
            var cometLayer = UIKit.Stretch(content, "Comet");
            for (int i = 0; i < comet.Length; i++)
                comet[i] = UIKit.Image(cometLayer, i == 0 ? "Comet" : "Tail", i == 0 ? UIKit.Sparkle : Art.SoftCircle, Color.clear,
                    bottom, Vector2.zero, Vector2.one * (i == 0 ? 22f : 14f - i * 2f));
        }

        void BuildPopup(Transform canvas)
        {
            (popupGroup, popupCard) = UIKit.Modal(canvas, "Level Card", style, style.mapPopupSize, () => ClosePopup(false));
            popupFrame = popupCard.GetComponent<Cartouche>();
            UIKit.CloseButton(popupCard, style, () => ClosePopup(false));
            var mid = new Vector2(0.5f, 0.5f);
            float h = style.mapPopupSize.y;
            popupLevel = UIKit.Text(popupCard, "Level", UIKit.TitleFont(style), style.levelLabelSize,
                UIKit.WithAlpha(style.text, style.labelAlpha), mid, new Vector2(0f, h * 0.5f - style.panelTitleY + 10f),
                new Vector2(600f, 44f), style.levelLabelSpacing, FontStyles.SmallCaps);
            popupName = UIKit.Text(popupCard, "Name", UIKit.TitleFont(style), 64f, style.Gold, mid,
                new Vector2(0f, h * 0.5f - style.panelTitleY - 80f), new Vector2(style.mapPopupSize.x - 80f, 90f), 3f);
            popupName.enableAutoSizing = true;
            popupName.fontSizeMin = 36f;
            popupName.fontSizeMax = 64f;
            UIKit.Glow(popupName, UIKit.WithAlpha(style.Gold, 0.45f), 0.3f);
            popupTag = UIKit.Text(popupCard, "Tag", UIKit.BodyFont(style), style.badgeTextSize, style.Gold, mid,
                new Vector2(0f, h * 0.5f - style.panelTitleY - 150f), new Vector2(300f, 40f), 10f, FontStyles.Bold);
            for (int i = 0; i < popupStars.Length; i++)
            {
                float size = i == 1 ? 84f : 66f;
                popupStars[i] = UIKit.Image(popupCard, "Star", Art.Star, Color.white, mid,
                    new Vector2((i - 1) * 100f, -30f + (i == 1 ? 10f : 0f)), Vector2.one * size);
            }
            var playPos = new Vector2(0f, -h * 0.5f + 130f);
            popupGlow = UIKit.Image(popupCard, "Play Glow", Art.SoftCircle, Color.clear, mid, playPos, style.mapPlayButtonSize * 1.4f);
            var play = UIKit.PillButton(popupCard, "Play", style, "Play", mid, playPos, style.mapPlayButtonSize, PlayPopupLevel,
                out popupPlayLabel, style.Gold);
            float corners = UIKit.PillCorners(style.mapPlayButtonSize.y);
            play.GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            play.GetComponent<Image>().color = UIKit.WithAlpha(style.Gold, style.switchOnFillAlpha);
            play.transform.Find("Outline").GetComponent<Image>().pixelsPerUnitMultiplier = corners;
            popupPlayLabel.font = UIKit.TitleFont(style);
            popupPlayLabel.fontSize = style.playTextSize;
            popupPlayLabel.color = style.Gold;
        }
    }

    /// <summary>
    /// A stretch of the map's path (one pooled piece per few levels): glowing gold lines up to the current level,
    /// faint dashes beyond it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PathPiece : MaskableGraphic
    {
        public LevelMap map;
        int from, to;
        readonly List<Vector2> pts = new();

        /// <summary>Segments from level <paramref name="a"/> to level <paramref name="b"/> (indices).</summary>
        public void Set(int a, int b)
        {
            from = a;
            to = b;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!map || !map.style || to <= from) return;
            var s = map.style;
            float px = VectorMesh.Pixel(this);
            var gold = s.Gold;
            int lit = Mathf.Min(to, map.Unlocked); // segments ending at or before the current level are lit

            if (lit > from)
            {
                pts.Clear();
                for (int i = from; i <= lit; i++) pts.Add(map.Position(i));
                float half = s.mapGlowWidth * 0.5f;
                VectorMesh.Stroke(vh, pts, 0, pts.Count, false, half, half, UIKit.WithAlpha(gold, 0.16f));
                VectorMesh.Stroke(vh, pts, 0, pts.Count, false, s.mapLineWidth, px, UIKit.WithAlpha(gold, 0.85f));
            }

            // Dashes toward locked levels.
            Color32 dim = UIKit.WithAlpha(s.mapLockedColor, s.mapLockedLineAlpha);
            float period = s.mapDash + s.mapDashGap;
            for (int i = Mathf.Max(from, lit); i < to; i++)
            {
                Vector2 a = map.Position(i), b = map.Position(i + 1);
                float len = Vector2.Distance(a, b);
                if (len <= 0f) continue;
                var dir = (b - a) / len;
                for (float d = s.mapDashGap * 0.5f; d < len; d += period)
                {
                    pts.Clear();
                    pts.Add(a + dir * d);
                    pts.Add(a + dir * Mathf.Min(len, d + s.mapDash));
                    VectorMesh.Stroke(vh, pts, 0, 2, false, s.mapLineWidth * 0.8f, px, dim);
                }
            }
        }
    }
}
