using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneLine.EditorTools
{
    /// <summary>
    /// Generates levels that follow Difficulty's curve. Each level is built around a random
    /// solution path on a small grid (so it is solvable by construction), then extra "decoy"
    /// edges are added or removed until the measured difficulty is close to the target.
    /// Deterministic: level N always comes out the same.
    /// </summary>
    public static class LevelGenerator
    {
        public const string Folder = "Assets/Levels/Generated";
        const int MaxWidth = 5, MaxHeight = 7; // portrait phone layout

        static readonly Vector2Int[] Ortho = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        static readonly Vector2Int[] Diag = { new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };
        // Forward-only neighbor offsets, used to list candidate edges once.
        static readonly Vector2Int[] Forward = { new(1, 0), new(0, 1), new(1, 1), new(1, -1) };

        public static string AssetPath(int number) => $"{Folder}/Level_{number:000}.asset";

        [MenuItem("One Line/Generate 500 Levels")]
        public static void GenerateMenu()
        {
            Generate(1, Difficulty.TierCount * Difficulty.LevelsPerTier);
            OneLineSetup.BuildPackFromGenerated();
            TimeLimits.RecalculateMenu(); // new boards, new node counts / difficulty → new time limits
        }

        /// <summary>Generates levels [from, to] into Folder. Returns a short summary.</summary>
        public static string Generate(int from, int to)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels")) AssetDatabase.CreateFolder("Assets", "Levels");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Levels", "Generated");

            float worstMiss = 0;
            int worstLevel = 0, made = 0, offSpec = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int number = from; number <= to; number++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("One Line", $"Generating level {number}",
                            (number - from) / (float)(to - from + 1)))
                        break;

                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetPath(number));
                    bool isNew = !level;
                    if (isNew) level = ScriptableObject.CreateInstance<LevelData>();
                    if (!GenerateInto(level, number)) offSpec++;
                    if (!LevelSolver.IsValidSolution(level, level.solution))
                        throw new Exception($"Generator produced an invalid solution for level {number}");
                    if (isNew) AssetDatabase.CreateAsset(level, AssetPath(number));
                    else EditorUtility.SetDirty(level);

                    float miss = Mathf.Abs(level.difficulty - Difficulty.TargetBits(number));
                    if (miss > worstMiss) { worstMiss = miss; worstLevel = number; }
                    made++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            return $"generated {made} level(s); worst difficulty miss {worstMiss:0.00} bits (level {worstLevel}); " +
                   $"{offSpec} level(s) missed a limit (solutions, single edges, corner starts or decoy count)";
        }

        // One scored decoy layout. Lower Penalty is better.
        struct Score
        {
            public float Bits, Penalty;
            public int Solutions, SingleEdgeNodes, CornerStarts, Edges;
        }

        /// <summary>Fills <paramref name="level"/>. Returns false if no layout met every limit (best one is used).</summary>
        public static bool GenerateInto(LevelData level, int number)
        {
            int n = Difficulty.TargetNodes(number);
            float target = Difficulty.TargetBits(number);
            int maxSolutions = Difficulty.MaxSolutions(number);
            const int Samples = 800;
            // HARD / BOSS targets are harder to hit, so they get a longer search.
            bool spike = Difficulty.IsHard(number);
            int iterations = spike ? 400 : 250, layouts = spike ? 14 : 10;
            var rnd = new System.Random(number * 7919);

            // Layouts must stay within the limits: at most maxSolutions distinct solutions, at most one
            // node with a single edge (it acts as a visible start hint; more would give the answer away),
            // and no solution may start on an outer corner — "start in a corner and sweep" must never work.
            // Below 8 nodes no path can avoid the corners, so the warm-up levels skip that rule and may
            // keep one single-edge node. Every level needs enough decoys that it isn't a bare path.
            bool cornerRule = number > Difficulty.WarmUpLevels && n >= 8;
            int maxSingles = cornerRule ? 0 : 1;
            int minEdges = (n - 1) + Mathf.Max(2, n / 3);
            bool Valid(Score s) => s.Solutions <= maxSolutions && s.SingleEdgeNodes <= maxSingles
                                   && s.CornerStarts == 0 && s.Edges >= minEdges;

            Score Evaluate(List<(int a, int b)> edges, List<int> corners)
            {
                var adj = Adjacency(n, edges);
                int singles = adj.Count(a => a.Count < 2);
                int solutions = LevelSolver.CountSolutions(adj, 2 * maxSolutions + 2) / 2; // both directions counted
                int cornerStarts = cornerRule ? corners.Count(c => LevelSolver.CountSolutions(adj, 1, c) > 0) : 0;
                float bits = Difficulty.Measure(adj, Samples, rnd, -1, corners);
                return new Score
                {
                    Bits = bits,
                    Solutions = solutions,
                    SingleEdgeNodes = singles,
                    CornerStarts = cornerStarts,
                    Edges = edges.Count,
                    // The limits weigh far more than the difficulty target, so they are met first.
                    Penalty = Mathf.Abs(bits - target) + 0.5f * Mathf.Min(singles, 1) + 5f * Mathf.Max(0, singles - maxSingles)
                              + 5f * Mathf.Max(0, solutions - maxSolutions) + 5f * cornerStarts
                              + 5f * Mathf.Max(0, minEdges - edges.Count),
                };
            }

            (List<Vector2Int> cells, List<(int a, int b)> edges, Score score)? best = null;
            bool Better(Score s) => best == null
                || (Valid(s) && !Valid(best.Value.score))
                || (Valid(s) == Valid(best.Value.score) && Mathf.Abs(s.Bits - target) < Mathf.Abs(best.Value.score.Bits - target));
            bool Done() => best != null && Valid(best.Value.score) && Mathf.Abs(best.Value.score.Bits - target) < 0.2f;

            for (int attempt = 0, built = 0; attempt < 500 && built < layouts && !Done(); attempt++)
            {
                var (w, h) = BoxFor(n);
                var cells = RandomPath(n, w, h, rnd);
                if (cells == null) continue;
                // The planted path's ends are always valid starts, so they must not be corners.
                // (After many misses, take any path; the level is then reported as off-spec.)
                var corners = Difficulty.CornerNodes(cells.Select(c => (Vector2)c).ToList());
                if (cornerRule && attempt < 400 && (corners.Contains(0) || corners.Contains(n - 1))) continue;
                built++;

                // The random path is the planted solution; decoy edges are searched on top of it
                // (simulated annealing: add / swap / remove a decoy, sometimes accept a worse layout early on).
                var pathEdges = Enumerable.Range(0, n - 1).Select(i => (a: i, b: i + 1)).ToList();
                var decoys = new List<(int a, int b)>();
                var current = Evaluate(pathEdges, corners);
                float temperature = 1f;

                for (int iter = 0; iter < iterations && !Done(); iter++)
                {
                    var trial = new List<(int a, int b)>(decoys);
                    var options = Candidates(cells, pathEdges.Concat(trial).ToList());
                    double r = rnd.NextDouble();
                    if ((r < 0.55 || trial.Count == 0) && options.Count > 0)
                        trial.Add(options[rnd.Next(options.Count)]);
                    else if (r < 0.8 && trial.Count > 0 && options.Count > 0)
                    {
                        trial.RemoveAt(rnd.Next(trial.Count));
                        trial.Add(options[rnd.Next(options.Count)]);
                    }
                    else if (trial.Count > 0)
                        trial.RemoveAt(rnd.Next(trial.Count));
                    else
                        break;

                    var edges = pathEdges.Concat(trial).ToList();
                    var score = Evaluate(edges, corners);
                    float worse = score.Penalty - current.Penalty;
                    if (worse < 0 || rnd.NextDouble() < 0.3 * Math.Exp(-worse / Math.Max(temperature, 1e-3f)))
                    {
                        decoys = trial;
                        current = score;
                        if (Better(score)) best = (cells, edges, score);
                    }
                    temperature *= 0.985f;
                }
                if (Better(current)) best = (cells, pathEdges.Concat(decoys).ToList(), current);
            }

            if (best == null) throw new Exception($"Could not generate level {number} ({n} nodes)");
            Write(level, best.Value.cells, best.Value.edges, best.Value.score.Bits, rnd);
            return Valid(best.Value.score);
        }

        // Shuffles node indices so the stored order doesn't give the solution away, then fills the asset.
        static void Write(LevelData level, List<Vector2Int> cells, List<(int a, int b)> edges, float difficulty,
            System.Random rnd)
        {
            int n = cells.Count;
            var newIndex = Enumerable.Range(0, n).OrderBy(_ => rnd.Next()).ToArray();
            level.nodes = new Vector2[n];
            for (int i = 0; i < n; i++) level.nodes[newIndex[i]] = cells[i];
            level.edges = edges.Select(e => new LevelData.Edge(newIndex[e.a], newIndex[e.b])).ToArray();
            level.solution = Enumerable.Range(0, n).Select(i => newIndex[i]).ToArray();
            level.startNode = -1;
            level.difficulty = difficulty;
        }

        // Grid box close to 80% full and taller than wide.
        static (int w, int h) BoxFor(int n)
        {
            (int w, int h) best = (MaxWidth, Mathf.CeilToInt(n / (float)MaxWidth));
            float bestScore = float.MaxValue;
            for (int w = 3; w <= MaxWidth; w++) // >= 3 wide so there is an inside to start from
            for (int h = w; h <= MaxHeight; h++)
            {
                float density = n / (float)(w * h);
                if (density > 1f || density < 0.6f) continue;
                float score = Mathf.Abs(density - 0.8f) + Mathf.Abs(h / (float)w - 1.4f) * 0.3f;
                if (score < bestScore) { bestScore = score; best = (w, h); }
            }
            return best;
        }

        // Random self-avoiding walk of n cells inside w*h; may use diagonals, never two in the same cell.
        static List<Vector2Int> RandomPath(int n, int w, int h, System.Random rnd)
        {
            // Start away from the border when the box allows it, so the solution doesn't begin in a corner.
            var start = w >= 3 && h >= 3 ? new Vector2Int(1 + rnd.Next(w - 2), 1 + rnd.Next(h - 2))
                                         : new Vector2Int(rnd.Next(w), rnd.Next(h));
            var path = new List<Vector2Int> { start };
            var used = new HashSet<Vector2Int> { path[0] };
            var diagonalCells = new HashSet<Vector2Int>();
            int budget = 20000;

            bool Inside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < w && c.y < h;
            int FreeNeighbors(Vector2Int c) => Ortho.Count(d => Inside(c + d) && !used.Contains(c + d));

            bool Dfs()
            {
                if (--budget < 0) return false;
                if (path.Count == n) return true;
                var c = path[^1];
                var moves = Ortho.Select(d => (cell: c + d, diag: false)).ToList();
                if (rnd.NextDouble() < 0.25) moves.AddRange(Diag.Select(d => (cell: c + d, diag: true)));
                // Prefer cramped cells first (Warnsdorff), with noise for variety — keeps the walk compact.
                var ordered = moves.Where(m => Inside(m.cell) && !used.Contains(m.cell))
                    .Select(m => (m, key: FreeNeighbors(m.cell) + rnd.NextDouble() * 1.5))
                    .OrderBy(x => x.key).Select(x => x.m).ToList();

                foreach (var (cell, diag) in ordered)
                {
                    var diagCell = Vector2Int.Min(c, cell);
                    if (diag && !diagonalCells.Add(diagCell)) continue;
                    path.Add(cell);
                    used.Add(cell);
                    if (Dfs()) return true;
                    path.RemoveAt(path.Count - 1);
                    used.Remove(cell);
                    if (diag) diagonalCells.Remove(diagCell);
                }
                return false;
            }

            return Dfs() ? path : null;
        }

        // Unit-length edges not in the level yet; a diagonal only if its grid cell has no diagonal (no crossings).
        static List<(int a, int b)> Candidates(List<Vector2Int> cells, List<(int a, int b)> edges)
        {
            var index = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < cells.Count; i++) index[cells[i]] = i;
            var existing = new HashSet<(int, int)>(edges.Select(e => Sorted(e.a, e.b)));
            var diagonalCells = new HashSet<Vector2Int>(edges
                .Where(e => cells[e.a].x != cells[e.b].x && cells[e.a].y != cells[e.b].y)
                .Select(e => Vector2Int.Min(cells[e.a], cells[e.b])));

            var result = new List<(int a, int b)>();
            for (int i = 0; i < cells.Count; i++)
            {
                foreach (var d in Forward)
                {
                    if (!index.TryGetValue(cells[i] + d, out int j)) continue;
                    if (existing.Contains(Sorted(i, j))) continue;
                    if (d.x != 0 && d.y != 0 && diagonalCells.Contains(Vector2Int.Min(cells[i], cells[j]))) continue;
                    result.Add(Sorted(i, j));
                }
            }
            return result;
        }

        static List<int>[] Adjacency(int n, List<(int a, int b)> edges)
        {
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>();
            foreach (var (a, b) in edges) { adj[a].Add(b); adj[b].Add(a); }
            return adj;
        }

        static (int, int) Sorted(int a, int b) => a < b ? (a, b) : (b, a);
    }
}
