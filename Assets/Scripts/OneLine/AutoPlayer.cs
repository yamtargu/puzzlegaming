using System.Collections;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Demo bot: solves each level on screen by dragging along a known solution at human-ish speed.
    /// Real mouse/touch input is ignored while it runs. Add it in Play mode to watch levels being played.
    /// </summary>
    public class AutoPlayer : MonoBehaviour
    {
        public LevelManager levelManager;
        public PathManager pathManager;
        [Tooltip("Stops after this level number has been played. 0 = never stop.")]
        public int stopAfterLevel = 20;
        [Tooltip("Pause after a level appears, before the first touch.")]
        public float thinkTime = 0.7f;
        public float secondsPerEdge = 0.22f;
        [Tooltip("With a win screen: how long to look at it before pressing Next.")]
        public float nextDelay = 2f;

        void Awake()
        {
            if (!levelManager) levelManager = GetComponent<LevelManager>();
            if (!pathManager) pathManager = GetComponent<PathManager>();
        }

        void OnEnable()
        {
            levelManager.LevelLoaded += OnLevelLoaded;
            pathManager.acceptPlayerInput = false;
        }

        void OnDisable()
        {
            levelManager.LevelLoaded -= OnLevelLoaded;
            pathManager.acceptPlayerInput = true;
        }

        // The current level is already on screen when the bot is added.
        void Start() => StartCoroutine(Play());

        void OnLevelLoaded(int _)
        {
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        IEnumerator Play()
        {
            if (stopAfterLevel > 0 && levelManager.CurrentNumber > stopAfterLevel)
            {
                Debug.Log($"AutoPlayer: finished level {stopAfterLevel}, handing control back.");
                enabled = false;
                yield break;
            }
            yield return new WaitForSeconds(thinkTime);

            var level = pathManager.Level;
            var solution = LevelSolver.IsValidSolution(level, level.solution)
                ? level.solution
                : LevelSolver.Analyze(level, 1).ExampleSolution.ToArray();
            var nodes = pathManager.Nodes;

            pathManager.SimulateDown(nodes[solution[0]].Position);
            for (int i = 1; i < solution.Length; i++)
            {
                // Read positions every frame: the board may be tilting while the bot drags.
                Node from = nodes[solution[i - 1]], to = nodes[solution[i]];
                for (float t = 0; t < secondsPerEdge; t += Time.deltaTime)
                {
                    pathManager.SimulateDrag(Vector3.Lerp(from.Position, to.Position, Mathf.SmoothStep(0, 1, t / secondsPerEdge)));
                    yield return null;
                }
                pathManager.SimulateDrag(to.Position);
            }
            yield return new WaitForSeconds(0.15f);
            pathManager.SimulateUp();

            // With a win screen there is no auto advance: press "Next" like a player would (except after the last
            // level the bot was asked to play, so the win screen stays up).
            if (!levelManager.autoAdvance && (stopAfterLevel <= 0 || levelManager.CurrentNumber < stopAfterLevel))
            {
                int finished = levelManager.CurrentNumber;
                yield return new WaitForSeconds(nextDelay);
                if (levelManager.CurrentNumber == finished) levelManager.LoadNext();
            }
        }
    }
}
