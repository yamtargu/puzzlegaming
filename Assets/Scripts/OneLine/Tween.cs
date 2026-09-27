using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>Easing curves (t in 0..1).</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        /// <summary>Overshoots a little past 1, then settles.</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
        /// <summary>Eases in and out, overshooting a little at the end (and dipping a little at the start).</summary>
        public static float InOutBack(float t)
        {
            const float c2 = 1.70158f * 1.525f;
            return t < 0.5f
                ? 2f * t * 2f * t * ((c2 + 1f) * 2f * t - c2) / 2f
                : ((2f * t - 2f) * (2f * t - 2f) * ((c2 + 1f) * (2f * t - 2f) + c2) + 2f) / 2f;
        }
    }

    /// <summary>
    /// Minimal tween runner (DOTween isn't in the project yet). Usage mirrors a DOTween "DOVirtual.Float":
    /// Tween.Run(owner, duration, Ease.OutCubic, k => ..., onComplete). The tween stops by itself when
    /// its owner is destroyed. Swapping to DOTween later is a one-line change per call site.
    /// </summary>
    public sealed class Tween
    {
        readonly UnityEngine.Object owner;
        readonly float duration;
        readonly Func<float, float> ease;
        readonly Action<float> update;
        readonly Action complete;
        float elapsed;
        public bool Alive { get; private set; } = true;

        Tween(UnityEngine.Object owner, float duration, Func<float, float> ease, Action<float> update, Action complete)
        {
            this.owner = owner;
            this.duration = Mathf.Max(0.0001f, duration);
            this.ease = ease ?? Ease.Linear;
            this.update = update;
            this.complete = complete;
        }

        /// <summary>Calls <paramref name="update"/> every frame with the eased progress (0..1) for <paramref name="duration"/> seconds.</summary>
        public static Tween Run(UnityEngine.Object owner, float duration, Func<float, float> ease, Action<float> update,
            Action complete = null)
        {
            var tween = new Tween(owner, duration, ease, update, complete);
            Runner.Instance.Add(tween);
            tween.update?.Invoke(tween.ease(0f));
            return tween;
        }

        public void Kill() => Alive = false;

        // Returns false when finished or dead.
        bool Step(float dt)
        {
            if (!Alive || !owner) return false;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            update?.Invoke(ease(t));
            if (t < 1f) return true;
            Alive = false;
            complete?.Invoke();
            return false;
        }

        sealed class Runner : MonoBehaviour
        {
            static Runner instance;
            readonly List<Tween> tweens = new();
            readonly List<Tween> adding = new();

            public static Runner Instance
            {
                get
                {
                    if (instance) return instance;
                    var go = new GameObject("[Tweens]") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    return instance = go.AddComponent<Runner>();
                }
            }

            public void Add(Tween t) => adding.Add(t);

            float stepTime;
            Predicate<Tween> finished; // cached: a lambda capturing a local would allocate every frame

            void Update()
            {
                tweens.AddRange(adding);
                adding.Clear();
                stepTime = Time.deltaTime;
                finished ??= t => !t.Step(stepTime);
                tweens.RemoveAll(finished);
            }
        }
    }
}
