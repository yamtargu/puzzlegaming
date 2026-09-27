using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    public enum TimerState { Off, Ready, Running, TimeUp, Done }

    /// <summary>
    /// The time limit of timed levels (TimerSettings: HARD levels from level 15 on). Pure logic — it knows nothing
    /// about UI; HourglassView, BoostBar, TimeUpPanel and the HUD subscribe to its events.
    /// Rules: one timer per LEVEL, not per attempt (a broken path or the Restart button doesn't refill it; leaving to
    /// the map and coming back resumes it). It starts on the first touch of the board, so reading the puzzle is free.
    /// It pauses while the app is in the background or any pause source (panels, other screens — registered by
    /// ScreenRouter) is active, and stops on the win. It runs on unscaled time, so hit-stops don't steal seconds.
    /// Time up locks the board until Saturn's Gift extends it or the level restarts with a full timer.
    /// Also measures the solve time (first touch → win, pauses excluded) of every level for local tuning data.
    /// </summary>
    public class LevelTimer : MonoBehaviour
    {
        public LevelManager levelManager;
        public TimerSettings settings;
        [Tooltip("Longest step per frame (s): a hitch or coming back from the background never eats a chunk of time.")]
        public float maxFrameStep = 0.25f;

        /// <summary>The clock started (first touch of the board).</summary>
        public event Action Started;
        /// <summary>Every running frame: time left as a share of the limit (0..1).</summary>
        public event Action<float> Tick;
        public event Action TimeUp;
        /// <summary>Saturn's Gift: seconds added.</summary>
        public event Action<float> Extended;
        public event Action SlowStarted, SlowEnded;
        /// <summary>A level was set up (or restarted with a full timer). Argument: timed?</summary>
        public event Action<bool> Configured;
        /// <summary>The timer paused / resumed (panels, other screens, app in background).</summary>
        public event Action<bool> PausedChanged;
        /// <summary>The level was won. Argument: stars (3 on untimed levels).</summary>
        public event Action<int> Won;

        public TimerState State { get; private set; }
        public bool IsTimed => limit > 0f;
        /// <summary>The level's time limit in seconds (0 = untimed).</summary>
        public float Limit => limit;
        public float Remaining => remaining;
        /// <summary>Time left as a share of the limit, 0..1 (can't overfill after an extension).</summary>
        public float Remaining01 => limit > 0f ? Mathf.Clamp01(remaining / limit) : 1f;
        public bool IsPaused { get; private set; }
        public bool IsSlowed => slowLeft > 0f;
        /// <summary>Lunar Stillness left, 0..1.</summary>
        public float SlowLeft01 => settings && settings.lunarSeconds > 0f ? Mathf.Clamp01(slowLeft / settings.lunarSeconds) : 0f;
        /// <summary>How fast the timer runs right now (Lunar Stillness slows it; gameplay is never slowed).</summary>
        public float Speed => IsSlowed ? settings.lunarSpeed : 1f;
        public int SaturnUsed { get; private set; }
        public int LunarUsed { get; private set; }
        /// <summary>This is the first timed level the player meets: show the "stars wait for no one" hint.</summary>
        public bool ShowIntro { get; private set; }
        /// <summary>Stars the level would get if won now: by the share of the time left, 3 on untimed levels.</summary>
        public int StarsNow => !IsTimed ? 3 : settings.Stars(granted > 0f ? remaining / granted : 0f);

        /// <summary>Saturn's Gift can be used now (timed, running / ready / time up, under the per-level cap).</summary>
        public bool CanExtend => IsTimed && SaturnUsed < settings.maxSaturnPerLevel && (State == TimerState.TimeUp ||
                                 !IsPaused && (State == TimerState.Ready || State == TimerState.Running));
        /// <summary>Lunar Stillness can start now (timed, running or ready, not already active, under the cap).</summary>
        public bool CanSlow => IsTimed && !IsPaused && !IsSlowed && LunarUsed < settings.maxLunarPerLevel &&
                               (State == TimerState.Ready || State == TimerState.Running);

        float limit, remaining, granted, slowLeft, activeSeconds;
        bool touched, appPaused;
        readonly List<Func<bool>> pauseSources = new();

        // The unfinished session of the last level, so leaving to the map and coming back doesn't refill the timer.
        struct Session { public bool valid, started; public int index, saturn, lunar; public float remaining, granted, slow, active; }
        Session memo;

        PathManager Path => levelManager ? levelManager.pathManager : null;

        /// <summary>While any source returns true the timer is paused (registered once; called every frame).</summary>
        public void AddPauseSource(Func<bool> pausedWhile)
        {
            if (pausedWhile != null) pauseSources.Add(pausedWhile);
        }

        void Awake()
        {
            if (!settings) { Debug.LogError("LevelTimer: assign TimerSettings.", this); enabled = false; return; }
            Boosts.Configure(settings);
        }

        void OnEnable()
        {
            if (!settings) return;
            if (levelManager) levelManager.LevelLoaded += OnLevelLoaded;
            if (Path)
            {
                Path.BoardTouched += OnBoardTouched;
                Path.Completed += OnCompleted;
            }
        }

        void OnDisable()
        {
            if (levelManager) levelManager.LevelLoaded -= OnLevelLoaded;
            if (Path)
            {
                Path.BoardTouched -= OnBoardTouched;
                Path.Completed -= OnCompleted;
            }
        }

        void OnApplicationPause(bool paused) => appPaused = paused;

#if !UNITY_EDITOR
        void OnApplicationFocus(bool focused) => appPaused = !focused;
#endif

        // ---------- level flow ----------

        void OnLevelLoaded(int index)
        {
            int number = levelManager.CurrentNumber;
            limit = levelManager.testLevel ? 0f : settings.LimitFor(number, Path ? Path.Level : null);
            bool resume = IsTimed && memo.valid && memo.index == index;
            if (resume)
            {
                remaining = memo.remaining;
                granted = memo.granted;
                SaturnUsed = memo.saturn;
                LunarUsed = memo.lunar;
                slowLeft = memo.slow;
                activeSeconds = memo.active;
                touched = memo.started;
                // Already started: it runs as soon as the board is back — no free look.
                State = memo.started ? TimerState.Running : TimerState.Ready;
            }
            else ResetClock(IsTimed ? TimerState.Ready : TimerState.Off);
            memo.index = index;
            memo.valid = IsTimed;
            Remember();

            ShowIntro = IsTimed && !resume && !SaveService.TimerIntroSeen;
            if (ShowIntro) SaveService.TimerIntroSeen = true;
            Configured?.Invoke(IsTimed);
            if (IsTimed) Tick?.Invoke(Remaining01);
            if (resume && IsSlowed) SlowStarted?.Invoke();
        }

        void ResetClock(TimerState state)
        {
            State = state;
            remaining = granted = limit;
            SaturnUsed = LunarUsed = 0;
            slowLeft = 0f;
            activeSeconds = 0f;
            touched = false;
        }

        /// <summary>"Try again" after time up: the level starts over with a full timer (it waits for a touch again).</summary>
        public void Restart()
        {
            if (!IsTimed || State == TimerState.Done) return;
            if (IsSlowed) EndSlow();
            ResetClock(TimerState.Ready);
            memo.valid = true;
            Remember();
            if (Path)
            {
                Path.SetLocked(false);
                Path.ResetPath();
            }
            Configured?.Invoke(true);
            Tick?.Invoke(1f);
        }

        void OnBoardTouched()
        {
            touched = true;
            if (State != TimerState.Ready) return;
            State = TimerState.Running;
            Started?.Invoke();
        }

        void OnCompleted()
        {
            int stars = StarsNow;
            if (IsSlowed) EndSlow();
            if (IsTimed) State = TimerState.Done;
            memo.valid = false;
            if (levelManager && !levelManager.testLevel) SaveService.RecordSolveTime(levelManager.CurrentIndex, activeSeconds);
            Won?.Invoke(stars);
        }

        // ---------- boosts ----------

        /// <summary>Saturn's Gift: uses one from the inventory and adds its seconds. False if not allowed / none owned.</summary>
        public bool TryExtend()
        {
            if (!CanExtend || !Boosts.TryConsume(BoostType.SaturnsGift)) return false;
            SaturnUsed++;
            float add = settings.saturnSeconds;
            remaining += add;
            granted += add;
            if (State == TimerState.TimeUp)
            {
                State = TimerState.Running;
                if (Path) Path.SetLocked(false);
                memo.valid = true;
            }
            Remember();
            Extended?.Invoke(add);
            Tick?.Invoke(Remaining01);
            return true;
        }

        /// <summary>Lunar Stillness: uses one and slows the timer for a while. False if not allowed / none owned.</summary>
        public bool TrySlow()
        {
            if (!CanSlow || !Boosts.TryConsume(BoostType.LunarStillness)) return false;
            LunarUsed++;
            slowLeft = settings.lunarSeconds;
            Remember();
            SlowStarted?.Invoke();
            return true;
        }

        void EndSlow()
        {
            slowLeft = 0f;
            SlowEnded?.Invoke();
        }

        // ---------- clock (no allocations) ----------

        void Update()
        {
            bool paused = appPaused;
            for (int i = 0; i < pauseSources.Count && !paused; i++) paused = pauseSources[i]();
            if (paused != IsPaused)
            {
                IsPaused = paused;
                PausedChanged?.Invoke(paused);
            }
            if (paused || State == TimerState.Done || State == TimerState.TimeUp) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, maxFrameStep);
            if (touched) activeSeconds += dt; // solve time, untimed levels too
            if (State != TimerState.Running) return;

            float speed = Speed;
            if (slowLeft > 0f)
            {
                slowLeft -= dt;
                if (slowLeft <= 0f) EndSlow();
            }
            remaining -= dt * speed;
            if (remaining <= 0f)
            {
                remaining = 0f;
                if (IsSlowed) EndSlow();
                State = TimerState.TimeUp;
                memo.valid = false;
                if (Path) Path.SetLocked(true);
                Tick?.Invoke(0f);
                TimeUp?.Invoke();
                return;
            }
            Tick?.Invoke(Remaining01);
            Remember();
        }

        void Remember()
        {
            memo.remaining = remaining;
            memo.granted = granted;
            memo.saturn = SaturnUsed;
            memo.lunar = LunarUsed;
            memo.slow = slowLeft;
            memo.active = activeSeconds;
            memo.started = State == TimerState.Running;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Testing / screenshots: jumps the clock to <paramref name="seconds"/> left (and starts it).</summary>
        public void DebugSetRemaining(float seconds)
        {
            if (!IsTimed) return;
            if (State == TimerState.Ready) OnBoardTouched();
            remaining = Mathf.Max(0.01f, seconds);
            Tick?.Invoke(Remaining01);
        }
#endif
    }
}
