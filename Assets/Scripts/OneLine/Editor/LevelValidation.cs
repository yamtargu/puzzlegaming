using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneLine.EditorTools
{
    /// <summary>
    /// Level checks. Hard requirement (build fails, console error): every LevelData must be solvable.
    /// Soft design check (warning only): the number of distinct solutions must follow the difficulty curve —
    /// forgiving warm-up levels (3+ solutions), then at most 3 solutions from level 5 on.
    /// </summary>
    public class LevelValidation : AssetPostprocessor, IPreprocessBuildWithReport
    {
        const string PackPath = "Assets/Levels/LevelPack.asset";
        public const int FirstCurveLevel = 5;
        public const int MinWarmUpSolutions = 3;
        public const int MaxCurveSolutions = 3;

        [MenuItem("One Line/Validate All Levels")]
        public static void ValidateAllMenu()
        {
            var (table, failed, warned) = ValidateAll();
            if (failed > 0) Debug.LogError(table);
            else if (warned > 0) Debug.LogWarning(table);
            else Debug.Log(table);
        }

        /// <summary>Warning text if a level's solution count breaks the curve; null if it fits.</summary>
        public static string CurveWarning(int levelNumber, LevelSolver.Report report)
        {
            if (!report.Solvable) return null;
            if (levelNumber < FirstCurveLevel && report.SolutionCount < MinWarmUpSolutions)
                return $"warm-up level has only {report.CountText} solution(s), want {MinWarmUpSolutions}+";
            if (levelNumber >= FirstCurveLevel && report.SolutionCount > MaxCurveSolutions)
                return $"{report.CountText} solutions, want at most {MaxCurveSolutions} from level {FirstCurveLevel} on";
            return null;
        }

        /// <summary>
        /// Validates the LevelPack in play order (with level numbers and curve warnings), then every other
        /// LevelData in the project. Returns the console table, the FAIL count and the WARN count.
        /// </summary>
        public static (string table, int failed, int warned) ValidateAll()
        {
            var pack = AssetDatabase.LoadAssetAtPath<LevelPack>(PackPath);
            var packLevels = pack ? pack.levels.Where(l => l).ToList() : new List<LevelData>();
            var others = AssetDatabase.FindAssets("t:" + nameof(LevelData))
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<LevelData>(p))
                .Where(l => l && !packLevels.Contains(l))
                .ToList();

            var sb = new StringBuilder($"Level validation — {packLevels.Count} level(s) in the pack, {others.Count} other(s)\n");
            int failed = 0, warned = 0;
            var counts = new List<(int number, int solutions)>();

            string Line(string label, LevelData level, LevelSolver.Report report, string warning)
            {
                string status = !report.Solvable ? "FAIL" : warning != null ? "WARN" : "PASS";
                string detail = report.Solvable ? $"solutions: {report.CountText}" : string.Join("; ", report.Problems);
                return $"{status}  {label,-5} {level.name,-16} {level.nodes.Length,2} nodes {level.edges.Length,2} edges  " +
                       detail + (warning != null ? $"  — {warning}" : "");
            }

            for (int i = 0; i < packLevels.Count; i++)
            {
                int number = i + 1;
                var report = LevelSolver.Analyze(packLevels[i]);
                string warning = CurveWarning(number, report);
                if (!report.Solvable) failed++;
                else if (warning != null) warned++;
                counts.Add((number, report.SolutionCount));
                sb.AppendLine(Line($"L{number}", packLevels[i], report, warning));
            }
            foreach (var level in others)
            {
                var report = LevelSolver.Analyze(level);
                if (!report.Solvable) failed++;
                sb.AppendLine(Line("-", level, report, null));
            }

            // Trend: average distinct solutions per block of 25 levels (warm-up shown on its own).
            if (counts.Count > 0)
            {
                sb.AppendLine("Solution-count trend (average distinct solutions):");
                var warmUp = counts.Where(c => c.number < FirstCurveLevel).ToList();
                if (warmUp.Count > 0) sb.AppendLine($"  levels 1-{FirstCurveLevel - 1}: {warmUp.Average(c => c.solutions):0.0}");
                foreach (var block in counts.Where(c => c.number >= FirstCurveLevel).GroupBy(c => (c.number - 1) / 25))
                    sb.AppendLine($"  levels {Mathf.Max(FirstCurveLevel, block.Key * 25 + 1)}-{block.Max(c => c.number)}: " +
                                  $"{block.Average(c => c.solutions):0.0} (max {block.Max(c => c.solutions)})");
            }
            sb.Append(failed > 0 ? $"{failed} level(s) FAIL. " : "All levels solvable. ");
            sb.Append(warned > 0 ? $"{warned} level(s) off the solution-count curve (warning only)." : "Curve OK.");
            return (sb.ToString(), failed, warned);
        }

        // Runs whenever assets are created, imported or saved.
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.EndsWith(".asset")) continue;
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (!level) continue;
                if (level.nodes.Length == 0)
                {
                    Debug.LogWarning($"{level.name}: level has no nodes yet.", level);
                    continue;
                }
                var report = LevelSolver.Analyze(level);
                if (!report.Solvable)
                {
                    Debug.LogError($"UNSOLVABLE LEVEL {path}: {string.Join("; ", report.Problems)}", level);
                    continue;
                }
                var pack = AssetDatabase.LoadAssetAtPath<LevelPack>(PackPath);
                int index = pack ? pack.levels.IndexOf(level) : -1;
                string warning = index >= 0 ? CurveWarning(index + 1, report) : null;
                if (warning != null) Debug.LogWarning($"Level {index + 1} ({level.name}): {warning}", level);
            }
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var (table, failed, _) = ValidateAll();
            if (failed > 0)
                throw new BuildFailedException("Unsolvable levels found — fix them before building.\n" + table);
        }
    }
}
