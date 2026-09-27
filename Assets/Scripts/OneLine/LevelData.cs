using System;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// One puzzle: node positions (in grid units) and the edges the line may travel along.
    /// Goal: visit every node exactly once in a single stroke (a Hamiltonian path).
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "One Line/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Serializable]
        public struct Edge
        {
            public int a;
            public int b;

            public Edge(int a, int b)
            {
                this.a = a;
                this.b = b;
            }
        }

        public Vector2[] nodes = Array.Empty<Vector2>();
        public Edge[] edges = Array.Empty<Edge>();

        [Tooltip("-1 = the player may start on any node.")]
        public int startNode = -1;

        [Tooltip("A known solution (node indices in order). Filled by the generator; " +
                 "lets validation skip the brute-force search. Optional for hand-made levels.")]
        public int[] solution = Array.Empty<int>();

        [Tooltip("Measured difficulty in bits (see Difficulty.Measure). 0 = not measured yet.")]
        public float difficulty;

        [Tooltip("Time limit in seconds; 0 = untimed. Filled by One Line/Recalculate Time Limits (TimerSettings).")]
        public float timeLimit;

        [Tooltip("Hand-tuned: Recalculate Time Limits leaves timeLimit alone (0 here = always untimed).")]
        public bool manualTimeLimit;

        [ContextMenu("Validate (count solutions)")]
        void LogSolutionCount()
        {
            var report = LevelSolver.Analyze(this);
            if (report.Solvable) Debug.Log($"{name}: {report}", this);
            else Debug.LogError($"{name}: {report}", this);
        }

        void OnValidate()
        {
            foreach (var e in edges)
            {
                if (e.a < 0 || e.b < 0 || e.a >= nodes.Length || e.b >= nodes.Length || e.a == e.b)
                    Debug.LogWarning($"{name}: invalid edge {e.a}-{e.b}", this);
            }
            if (startNode >= nodes.Length)
                Debug.LogWarning($"{name}: startNode {startNode} is out of range", this);
        }
    }
}
