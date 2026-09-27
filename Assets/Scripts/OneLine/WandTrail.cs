using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// "Wand trail" for the drawn path: each new connection draws itself from the previous star to the new
    /// one over ~300 ms as a thin glowing line (color blends from one star to the next), over a soft glow
    /// in the same color, while a few dust particles drift off the moving tip and fade. The equipped wand
    /// (CosmeticCatalog.Wand, bought in the shop) decides how that dust looks and moves. Visual only.
    /// </summary>
    public class WandTrail : MonoBehaviour
    {
        public PathManager pathManager;
        public StarStyle style;
        [Tooltip("Wands (trail effects). Falls back to the LevelManager's catalog on this GameObject.")]
        public CosmeticCatalog cosmetics;

        static readonly CosmeticCatalog.Wand DefaultWand = new(); // the original stardust, without a catalog

        Transform root;
        ParticleSystem dust;
        ParticleSystemRenderer dustRenderer;
        CosmeticCatalog.Wand wand;
        readonly List<GameObject> segments = new();
        readonly List<Tween> tweens = new();

        void Awake()
        {
            if (!cosmetics && TryGetComponent(out LevelManager levelManager)) cosmetics = levelManager.cosmetics;
        }

        void OnEnable()
        {
            Cosmetics.Changed += OnCosmeticsChanged;
            if (!pathManager) return;
            pathManager.NodeVisited += OnNodeVisited;
            pathManager.PathCleared += Clear;
        }

        void OnDisable()
        {
            Cosmetics.Changed -= OnCosmeticsChanged;
            if (!pathManager) return;
            pathManager.NodeVisited -= OnNodeVisited;
            pathManager.PathCleared -= Clear;
        }

        void EnsureRoot()
        {
            if (root) return;
            root = new GameObject("WandTrail").transform;
            root.SetParent(pathManager.BoardPivot, false); // tilts with the board
            dust = CreateDust(root);
            dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            ApplyWand();
        }

        // Equipping a wand in the shop changes the dust from the next connection on.
        void OnCosmeticsChanged()
        {
            if (dust) ApplyWand();
        }

        void ApplyWand()
        {
            wand = (cosmetics ? cosmetics.EquippedWand() : null) ?? DefaultWand;
            var tsa = dust.textureSheetAnimation;
            tsa.SetSprite(0, wand.particle switch
            {
                CosmeticCatalog.WandParticle.Sparkle => UIKit.Sparkle,
                CosmeticCatalog.WandParticle.Star => Art.Star,
                _ => Art.SoftCircle,
            });
            dustRenderer.renderMode = wand.stretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            dustRenderer.velocityScale = wand.stretch;
            dustRenderer.lengthScale = 1f;
        }

        void OnNodeVisited(Node node, int visitIndex, Color color)
        {
            if (!style || visitIndex == 0) return;
            EnsureRoot();
            var from = pathManager.Path[visitIndex - 1];
            Color fromColor = from.CurrentColor;
            Vector3 a = from.BoardPoint, b = node.BoardPoint;
            Vector3 dir = (b - a).normalized;
            float dustCount = style.dustPerSegment * wand.amount;

            var glow = PathManager.CreateLine("Segment Glow", root, style.trailWidth * style.trailGlowWidth, color, 9);
            glow.sharedMaterial = Art.SoftLineMaterial; // fades out across its width: a soft halo, not a band
            glow.textureMode = LineTextureMode.Stretch;
            var core = PathManager.CreateLine("Segment", root, style.trailWidth, color, 10);
            segments.Add(glow.gameObject);
            segments.Add(core.gameObject);
            glow.positionCount = core.positionCount = 2;

            Vector3 lastTip = a;
            int emitted = 0;
            tweens.Add(Tween.Run(core, style.trailDrawTime, Ease.OutCubic, k =>
            {
                Vector3 tip = Vector3.Lerp(a, b, k);
                float alpha = Mathf.Lerp(0.35f, 1f, k);
                SetLine(core, a, tip, fromColor, color, alpha);
                SetLine(glow, a, tip, fromColor, color, alpha * style.trailGlowAlpha);
                // Spread the dust evenly over the draw.
                int due = Mathf.RoundToInt(k * dustCount);
                for (; emitted < due; emitted++) EmitDust(Vector3.Lerp(lastTip, tip, Random.value), Color.Lerp(fromColor, color, k), dir);
                lastTip = tip;
            }));
        }

        static void SetLine(LineRenderer line, Vector3 a, Vector3 b, Color ca, Color cb, float alpha)
        {
            ca.a *= alpha;
            cb.a *= alpha;
            line.startColor = ca;
            line.endColor = cb;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
        }

        void EmitDust(Vector3 position, Color color, Vector3 dir)
        {
            var w = wand;
            if (w.tintAmount > 0f) color = Color.Lerp(color, w.tint, w.tintAmount);
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = (Vector3)(Random.insideUnitCircle * w.scatter) + (Vector3)w.drift - dir * w.trailBack,
                startColor = color,
                startSize = Random.Range(w.size.x, w.size.y),
                startLifetime = Random.Range(w.lifetime.x, w.lifetime.y),
            };
            dust.Emit(p, 1);
        }

        /// <summary>Removes the drawn segments (the level-complete sequence takes over the figure).</summary>
        public void Clear()
        {
            foreach (var t in tweens) t.Kill();
            tweens.Clear();
            foreach (var s in segments) if (s) Destroy(s);
            segments.Clear();
        }

        static ParticleSystem CreateDust(Transform parent)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // tilts with the board
            main.maxParticles = 400;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.enabled = false; // only explicit Emit() calls

            var shape = ps.shape;
            shape.enabled = false;

            // Drift slows down, and each mote fades out over its life.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = 0f;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            tsa.SetSprite(0, Art.SoftCircle);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.SpriteMaterial;
            r.sortingOrder = 11;
            ps.Play();
            return ps;
        }
    }
}
