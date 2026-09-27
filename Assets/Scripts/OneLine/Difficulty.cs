using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Difficulty curve and measurement.
    /// Every 10 levels form a tier that is harder than the last; every 5th level is HARD,
    /// every 10th (the last of a tier) is a BOSS.
    /// </summary>
    public static class Difficulty
    {
        public const int LevelsPerTier = 10;
        public const int TierCount = 50; // 500 levels
        /// <summary>Difficulty (in bits) shown as 100%.</summary>
        public const float MaxBits = 7.5f;

        public static int TierIndex(int levelNumber) => (levelNumber - 1) / LevelsPerTier;
        public static bool IsHard(int levelNumber) => levelNumber % 5 == 0;
        public static bool IsBoss(int levelNumber) => levelNumber % LevelsPerTier == 0;

        static float TierProgress(int levelNumber) => Mathf.Clamp01(TierIndex(levelNumber) / (TierCount - 1f));
        static int StepInTier(int levelNumber) => (levelNumber - 1) % LevelsPerTier;

        /// <summary>Levels 1-4 are a short warm-up on small boards; real puzzles start at level 5.</summary>
        public const int WarmUpLevels = 4;

        /// <summary>
        /// Node count the generator aims for. Small boards are easy no matter how they're wired,
        /// so after the warm-up (6-8 nodes) boards start at ~11 nodes: ~15 by level 50, ~20 by 150, ~33 by 500.
        /// </summary>
        public static int TargetNodes(int levelNumber)
        {
            if (levelNumber <= WarmUpLevels) return 5 + (levelNumber + 1) / 2 + levelNumber / 4; // 6, 6, 7, 8
            return 10 + Mathf.RoundToInt(20f * Mathf.Pow(TierProgress(levelNumber), 0.6f))
                      + StepInTier(levelNumber) / 4
                      + (IsHard(levelNumber) ? 1 : 0);
        }

        /// <summary>
        /// Difficulty in bits the generator aims for. HARD adds a spike, BOSS a bigger one.
        /// Warm-up ≈ 0.5 bits (an intuitive player wins ~70%), level 5 ≈ 2 bits (25%),
        /// level 150 ≈ 3.6 bits (~8%), level 500 ≈ 6 bits (~1.5%).
        /// </summary>
        public static float TargetBits(int levelNumber)
        {
            if (levelNumber <= WarmUpLevels) return 0.4f + 0.1f * levelNumber;
            return 1.2f + 5f * Mathf.Pow(TierProgress(levelNumber), 0.6f)
                        + 0.05f * StepInTier(levelNumber)
                        + (IsBoss(levelNumber) ? 1.3f : IsHard(levelNumber) ? 0.8f : 0f);
        }

        /// <summary>Most distinct solutions a level may have — fewer means less room for lucky guesses.</summary>
        public static int MaxSolutions(int levelNumber) =>
            IsBoss(levelNumber) ? 3 : IsHard(levelNumber) ? 4 : 5;

        public static int Percent(float bits) => Mathf.Clamp(Mathf.RoundToInt(100f * bits / MaxBits), 1, 100);

        /// <summary>
        /// -log2 of the chance that an intuitive player solves the level on the first try.
        /// The model: usually start on a node with the fewest edges, then always move to the neighbor
        /// with the fewest ways out (Warnsdorff's rule), breaking ties randomly. That simple rule solves
        /// easy levels almost always, so a high score means the level has real traps.
        /// 1 bit = a coin flip, 5 bits ≈ 1 in 32.
        /// </summary>
        public static float Measure(LevelData level, int samples = 1000, int seed = 1)
        {
            int n = level.nodes.Length;
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>();
            foreach (var e in level.edges)
            {
                if (e.a < 0 || e.b < 0 || e.a >= n || e.b >= n || e.a == e.b) continue;
                adj[e.a].Add(e.b);
                adj[e.b].Add(e.a);
            }
            return Measure(adj, samples, new System.Random(seed), level.startNode, CornerNodes(level.nodes));
        }

        /// <param name="corners">Outer corner nodes (see CornerNodes) — players love starting there.</param>
        public static float Measure(List<int>[] adj, int samples, System.Random rnd, int startNode = -1,
            IList<int> corners = null)
        {
            int n = adj.Length;
            if (n == 0) return 0;

            int minDegree = int.MaxValue;
            foreach (var a in adj) minDegree = Mathf.Min(minDegree, a.Count);
            var lowStarts = new List<int>();
            for (int i = 0; i < n; i++) if (adj[i].Count == minDegree) lowStarts.Add(i);

            var seen = new bool[n];
            var best = new List<int>(8);
            int successes = 0;

            for (int s = 0; s < samples; s++)
            {
                System.Array.Clear(seen, 0, n);
                // Start: a node with a single edge is an obvious end, so always start there if one exists.
                // Otherwise a corner 40% of the time, a fewest-edges node 30%, anywhere 30%.
                double pick = rnd.NextDouble();
                int v = startNode >= 0 ? startNode
                    : minDegree <= 1 ? lowStarts[rnd.Next(lowStarts.Count)]
                    : pick < 0.4 && corners != null && corners.Count > 0 ? corners[rnd.Next(corners.Count)]
                    : pick < 0.7 ? lowStarts[rnd.Next(lowStarts.Count)]
                    : rnd.Next(n);
                seen[v] = true;
                int count = 1;
                while (count < n)
                {
                    best.Clear();
                    int bestWaysOut = int.MaxValue;
                    foreach (int u in adj[v])
                    {
                        if (seen[u]) continue;
                        int waysOut = 0;
                        foreach (int x in adj[u]) if (!seen[x]) waysOut++;
                        if (waysOut < bestWaysOut) { bestWaysOut = waysOut; best.Clear(); }
                        if (waysOut == bestWaysOut) best.Add(u);
                    }
                    if (best.Count == 0) break;
                    v = best[rnd.Next(best.Count)];
                    seen[v] = true;
                    count++;
                }
                if (count == n) successes++;
            }
            float p = (successes + 0.5f) / (samples + 1f);
            return -Mathf.Log(p, 2f);
        }

        /// <summary>
        /// Indices of the nodes that form the outer corners of the level's shape (convex hull vertices;
        /// nodes lying in the middle of a straight outer edge don't count).
        /// </summary>
        public static List<int> CornerNodes(IList<Vector2> positions)
        {
            int n = positions.Count;
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => positions[a].x != positions[b].x
                ? positions[a].x.CompareTo(positions[b].x)
                : positions[a].y.CompareTo(positions[b].y));
            if (n < 3) return order;

            float Cross(int o, int a, int b) =>
                (positions[a].x - positions[o].x) * (positions[b].y - positions[o].y) -
                (positions[a].y - positions[o].y) * (positions[b].x - positions[o].x);

            // Andrew's monotone chain; "<= 0" drops collinear points so only real corners remain.
            var hull = new List<int>(2 * n);
            for (int pass = 0; pass < 2; pass++)
            {
                int floor = hull.Count;
                for (int k = 0; k < n; k++)
                {
                    int i = pass == 0 ? order[k] : order[n - 1 - k];
                    while (hull.Count >= floor + 2 && Cross(hull[^2], hull[^1], i) <= 0) hull.RemoveAt(hull.Count - 1);
                    hull.Add(i);
                }
                hull.RemoveAt(hull.Count - 1);
            }
            return hull;
        }
    }
}
