using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Space backdrop: a deep navy radial gradient plus three layers of faint, slowly twinkling stars that
    /// drift with the board tilt at different rates (far = slow, near = fast). The gradient center picks up a
    /// light tint of the tier / equipped background color. Visual only.
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        public BoardView boardView;
        public PathManager pathManager;
        public DepthSettings settings;
        public StarStyle style;
        public int seed = 7;
        [Tooltip("Extra zoom of the star layers (set by ScreenRouter: slightly closer on the home screen).")]
        public float zoom = 1f;
        [Tooltip("Twinkle speed (Lunar Stillness slows it). Visual only.")]
        public float timeScale = 1f;
        float skyTime;

        // Layout is in "view units": 1 = half the screen height, so layers fit any zoom level.
        const float SpreadX = 2.6f, SpreadY = 1.4f;

        struct Twinkle
        {
            public SpriteRenderer renderer;
            public float baseAlpha, speed, phase;
        }

        readonly Transform[] layers = new Transform[3];
        readonly List<Twinkle> stars = new();
        Camera cam;
        SpriteRenderer glow;
        Color lastTint = Color.clear;

        void Start()
        {
            cam = Camera.main;
            if (!settings) { enabled = false; return; }
            var rnd = new System.Random(seed);

            // Radial gradient: camera clears to the edge color; a big soft disc in the center color on top.
            var glowGo = new GameObject("Backdrop Glow", typeof(SpriteRenderer));
            glowGo.transform.SetParent(cam.transform, false);
            glow = glowGo.GetComponent<SpriteRenderer>();
            glow.sprite = Art.SoftCircle;
            glow.sharedMaterial = Art.SpriteMaterial;
            glow.sortingOrder = -60;

            // Far layer: most, tiniest and faintest; near layer: fewer, a bit bigger and brighter.
            float[] sizeMin = { 0.004f, 0.006f, 0.008f }, sizeMax = { 0.007f, 0.010f, 0.014f };
            int[] counts = { settings.parallaxCounts.x, settings.parallaxCounts.y, settings.parallaxCounts.z };
            float[] alpha = { settings.parallaxAlpha.x, settings.parallaxAlpha.y, settings.parallaxAlpha.z };
            for (int l = 0; l < 3; l++)
            {
                var layer = new GameObject($"Star Layer {l}").transform;
                layer.SetParent(cam.transform, false);
                for (int i = 0; i < counts[l]; i++)
                {
                    var go = new GameObject("Star", typeof(SpriteRenderer));
                    go.transform.SetParent(layer, false);
                    go.transform.localPosition = new Vector3(
                        (float)(rnd.NextDouble() * 2 - 1) * SpreadX, (float)(rnd.NextDouble() * 2 - 1) * SpreadY, 0);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(sizeMin[l], sizeMax[l], (float)rnd.NextDouble());
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = Art.Circle;
                    sr.sharedMaterial = Art.SpriteMaterial;
                    sr.sortingOrder = -40 + l * 5; // far first
                    sr.color = new Color(0.85f, 0.9f, 1f, 0f);
                    stars.Add(new Twinkle
                    {
                        renderer = sr,
                        baseAlpha = alpha[l] * Mathf.Lerp(0.5f, 1f, (float)rnd.NextDouble()),
                        speed = Mathf.Lerp(0.25f, 0.7f, (float)rnd.NextDouble()), // slow
                        phase = (float)rnd.NextDouble() * 10f,
                    });
                }
                layers[l] = layer;
            }
        }

        void LateUpdate()
        {
            if (!cam || !layers[0]) return;
            float size = cam.orthographicSize;
            Vector2 tilt = boardView ? boardView.Tilt : Vector2.zero;
            Vector3 strength = settings.parallaxStrength;
            float[] rates = { strength.x, strength.y, strength.z };
            for (int l = 0; l < 3; l++)
            {
                layers[l].localScale = new Vector3(size * zoom, size * zoom, 1f);
                Vector2 offset = -tilt * rates[l] * size; // opposite to the tilt, farther layers move less
                layers[l].localPosition = new Vector3(offset.x, offset.y, 50f + (2 - l) * 10f);
            }

            skyTime += Time.deltaTime * timeScale;
            float t = skyTime;
            foreach (var s in stars)
            {
                var c = s.renderer.color;
                c.a = s.baseAlpha * (0.55f + 0.45f * Mathf.Sin(t * s.speed + s.phase));
                s.renderer.color = c;
            }

            UpdateGradient(size);
        }

        void UpdateGradient(float size)
        {
            if (!style) return;
            // PathManager.ApplyTheme puts the tier / equipped background color on the camera; use it as a tint.
            var tint = pathManager ? pathManager.background : style.backgroundCenter;
            glow.transform.localPosition = new Vector3(0, 0, 90f);
            glow.transform.localScale = Vector3.one * size * 3.2f * Mathf.Max(1f, cam.aspect);
            cam.backgroundColor = style.backgroundEdge; // every frame: level loads reset the camera color
            if (tint == lastTint) return;
            lastTint = tint;
            glow.color = Color.Lerp(style.backgroundCenter, tint, style.backgroundTint);
        }
    }
}
