using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OneLine
{
    /// <summary>
    /// Builds a level from LevelData, turns pointer drags into a path through the graph,
    /// validates every step and decides win / fail.
    /// Rules: move only along edges to unvisited nodes; touching an already-visited node,
    /// getting stuck, or lifting the finger early resets the level instantly.
    /// Visuals of the drawn path live in WandTrail / ProgressDots / GameFeedback, driven by the events below.
    /// </summary>
    public class PathManager : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("World units between grid positions in LevelData.")]
        public float spacing = 1.6f;
        public float nodeSize = 0.55f;
        [Tooltip("How close (world units) the finger must get to a node to snap to it.")]
        public float snapRadius = 0.45f;
        [Tooltip("Empty world units kept around the level when framing the camera.")]
        public float framePadding = 1.2f;
        [Tooltip("Fraction of the screen height kept free at the top for the HUD (set at runtime by HUD).")]
        [Range(0f, 0.6f)] public float topReserveFraction = 0.2f;
        [Tooltip("Fraction of the screen height kept free at the bottom for the progress dots (set at runtime).")]
        [Range(0f, 0.4f)] public float bottomReserveFraction = 0f;

        [Header("Style")]
        [Tooltip("Star look: visit palette, idle colors, motion timings.")]
        public StarStyle style;
        [Tooltip("Color visited nodes by visit order (StarStyle palette). Off = one flat color (pathColor), " +
                 "used when a line skin is equipped.")]
        public bool usePalette = true;

        [Header("Palette (tier / cosmetic colors)")]
        public Color background = new(0.10f, 0.11f, 0.15f);
        public Color edgeColor = new(0.24f, 0.26f, 0.33f);
        public Color nodeColor = new(0.40f, 0.43f, 0.52f);
        public Color startNodeColor = new(0.95f, 0.80f, 0.35f);
        public Color pathColor = new(0.31f, 0.82f, 0.77f);

        [Header("References")]
        public GameFeedback feedback;

        [Tooltip("Turn off to ignore the real mouse/touch, e.g. while an AutoPlayer drives the game.")]
        public bool acceptPlayerInput = true;
        [Tooltip("Set by ScreenRouter while the game screen isn't showing: the board ignores all input.")]
        public bool suspended;

        /// <summary>Raised when the player releases after visiting every node.</summary>
        public event Action Completed;
        /// <summary>Raised when the path is broken and the level resets.</summary>
        public event Action Failed;
        /// <summary>Raised after a level's nodes and edges have been created.</summary>
        public event Action BoardBuilt;
        /// <summary>Raised when a node joins the path: the node, its 0-based visit index, its visit color.</summary>
        public event Action<Node, int, Color> NodeVisited;
        /// <summary>Raised when the drawn path is wiped (fail, new level).</summary>
        public event Action PathCleared;
        /// <summary>Raised on every press on the board (not on UI), real or simulated — LevelTimer starts on the first.</summary>
        public event Action BoardTouched;
        /// <summary>
        /// Visual only: the finger's point on the board (board-local, like Node.BoardPoint) on every frame a line is being
        /// drawn, real or simulated (AutoPlayer). WandTrail's drag dust follows it.
        /// </summary>
        public event Action<Vector3> Dragged;

        Camera cam;
        LevelData level;
        readonly List<Node> nodes = new();
        readonly List<Node> path = new();
        readonly List<LineRenderer> edgeLines = new();
        Vector2 boardSize;
        float framedAspect;
        float cameraZoom = 1f;
        Vector2 cameraOffset;
        Transform levelRoot;
        LineRenderer fingerLine; // from the last visited node to the finger
        bool dragging;
        bool busy; // true after a win until the next level loads; input is ignored

        public LevelData Level => level;
        public IReadOnlyList<Node> Nodes => nodes;
        public IReadOnlyList<Node> Path => path;
        public IReadOnlyList<LineRenderer> EdgeLines => edgeLines;
        public LineRenderer PathLine => fingerLine;
        /// <summary>Everything on the board (nodes, edges, drawn path) lives under this; BoardView tilts it.</summary>
        public Transform BoardPivot { get; private set; }
        public bool IsDrawing => dragging;
        /// <summary>Set by LevelTimer while time is up: the board ignores all input.</summary>
        public bool Locked { get; private set; }

        /// <summary>Locks / unlocks the board (time up). Locking drops a line being drawn, without counting a mistake.</summary>
        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (locked && dragging) ResetPath();
        }

        /// <summary>Clears the drawn path without counting it as a mistake (Restart button).</summary>
        public void ResetPath()
        {
            if (busy || !level) return;
            dragging = false;
            ClearPath();
        }

        // Simulated input (bots, tests) — same code path as a real finger, via the node's screen position.
        public void SimulateDown(Vector3 world)
        {
            if (busy || Locked || !level) return;
            BoardTouched?.Invoke();
            OnPointerDown(cam.WorldToScreenPoint(world));
        }
        public void SimulateDrag(Vector3 world) { if (!busy && dragging) OnDrag(cam.WorldToScreenPoint(world)); }
        public void SimulateUp() { if (!busy && dragging) OnPointerUp(); }

        void Awake()
        {
            cam = Camera.main;
            cam.backgroundColor = background;
            cam.clearFlags = CameraClearFlags.SolidColor;
            BoardPivot = new GameObject("BoardPivot").transform;
            BoardPivot.SetParent(transform, false);
            fingerLine = CreateLine("FingerLine", BoardPivot, style ? style.trailWidth : 0.06f, pathColor, 10);
        }

        /// <summary>Color of the <paramref name="visitIndex"/>-th node joining the path.</summary>
        public Color VisitColor(int visitIndex) =>
            usePalette && style ? style.ForVisit(visitIndex, nodes.Count) : pathColor;

        Color IdleNodeColor => usePalette && style ? style.idleNode : nodeColor;
        Color IdleEdgeColor => style ? style.idleEdge : edgeColor;

        /// <summary>
        /// Recolors the board — the current level right away, and every level loaded later.
        /// Colors only: doesn't touch the graph, the path or any rule.
        /// </summary>
        public void ApplyTheme(Color bg, Color edge, Color node, Color path)
        {
            background = bg;
            edgeColor = edge;
            nodeColor = node;
            pathColor = path;
            cam.backgroundColor = bg;

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                int visit = this.path.IndexOf(n);
                n.SetPalette(n.Index == level.startNode ? startNodeColor : IdleNodeColor,
                    visit >= 0 ? VisitColor(visit) : (Color?)null);
            }
            foreach (var line in edgeLines)
                line.startColor = line.endColor = IdleEdgeColor;
        }

        // ---------- Level building ----------

        public void Load(LevelData data)
        {
            level = data;

            if (levelRoot) Destroy(levelRoot.gameObject);
            levelRoot = new GameObject(level.name).transform;
            levelRoot.SetParent(BoardPivot, false);
            nodes.Clear();
            path.Clear();
            edgeLines.Clear();
            dragging = false;
            busy = false;
            Locked = false;

            // Center the level on the origin.
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (var p in level.nodes) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 center = (min + max) * 0.5f;

            int levelSeed = StarStyle.Seed(level.name); // star personalities: same every time this level is played
            float heroScale = style ? style.heroScale : 1.25f;
            int hero = HeroStar(level);
            for (int i = 0; i < level.nodes.Length; i++)
            {
                var go = new GameObject($"Node {i}", typeof(SpriteRenderer));
                go.transform.SetParent(levelRoot, false);
                go.transform.localPosition = (level.nodes[i] - center) * spacing;
                var node = go.AddComponent<Node>();
                bool isStart = i == level.startNode;
                node.Init(i, i == hero ? nodeSize * heroScale : nodeSize,
                    isStart ? startNodeColor : IdleNodeColor, 20, style, i == hero, levelSeed + i * 7919);
                nodes.Add(node);
            }

            // Connections not drawn yet: faint, thin, dashed.
            float edgeWidth = style ? style.idleEdgeWidth : 0.03f;
            float dashTiling = style ? 1f / style.dashPeriod : 4f;
            foreach (var e in level.edges)
            {
                Node a = nodes[e.a], b = nodes[e.b];
                if (!a.Neighbors.Contains(b)) a.Neighbors.Add(b);
                if (!b.Neighbors.Contains(a)) b.Neighbors.Add(a);
                var line = CreateLine($"Edge {e.a}-{e.b}", levelRoot, edgeWidth, IdleEdgeColor, 0);
                line.sharedMaterial = Art.DashedMaterial;
                line.textureMode = LineTextureMode.Tile;
                line.textureScale = new Vector2(dashTiling, 1f);
                line.numCapVertices = 0;
                line.positionCount = 2;
                line.SetPosition(0, a.BoardPoint);
                line.SetPosition(1, b.BoardPoint);
                edgeLines.Add(line);
            }

            boardSize = (max - min) * spacing;
            FrameCamera();
            ClearPath();
            BoardBuilt?.Invoke();
        }

        /// <summary>Reserves this fraction of the screen height at the top (for the HUD) and re-frames the board.</summary>
        public void SetTopReserve(float fraction)
        {
            topReserveFraction = Mathf.Clamp(fraction, 0f, 0.6f);
            if (level) FrameCamera();
        }

        /// <summary>Reserves this fraction of the screen height at the bottom (progress dots) and re-frames.</summary>
        public void SetBottomReserve(float fraction)
        {
            bottomReserveFraction = Mathf.Clamp(fraction, 0f, 0.4f);
            if (level) FrameCamera();
        }

        /// <summary>
        /// Short-lived camera effects (GameFeedback): zoom &gt; 1 punches in, offset shakes (world units).
        /// Applied on top of the normal framing, so they can never knock the board out of place.
        /// </summary>
        public void SetCameraEffects(float zoom, Vector2 offset)
        {
            cameraZoom = Mathf.Max(0.5f, zoom);
            cameraOffset = offset;
            if (level) FrameCamera();
        }

        // Fits the board, plus padding, between the top and bottom reserves, centered in that area.
        void FrameCamera()
        {
            float free = 1f - topReserveFraction - bottomReserveFraction; // share of the screen height for the board
            float height = boardSize.y + 2f * framePadding;
            float width = boardSize.x + 2f * framePadding;
            float size = Mathf.Max(height / (2f * free), width / (2f * cam.aspect));
            // Board center (world 0) ends up in the middle of the free area.
            float y = (topReserveFraction - bottomReserveFraction) * size;
            cam.orthographicSize = size / cameraZoom;
            cam.transform.position = new Vector3(cameraOffset.x, y + cameraOffset.y, cam.transform.position.z);
            framedAspect = cam.aspect;
        }

        // ---------- Input ----------

        void Update()
        {
            // The window / screen changed shape (rotation, resized Game view): fit the board again.
            if (level && !Mathf.Approximately(cam.aspect, framedAspect)) FrameCamera();

            var pointer = Pointer.current;
            if (busy || suspended || Locked || !level || !acceptPlayerInput || pointer == null) return;

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (!PointerOverUI())
                {
                    BoardTouched?.Invoke();
                    OnPointerDown(screen);
                }
            }
            else if (dragging && pointer.press.isPressed) OnDrag(screen);
            if (dragging && pointer.press.wasReleasedThisFrame) OnPointerUp();
        }

        // A press on a UI button or panel (e.g. the shop) must not start a line on the board behind it.
        static bool PointerOverUI() => EventSystem.current && EventSystem.current.IsPointerOverGameObject();

        void OnPointerDown(Vector2 screen)
        {
            Node node = NearestNode(screen);
            if (!node) return;
            if (level.startNode >= 0 && node.Index != level.startNode) return;

            dragging = true;
            AddToPath(node);
            UpdateFingerLine(screen);
            foreach (var n in nodes) n.SetFingerOver(false); // a fresh press counts as entering the star
            UpdateFingerOverStars(screen);
            Dragged?.Invoke(BoardPointUnder(screen));
        }

        void OnDrag(Vector2 screen)
        {
            UpdateFingerOverStars(screen);
            Dragged?.Invoke(BoardPointUnder(screen));
            Node current = path[^1];
            Node hit = NearestNode(screen);

            if (hit && hit != current)
            {
                if (hit.Visited)
                {
                    Fail(); // crossed back over our own path
                    return;
                }
                if (current.IsNeighbor(hit))
                {
                    AddToPath(hit);
                    if (path.Count < nodes.Count && !hit.HasUnvisitedNeighbor())
                    {
                        Fail(); // dead end
                        return;
                    }
                }
                // Unvisited but not adjacent: ignore, the finger is just passing by.
            }
            UpdateFingerLine(screen);
        }

        void OnPointerUp()
        {
            dragging = false;
            foreach (var n in nodes) n.SetFingerOver(false);
            if (path.Count == nodes.Count) Win();
            else Fail(); // lifted on a non-final node
        }

        // Snapping works on where each node appears on screen, so it stays exact however the board is tilted.
        // The snap radius is snapRadius world units around the node, measured on screen.
        // If two nodes overlap on screen, the one closer to the camera wins.
        Node NearestNode(Vector2 screen)
        {
            Node best = null;
            float bestDist = float.MaxValue, bestDepth = float.MaxValue;
            Vector3 right = cam.transform.right * snapRadius;
            foreach (var n in nodes)
            {
                Vector3 p = cam.WorldToScreenPoint(n.Position);
                if (p.z < 0f) continue; // behind the camera
                float radius = Vector2.Distance(p, cam.WorldToScreenPoint(n.Position + right));
                float d = Vector2.Distance(screen, p);
                if (d > radius) continue;
                bool overlapping = best != null && Mathf.Abs(d - bestDist) < radius * 0.25f;
                if (overlapping ? p.z < bestDepth : d < bestDist)
                {
                    best = n;
                    bestDist = d;
                    bestDepth = p.z;
                }
            }
            return best;
        }

        // The constellation's main star (visual only): the start star, or on free-start levels the best-connected one.
        static int HeroStar(LevelData level)
        {
            if (level.startNode >= 0) return level.startNode;
            var degree = new int[level.nodes.Length];
            foreach (var e in level.edges) { degree[e.a]++; degree[e.b]++; }
            int best = 0;
            for (int i = 1; i < degree.Length; i++) if (degree[i] > degree[best]) best = i;
            return best;
        }

        // Visual only: tells each star whether the finger is over it, so it can pirouette as the finger passes.
        // Measured on screen like NearestNode, with its own (visual) radius; never affects snapping or the path.
        void UpdateFingerOverStars(Vector2 screen)
        {
            if (!style || !style.pirouetteEnabled) return;
            Vector3 right = cam.transform.right * style.pirouetteHoverRadius;
            foreach (var n in nodes)
            {
                Vector3 p = cam.WorldToScreenPoint(n.Position);
                float radius = Vector2.Distance(p, cam.WorldToScreenPoint(n.Position + right));
                n.SetFingerOver(p.z >= 0f && Vector2.Distance(screen, p) <= radius);
            }
        }

        // Where the pointer ray meets the (possibly tilted) board plane, in board-local space.
        Vector3 BoardPointUnder(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(BoardPivot.forward, BoardPivot.position);
            if (!plane.Raycast(ray, out float distance)) return path.Count > 0 ? path[^1].BoardPoint : Vector3.zero;
            return BoardPivot.InverseTransformPoint(ray.GetPoint(distance));
        }

        // ---------- Path ----------

        void AddToPath(Node node)
        {
            int visit = path.Count;
            var color = VisitColor(visit);
            node.Visit(color);
            path.Add(node);
            NodeVisited?.Invoke(node, visit, color);
            if (feedback) feedback.OnConnect(path.Count);
        }

        // A faint thread from the last node to the finger, in the color the next node will get.
        void UpdateFingerLine(Vector2 fingerScreen)
        {
            if (path.Count == 0 || path.Count == nodes.Count)
            {
                fingerLine.positionCount = 0;
                return;
            }
            var c = VisitColor(path.Count);
            c.a *= 0.45f;
            fingerLine.startColor = fingerLine.endColor = c;
            fingerLine.positionCount = 2;
            fingerLine.SetPosition(0, path[^1].BoardPoint);
            fingerLine.SetPosition(1, BoardPointUnder(fingerScreen));
        }

        void ClearPath()
        {
            foreach (var n in path) n.ResetState();
            path.Clear();
            fingerLine.positionCount = 0;
            PathCleared?.Invoke();
        }

        // ---------- Win / fail ----------

        void Fail()
        {
            dragging = false;
            Debug.Log("LEVEL FAILED");
            if (feedback) feedback.OnFail();
            Failed?.Invoke();
            ClearPath();
        }

        void Win()
        {
            Debug.Log("LEVEL COMPLETE");
            busy = true; // stays locked until the next Load()
            fingerLine.positionCount = 0;
            if (feedback) feedback.OnWin(path[^1].Position, VisitColor(path.Count - 1));
            Completed?.Invoke();
        }

        // ---------- Helpers ----------

        // Lines use local space so they tilt with the board. Public so other visuals can make matching lines.
        public static LineRenderer CreateLine(string name, Transform parent, float width, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Art.SpriteMaterial;
            lr.useWorldSpace = false;
            lr.widthMultiplier = width;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = order;
            lr.positionCount = 0;
            return lr;
        }
    }
}
