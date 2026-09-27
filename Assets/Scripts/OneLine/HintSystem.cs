using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Hint: shows the first steps of the solution as a faint golden ghost line that fades away, with a golden
    /// sonar on the starting star. Each extra hint on the same level reveals two more steps.
    /// Uses the level's stored solution (or asks LevelSolver for one).
    /// </summary>
    public class HintSystem : MonoBehaviour
    {
        public PathManager pathManager;
        public StarStyle style;
        public int stepsPerHint = 2;
        public float showTime = 2.6f;

        int revealed;
        GameObject ghost;

        void OnEnable() { if (pathManager) pathManager.BoardBuilt += ResetHints; }
        void OnDisable() { if (pathManager) pathManager.BoardBuilt -= ResetHints; }

        void ResetHints()
        {
            revealed = 0;
            if (ghost) Destroy(ghost);
        }

        public void Show()
        {
            var level = pathManager.Level;
            if (!level || !style) return;
            IList<int> solution = LevelSolver.IsValidSolution(level, level.solution)
                ? level.solution
                : LevelSolver.Analyze(level, 1).ExampleSolution;
            if (solution.Count == 0) return;

            revealed = Mathf.Min(revealed + stepsPerHint, solution.Count - 1);
            if (ghost) Destroy(ghost);
            var nodes = pathManager.Nodes;
            var gold = style.finalColor;

            var line = PathManager.CreateLine("Hint", pathManager.BoardPivot, style.trailWidth * 1.4f, gold, 8);
            ghost = line.gameObject;
            line.sharedMaterial = Art.DashedMaterial;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(1f / style.dashPeriod, 1f);
            line.positionCount = revealed + 1;
            for (int i = 0; i <= revealed; i++) line.SetPosition(i, nodes[solution[i]].BoardPoint);

            var start = nodes[solution[0]].transform;
            SonarRing.Spawn(start, gold, pathManager.nodeSize, style);
            Tween.Run(line, showTime, Ease.Linear, k =>
            {
                // Fade in fast, hold, fade out.
                float a = k < 0.1f ? k / 0.1f : k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
                var c = gold;
                c.a = 0.75f * a;
                line.startColor = line.endColor = c;
            }, () => { if (line) Destroy(line.gameObject); });
        }
    }
}
