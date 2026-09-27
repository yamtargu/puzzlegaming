using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OneLine.EditorTools
{
    /// <summary>
    /// One Line/Recalculate Time Limits: writes every level's time limit into its LevelData from the TimerSettings
    /// formula (HARD levels from firstTimedLevel on; 0 = untimed for the rest), skipping hand-tuned ones
    /// (manualTimeLimit). Prints the table of timed levels with the local average solve time (recorded on this machine
    /// while playing, via SaveService) for tuning, and checks the design's expected values.
    /// </summary>
    public static class TimeLimits
    {
        public const string SettingsPath = "Assets/Settings/TimerSettings.asset";
        const string PackPath = "Assets/Levels/LevelPack.asset";

        // The design's sanity check: level number → expected seconds.
        static readonly Dictionary<int, float> Expected = new()
        {
            { 15, 45f }, { 20, 60f }, { 25, 50f }, { 30, 65f }, { 50, 70f }, { 75, 60f }, { 100, 80f }, { 125, 70f }, { 150, 85f },
        };

        /// <summary>The timer settings asset, created with the defaults the first time.</summary>
        public static TimerSettings LoadOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TimerSettings>(SettingsPath);
            if (settings) return settings;
            settings = ScriptableObject.CreateInstance<TimerSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("One Line/Recalculate Time Limits")]
        public static void RecalculateMenu()
        {
            var (report, mismatches) = Recalculate();
            if (mismatches > 0) Debug.LogWarning(report);
            else Debug.Log(report);
        }

        /// <returns>The report and how many expected values didn't match.</returns>
        public static (string report, int mismatches) Recalculate()
        {
            var settings = LoadOrCreateSettings();
            var pack = AssetDatabase.LoadAssetAtPath<LevelPack>(PackPath);
            if (!pack) return ("Recalculate Time Limits: no LevelPack at " + PackPath, 1);

            var sb = new StringBuilder();
            sb.AppendLine("Time limits  (base + perNode*nodes + perBit*bits, x1.2 HARD / x1.4 BOSS, rounded up to 5 s, clamped)");
            sb.AppendLine("level  kind  nodes   bits   limit   avg solve (local)   expected");
            int timed = 0, changed = 0, manual = 0, mismatches = 0;
            for (int i = 0; i < pack.Count; i++)
            {
                var level = pack[i];
                if (!level) continue;
                int number = i + 1;
                if (!level.manualTimeLimit)
                {
                    float limit = settings.IsTimedNumber(number) ? settings.ComputeLimit(number, level) : 0f;
                    if (!Mathf.Approximately(limit, level.timeLimit))
                    {
                        level.timeLimit = limit;
                        EditorUtility.SetDirty(level);
                        changed++;
                    }
                }
                else manual++;

                float playing = settings.LimitFor(number, level);
                if (playing <= 0f) continue;
                timed++;
                float avg = SaveService.AverageSolveTime(i, out int wins);
                string kind = Difficulty.IsBoss(number) ? "BOSS" : "HARD";
                string expected = "";
                if (Expected.TryGetValue(number, out float want))
                {
                    bool ok = Mathf.Approximately(want, playing);
                    if (!ok) mismatches++;
                    expected = ok ? $"{want:0}s ok" : $"{want:0}s MISMATCH";
                }
                sb.AppendLine($"{number,5}  {kind}  {level.nodes.Length,5}  {level.difficulty,5:0.00}  {playing,5:0}s" +
                              $"{(level.manualTimeLimit ? " (manual)" : "        ")}  " +
                              $"{(wins > 0 ? $"{avg,6:0.0}s ({wins} wins)" : "      —         ")}   {expected}");
            }
            foreach (var kv in Expected)
                if (kv.Key > pack.Count) { mismatches++; sb.AppendLine($"expected level {kv.Key} is missing from the pack"); }
            AssetDatabase.SaveAssets();
            sb.Insert(0, $"Recalculate Time Limits: {timed} timed level(s), {changed} updated, {manual} hand-tuned skipped, " +
                         $"{mismatches} mismatch(es) with the expected values.\n");
            return (sb.ToString(), mismatches);
        }
    }
}
