using System.Collections.Generic;
using System.Linq;

namespace OneLine
{
    /// <summary>Checks that a level has a solution (a path visiting every node once) and explains why not.</summary>
    public static class LevelSolver
    {
        public class Report
        {
            public bool Solvable;
            /// <summary>
            /// Distinct solutions: a path and its reverse draw the same line, so they count once.
            /// Stops at the cap passed to Analyze (see CountCapped).
            /// </summary>
            public int SolutionCount;
            /// <summary>True if counting stopped at the cap — the real number is at least SolutionCount.</summary>
            public bool CountCapped;
            /// <summary>One valid path (node indices), empty if unsolvable.</summary>
            public List<int> ExampleSolution = new();
            /// <summary>Structural reasons a Hamiltonian path cannot exist. Empty when solvable.</summary>
            public List<string> Problems = new();

            public string CountText => CountCapped ? $"{SolutionCount}+" : SolutionCount.ToString();

            public override string ToString() => Solvable
                ? $"PASS — {CountText} distinct solution(s), e.g. {string.Join("-", ExampleSolution)}"
                : $"FAIL — {string.Join("; ", Problems)}";
        }

        public const int DefaultCountCap = 1000;

        /// <summary>
        /// Number of distinct solutions (a path and its reverse count once), stopping at <paramref name="cap"/>.
        /// 0 means the level is unsolvable.
        /// </summary>
        public static int CountDistinctSolutions(LevelData level, int cap = DefaultCountCap)
        {
            if (level.nodes.Length == 0) return 0;
            if (level.startNode >= 0) return CountSolutions(Adjacency(level), cap, level.startNode);
            // Without a fixed start every path is found once from each end.
            return CountSolutions(Adjacency(level), 2 * cap) / 2;
        }

        /// <summary>
        /// Fast solution count on an adjacency list (each path counted once per direction), stopping at
        /// <paramref name="cap"/>. Prunes a branch as soon as an unvisited node has no way in, or two
        /// unvisited nodes each have only one way in — both would have to be the path's last node.
        /// </summary>
        public static int CountSolutions(List<int>[] adj, int cap, int startNode = -1)
        {
            int n = adj.Length;
            if (n == 0) return 0;
            var linked = new bool[n, n];
            var free = new int[n]; // unvisited neighbors
            for (int i = 0; i < n; i++)
            {
                free[i] = adj[i].Count;
                foreach (int j in adj[i]) linked[i, j] = true;
            }
            var seen = new bool[n];
            int count = 0;

            void Visit(int v) { seen[v] = true; foreach (int u in adj[v]) free[u]--; }
            void Unvisit(int v) { seen[v] = false; foreach (int u in adj[v]) free[u]++; }

            void Dfs(int v, int depth)
            {
                if (count >= cap) return;
                if (depth == n) { count++; return; }
                int ends = 0;
                for (int u = 0; u < n; u++)
                {
                    if (seen[u]) continue;
                    int links = free[u] + (linked[u, v] ? 1 : 0);
                    if (links == 0) return;
                    if (links == 1 && ++ends > 1) return;
                }
                foreach (int u in adj[v])
                {
                    if (seen[u]) continue;
                    Visit(u);
                    Dfs(u, depth + 1);
                    Unvisit(u);
                }
            }

            for (int s = 0; s < n; s++)
            {
                if (startNode >= 0 && s != startNode) continue;
                Visit(s);
                Dfs(s, 1);
                Unvisit(s);
            }
            return count;
        }

        public static bool IsSolvable(LevelData level) =>
            IsValidSolution(level, level.solution) || CountDistinctSolutions(level, 1) > 0;

        /// <summary>True if <paramref name="path"/> visits every node once along existing edges.</summary>
        public static bool IsValidSolution(LevelData level, IList<int> path)
        {
            int n = level.nodes.Length;
            if (path == null || n == 0 || path.Count != n) return false;
            if (level.startNode >= 0 && path[0] != level.startNode && path[n - 1] != level.startNode) return false;

            var edges = new HashSet<long>();
            foreach (var e in level.edges) edges.Add(Key(e.a, e.b));
            var seen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int v = path[i];
                if (v < 0 || v >= n || seen[v]) return false;
                seen[v] = true;
                if (i > 0 && !edges.Contains(Key(path[i - 1], v))) return false;
            }
            return true;
        }

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        public static Report Analyze(LevelData level, int cap = DefaultCountCap)
        {
            var report = new Report();
            report.SolutionCount = CountDistinctSolutions(level, cap);
            report.CountCapped = report.SolutionCount >= cap;
            report.Solvable = report.SolutionCount > 0;
            if (!report.Solvable)
                report.Problems = Diagnose(level);
            else if (IsValidSolution(level, level.solution))
                report.ExampleSolution.AddRange(level.solution);
            else
                Search(level, 1, report.ExampleSolution);
            return report;
        }

