using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Tiny sparkle bursts when a star lands from its pirouette. One pooled ParticleSystem per level
    /// (under the level root, so it tilts with the board and goes away with the level). Visual only, no allocations.
    /// </summary>
    public static class StarSparks
    {
        static ParticleSystem system;

        /// <summary>Emits <paramref name="count"/> sparkles flying out from <paramref name="star"/>.</summary>
        public static void Burst(Transform star, Color color, int count, StarStyle style)
        {
            if (!star.parent || count <= 0) return;
            if (!system || system.transform.parent != star.parent) system = Create(star.parent);

            float size = star.localScale.x;
            float spin = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                // Evenly spread around the star with a little jitter, so the burst reads as a ring, not a clump.
                float a = (i + Random.Range(-0.3f, 0.3f)) / count * 2f * Mathf.PI + spin * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var p = new ParticleSystem.EmitParams
                {
                    position = star.localPosition + dir * (size * 0.35f),
                    velocity = dir * (style.sparkSpeed * size * Random.Range(0.7f, 1.2f)),
                    startColor = Color.Lerp(color, Color.white, 0.45f),
                    startSize = style.sparkSize * size * Random.Range(0.7f, 1.2f),
                    startLifetime = style.sparkLifetime * Random.Range(0.8f, 1.2f),
                    rotation = Random.value * 90f,
                };
                system.Emit(p, 1);
            }
        }

        static ParticleSystem Create(Transform parent)
        {
            var go = new GameObject("Star Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // tilts with the board
            main.maxParticles = 160;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.enabled = false; // only explicit Emit() calls
            var shape = ps.shape;
            shape.enabled = false;

            // Shoot out, then slow down quickly.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = 0f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var shrink = ps.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            tsa.SetSprite(0, UIKit.Sparkle);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.SpriteMaterial;
            r.sortingOrder = 21; // just above the stars
            ps.Play();
            return ps;
        }
    }
}