        // ---------- search ----------

        static List<int>[] Adjacency(LevelData level)
        {
            int n = level.nodes.Length;
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>();
            foreach (var e in level.edges)
            {
                if (e.a < 0 || e.b < 0 || e.a >= n || e.b >= n || e.a == e.b) continue;
                if (!adj[e.a].Contains(e.b)) adj[e.a].Add(e.b);
                if (!adj[e.b].Contains(e.a)) adj[e.b].Add(e.a);
            }
            return adj;
        }

        static int Search(LevelData level, int cap, List<int> example)
        {
            int n = level.nodes.Length;
            if (n == 0) return 0;
            var adj = Adjacency(level);
            var visited = new bool[n];
            var path = new List<int>(n);
            int count = 0;

            void Dfs(int v)
            {
                if (count >= cap) return;
                if (path.Count == n)
                {
                    if (count == 0 && example != null) example.AddRange(path);
                    count++;
                    return;
                }
                foreach (int u in adj[v])
                {
                    if (visited[u]) continue;
                    visited[u] = true; path.Add(u);
                    Dfs(u);
                    visited[u] = false; path.RemoveAt(path.Count - 1);
                }
            }

            for (int s = 0; s < n; s++)
            {
                if (level.startNode >= 0 && s != level.startNode) continue;
                visited[s] = true; path.Add(s);
                Dfs(s);
                visited[s] = false; path.Clear();
            }
            return count;
        }

        // ---------- diagnosis ----------

        // Necessary conditions for a Hamiltonian path. Any violation is a definite reason it can't exist.
        static List<string> Diagnose(LevelData level)
        {
            var problems = new List<string>();
            int n = level.nodes.Length;
            if (n == 0) { problems.Add("level has no nodes"); return problems; }
            var adj = Adjacency(level);

            var isolated = Enumerable.Range(0, n).Where(i => adj[i].Count == 0).ToList();
            if (n > 1 && isolated.Count > 0)
                problems.Add($"node(s) {Join(isolated)} have no edges");

            // A path has only two ends, so at most two nodes may have a single edge.
            var leaves = Enumerable.Range(0, n).Where(i => adj[i].Count == 1).ToList();
            if (leaves.Count > 2)
                problems.Add($"{leaves.Count} nodes have only one edge ({Join(leaves)}) — a path has only 2 ends");
            if (level.startNode >= 0 && leaves.Count == 2 && !leaves.Contains(level.startNode))
                problems.Add($"nodes {Join(leaves)} must both be path ends, but the fixed start is node {level.startNode}");

            int parts = Components(adj, -1);
            if (parts > 1) problems.Add($"graph is split into {parts} disconnected parts");

            // Removing a node on a path leaves at most 2 pieces; 3+ means some part is unreachable once it's visited.
            for (int v = 0; v < n && parts == 1; v++)
            {
                int split = Components(adj, v);
                if (split > 2)
                    problems.Add($"node {v} is a cut vertex: once visited it strands {split} separate parts (max 2 allowed)");
            }

            if (problems.Count == 0)
                problems.Add("no single structural cause; exhaustive search found no path" +
                             (level.startNode >= 0 ? $" from start node {level.startNode}" : ""));
            return problems;
        }

        // Connected components, ignoring node `removed` (-1 = none).
        static int Components(List<int>[] adj, int removed)
        {
            int n = adj.Length, parts = 0;
            var seen = new bool[n];
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (s == removed || seen[s]) continue;
                parts++;
                seen[s] = true; stack.Push(s);
                while (stack.Count > 0)
                    foreach (int u in adj[stack.Pop()])
                        if (u != removed && !seen[u]) { seen[u] = true; stack.Push(u); }
            }
            return parts;
        }

        static string Join(IEnumerable<int> xs) => string.Join(", ", xs);
    }
}
