using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The drawn path, styled by the equipped wand (CosmeticCatalog.Wand + its WandLook):
    /// - Line: each new connection draws itself from the previous star to the new one over ~300 ms (colors blend
    ///   from one star to the next). One pooled LineRenderer per segment with the WandLine shader (anti-aliased core,
    ///   round ends, soft glow, sparkles / rainbow / shimmer / ink width), or two plain LineRenderers as a fallback.
    /// - Drag dust: fairy dust that follows the finger (PathManager.Dragged), more when it moves faster.
    /// - Reach burst: a few sparks (and optionally a flash) when the line reaches a star.
    /// - Comet: a bright head at the finger with a tail of its last ~0.3 s. Gold Ink: each segment's width follows
    ///   how fast the finger moved along it.
    /// Everything lives under the BoardPivot in local space, so it tilts with the board. Visual only.
    /// </summary>
    public class WandTrail : MonoBehaviour
    {
        public PathManager pathManager;
        public StarStyle style;
        [Tooltip("Wands (trail effects). Falls back to the LevelManager's catalog on this GameObject.")]
        public CosmeticCatalog cosmetics;

        class Segment
        {
            public LineRenderer line;
            public LineRenderer glow; // only without the WandLine shader
            public Vector3 a, b;
            public Color from, to;
            public float start, length, t;
            public Vector4 widths; // core width at 0, 1/3, 2/3, 1 of the way (Gold Ink)
            public bool drawing;
        }

        const int TailCapacity = 48; // comet tail samples
        const int LegCapacity = 64;  // finger samples between two stars (Gold Ink)

        static MaterialPropertyBlock block;
        static readonly int SegId = Shader.PropertyToID("_Seg"), SegWidthId = Shader.PropertyToID("_SegWidth");

        Transform root;
        ParticleSystem dust, burst, flash;
        ParticleSystemRenderer dustRenderer;
        Material lineMaterial; // null = fallback lines
        CosmeticCatalog.Wand wand;
        WandLook look;
        WandEffect effect;
        readonly List<Segment> active = new();
        readonly List<Segment> pool = new();
        float pathLength;   // along the placed segments, up to the last star
        Vector3 lastFinger;
        bool hasFinger;
        float dustDue;      // fractional motes carried to the next frame

        // Comet: ring buffer of finger points, newest at tailNewest.
        LineRenderer cometTail;
        SpriteRenderer cometHead, cometHalo;
        readonly Vector3[] tailPoints = new Vector3[TailCapacity];
        readonly float[] tailTimes = new float[TailCapacity];
        readonly Vector3[] tailLine = new Vector3[TailCapacity + 1];
        int tailNewest, tailCount;
        float tailPushedAt, headAlpha;

        // Gold Ink: finger samples since the last star, and the smoothed finger speed.
        readonly Vector3[] legPoints = new Vector3[LegCapacity];
        readonly float[] legSpeeds = new float[LegCapacity];
        readonly float[] binSpeed = new float[4];
        readonly int[] binCount = new int[4];
        int legCount;
        float inkSpeed, lastInkWidth = -1f;

        void Awake()
        {
            if (!cosmetics && TryGetComponent(out LevelManager levelManager)) cosmetics = levelManager.cosmetics;
        }

        void OnEnable()
        {
            Cosmetics.Changed += ApplyWandIfBuilt;
            WandLook.Changed += OnLookEdited;
            if (!pathManager) return;
            pathManager.NodeVisited += OnNodeVisited;
            pathManager.PathCleared += Clear;
            pathManager.BoardBuilt += Clear;
            pathManager.Dragged += OnDragged;
        }

        void OnDisable()
        {
            Cosmetics.Changed -= ApplyWandIfBuilt;
            WandLook.Changed -= OnLookEdited;
            if (!pathManager) return;
            pathManager.NodeVisited -= OnNodeVisited;
            pathManager.PathCleared -= Clear;
            pathManager.BoardBuilt -= Clear;
            pathManager.Dragged -= OnDragged;
        }

        void EnsureRoot()
        {
            if (root) return;
            root = new GameObject("WandTrail").transform;
            root.SetParent(pathManager.BoardPivot, false); // tilts with the board
            dust = CreateSystem(root, "Dust", 11);
            dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            burst = CreateSystem(root, "Reach Burst", 21);
            flash = CreateSystem(root, "Reach Flash", 22);
            ApplyWand();
        }

        // Equipping a wand in the shop restyles the line already drawn and the dust from now on.
        void ApplyWandIfBuilt()
        {
            if (root) ApplyWand();
        }

        void OnLookEdited(WandLook edited)
        {
            if (root && edited == look) ApplyWand();
        }

        void ApplyWand()
        {
            wand = cosmetics ? cosmetics.EquippedWand() : null;
            look = WandLook.Of(wand);
            effect = wand?.effect ?? WandEffect.Classic;

            if (style && style.wandShader && style.wandShader.isSupported)
            {
                if (!lineMaterial) lineMaterial = new Material(style.wandShader) { name = "Wand Line", hideFlags = HideFlags.DontSave };
                look.ApplyLine(lineMaterial, effect);
            }
            else lineMaterial = null;

            ConfigureDust();
            ConfigureBurst();
            ConfigureComet();
            foreach (var seg in active)
            {
                SetupRenderers(seg);
                Draw(seg, seg.drawing ? Ease.OutCubic(seg.t) : 1f);
            }
        }

        // ---------- line ----------

        void OnNodeVisited(Node node, int visitIndex, Color color)
        {
            if (!style) return;
            EnsureRoot();
            if (visitIndex == 0)
            {
                pathLength = 0f;
                hasFinger = false;
                legCount = 0;
                inkSpeed = 0f; // a stroke starts slow, so thick
                lastInkWidth = -1f;
                return;
            }
            var from = pathManager.Path[visitIndex - 1];
            var seg = Take();
            seg.a = from.BoardPoint;
            seg.b = node.BoardPoint;
            seg.from = from.CurrentColor;
            seg.to = color;
            seg.start = pathLength;
            seg.length = Vector3.Distance(seg.a, seg.b);
            seg.widths = effect == WandEffect.GoldInk ? InkProfile(seg.a, seg.b) : Vector4.one;
            seg.t = 0f;
            seg.drawing = true;
            pathLength += seg.length;
            legCount = 0;
            Draw(seg, 0f);
        }

        void LateUpdate()
        {
            if (cometTail && effect == WandEffect.Comet) UpdateComet();
            if (active.Count == 0 || !style) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, style.trailDrawTime);
            for (int i = 0; i < active.Count; i++)
            {
                var seg = active[i];
                if (!seg.drawing) continue;
                seg.t = Mathf.Min(1f, seg.t + step);
                Draw(seg, Ease.OutCubic(seg.t));
                if (seg.t < 1f) continue;
                seg.drawing = false;
                Burst(seg); // the line has reached the star
            }
        }

        Segment Take()
        {
            Segment seg;
            if (pool.Count > 0)
            {
                seg = pool[^1];
                pool.RemoveAt(pool.Count - 1);
            }
            else
            {
                seg = new Segment { line = PathManager.CreateLine("Segment", root, 0.1f, Color.white, 10) };
                seg.line.positionCount = 2;
            }
            SetupRenderers(seg);
            active.Add(seg);
            return seg;
        }

        void SetupRenderers(Segment seg)
        {
            var line = seg.line;
            line.enabled = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Stretch;
            if (lineMaterial)
            {
                line.sharedMaterial = lineMaterial;
                line.numCapVertices = line.numCornerVertices = 0; // the shader draws the round ends
                if (seg.glow) seg.glow.enabled = false;
                return;
            }
            line.sharedMaterial = Art.SpriteMaterial;
            line.numCapVertices = line.numCornerVertices = 6;
            line.SetPropertyBlock(null);
            line.widthMultiplier = style.trailWidth;
            if (!seg.glow)
            {
                seg.glow = PathManager.CreateLine("Segment Glow", root, style.trailWidth * style.trailGlowWidth, Color.white, 9);
                seg.glow.sharedMaterial = Art.SoftLineMaterial; // fades out across its width: a soft halo, not a band
                seg.glow.textureMode = LineTextureMode.Stretch;
                seg.glow.positionCount = 2;
            }
            seg.glow.enabled = true;
        }

        // Draws the segment from its start to k (0..1) of the way to its end star.
        void Draw(Segment seg, float k)
        {
            Vector3 tip = Vector3.Lerp(seg.a, seg.b, k);
            float alpha = Mathf.Lerp(0.35f, 1f, k);
            Color ca = seg.from, cb = seg.to;
            ca.a *= alpha;
            cb.a *= alpha;
            var line = seg.line;
            line.startColor = ca;
            line.endColor = cb;
            if (lineMaterial)
            {
                // Geometry runs half a width past both ends; the shader rounds them off.
                float half = look.HalfWidth(effect);
                Vector3 dir = seg.length > 1e-5f ? (seg.b - seg.a) / seg.length : Vector3.right;
                line.widthMultiplier = half * 2f;
                line.SetPosition(0, seg.a - dir * half);
                line.SetPosition(1, tip + dir * half);
                block ??= new MaterialPropertyBlock();
                block.SetVector(SegId, new Vector4(seg.length * k, half, seg.start, seg.length));
                block.SetVector(SegWidthId, seg.widths);
                line.SetPropertyBlock(block);
                return;
            }
            line.SetPosition(0, seg.a);
            line.SetPosition(1, tip);
            ca.a *= style.trailGlowAlpha;
            cb.a *= style.trailGlowAlpha;
            seg.glow.startColor = ca;
            seg.glow.endColor = cb;
            seg.glow.SetPosition(0, seg.a);
            seg.glow.SetPosition(1, tip);
        }

        /// <summary>Removes the drawn segments and all dust (restart, fail, new level, or the level-complete sequence taking over).</summary>
        public void Clear()
        {
            foreach (var seg in active)
            {
                seg.line.enabled = false;
                if (seg.glow) seg.glow.enabled = false;
                seg.drawing = false;
                pool.Add(seg);
            }
            active.Clear();
            if (dust) dust.Clear();
            if (burst) burst.Clear();
            if (flash) flash.Clear();
            pathLength = 0f;
            hasFinger = false;
            dustDue = 0f;
            legCount = 0;
            lastInkWidth = -1f;
            tailCount = 0;
            headAlpha = 0f;
            if (cometTail) cometTail.positionCount = 0;
            if (cometHead) cometHead.enabled = cometHalo.enabled = false;
        }

        // ---------- finger ----------

        void OnDragged(Vector3 finger)
        {
            if (!style || pathManager.Path.Count == 0) return;
            EnsureRoot();
            if (effect == WandEffect.Comet) PushTail(finger);
            if (!hasFinger)
            {
                lastFinger = finger;
                hasFinger = true;
                return;
            }
            Vector3 delta = finger - lastFinger;
            float moved = delta.magnitude;
            if (effect == WandEffect.GoldInk) RecordInk(finger, moved);

            // Faster finger = more dust; standing still = almost none.
            dustDue += moved * look.dustPerUnit + look.dustIdleRate * Time.deltaTime;
            int count = Mathf.Min((int)dustDue, look.dustMaxPerFrame);
            dustDue -= (int)dustDue; // what the per-frame cap cut off is dropped, not saved up
            if (count > 0)
            {
                Vector3 dir = moved > 1e-5f ? delta / moved : Vector3.zero;
                var last = pathManager.Path[^1];
                float along = pathLength + Vector3.Distance(last.BoardPoint, finger);
                float time = Time.timeSinceLevelLoad;
                bool spins = look.dustSpin.y > 0f;
                for (int i = 0; i < count; i++)
                {
                    var p = new ParticleSystem.EmitParams
                    {
                        position = Vector3.Lerp(lastFinger, finger, Random.value),
                        velocity = (Vector3)(Random.insideUnitCircle * look.dustScatter) + (Vector3)look.dustDrift - dir * look.dustTrailBack,
                        startColor = look.DustColor(effect, along, time, last.CurrentColor),
                        startSize = Random.Range(look.dustSize.x, look.dustSize.y),
                        startLifetime = Random.Range(look.dustLifetime.x, look.dustLifetime.y),
                        rotation = look.dustShape == DustShape.Sparkle || spins ? Random.value * 360f : 0f,
                        angularVelocity = spins ? Random.Range(look.dustSpin.x, look.dustSpin.y) * (Random.value < 0.5f ? -1f : 1f) : 0f,
                    };
                    dust.Emit(p, 1);
                }
            }
            lastFinger = finger;
        }

        void Burst(Segment seg)
        {
            var range = look.burstCount;
            int count = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y) + 1);
            float spin = Random.value * 2f * Mathf.PI;
            float along = seg.start + seg.length, time = Time.timeSinceLevelLoad;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.35f, 0.35f)) / count * 2f * Mathf.PI + spin;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var p = new ParticleSystem.EmitParams
                {
                    position = seg.b + dir * 0.03f,
                    velocity = dir * (look.burstSpeed * Random.Range(0.6f, 1.1f)),
                    startColor = look.BurstColor(effect, along, time, seg.to),
                    startSize = Random.Range(look.burstSize.x, look.burstSize.y),
                    startLifetime = look.burstLifetime * Random.Range(0.75f, 1f),
                    rotation = Random.value * 90f,
                };
                burst.Emit(p, 1);
            }
            if (look.burstFlashSize > 0f)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = seg.b,
                    velocity = Vector3.zero,
                    startColor = look.burstFlashColor,
                    startSize = look.burstFlashSize,
                    startLifetime = look.burstFlashTime,
                };
                flash.Emit(p, 1);
            }
        }

        // ---------- Gold Ink ----------

        // Smoothed finger speed, and where the finger was at that speed (projected onto the segment when a star is reached).
        void RecordInk(Vector3 finger, float moved)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, look.inkSmoothing));
            inkSpeed = Mathf.Lerp(inkSpeed, moved / dt, k);
            if (legCount == LegCapacity)
            {
                // Full: keep every other sample, so a slow leg still covers the whole segment.
                for (int i = 0; i < LegCapacity / 2; i++)
                {
                    legPoints[i] = legPoints[i * 2 + 1];
                    legSpeeds[i] = legSpeeds[i * 2 + 1];
                }
                legCount = LegCapacity / 2;
            }
            legPoints[legCount] = finger;
            legSpeeds[legCount++] = inkSpeed;
        }

        // Core widths at 0, 1/3, 2/3 and the end of a new segment, from the finger's speed along it.
        Vector4 InkProfile(Vector3 a, Vector3 b)
        {
            for (int i = 0; i < 4; i++)
            {
                binSpeed[i] = 0f;
                binCount[i] = 0;
            }
            Vector3 ab = b - a;
            float lengthSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int i = 0; i < legCount; i++)
            {
                int bin = Mathf.RoundToInt(Mathf.Clamp01(Vector3.Dot(legPoints[i] - a, ab) / lengthSq) * 3f);
                binSpeed[bin] += legSpeeds[i];
                binCount[bin]++;
            }
            var widths = Vector4.zero;
            for (int i = 0; i < 4; i++) widths[i] = look.InkWidth(BinSpeed(i));
            if (lastInkWidth >= 0f) widths.x = lastInkWidth; // the stroke stays continuous through the star
            lastInkWidth = widths.w;
            return widths;
        }

        // Average speed in a bin; an empty bin borrows from its nearest filled neighbors, or the current speed.
        float BinSpeed(int i)
        {
            if (binCount[i] > 0) return binSpeed[i] / binCount[i];
            float sum = 0f;
            int n = 0;
            for (int d = 1; d < 4 && n == 0; d++)
            {
                if (i - d >= 0 && binCount[i - d] > 0) { sum += binSpeed[i - d] / binCount[i - d]; n++; }
                if (i + d < 4 && binCount[i + d] > 0) { sum += binSpeed[i + d] / binCount[i + d]; n++; }
            }
            return n > 0 ? sum / n : inkSpeed;
        }

        // ---------- Comet ----------

        void PushTail(Vector3 finger)
        {
            float now = Time.time;
            // Space the samples so the buffer spans the whole tail at any frame rate; the head always tracks the finger.
            float spacing = Mathf.Max(0.001f, look.cometTailTime) / (TailCapacity - 2);
            if (tailCount == 0 || now - tailPushedAt >= spacing)
            {
                tailNewest = (tailNewest + 1) % TailCapacity;
                tailCount = Mathf.Min(tailCount + 1, TailCapacity);
                tailPushedAt = now;
            }
            tailPoints[tailNewest] = finger; // the newest point follows the finger until the next one is pushed
            tailTimes[tailNewest] = now;
        }

        void UpdateComet()
        {
            float now = Time.time;
            while (tailCount > 0 && now - tailTimes[(tailNewest - tailCount + 1 + TailCapacity) % TailCapacity] > look.cometTailTime)
                tailCount--;
            bool held = pathManager.IsDrawing && tailCount > 0;
            headAlpha = Mathf.MoveTowards(headAlpha, held ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, look.cometFadeTime));

            if (tailCount >= 2)
            {
                for (int i = 0; i < tailCount; i++) tailLine[i] = tailPoints[(tailNewest - i + TailCapacity) % TailCapacity];
                cometTail.positionCount = tailCount;
                cometTail.SetPositions(tailLine);
            }
            else cometTail.positionCount = 0;

            bool visible = headAlpha > 0.01f && tailCount > 0;
            cometHead.enabled = cometHalo.enabled = visible;
            if (!visible) return;
            var at = tailPoints[tailNewest];
            cometHead.transform.localPosition = cometHalo.transform.localPosition = at;
            cometHead.color = UIKit.WithAlpha(look.cometHeadColor, headAlpha);
            cometHalo.color = UIKit.WithAlpha(look.cometHeadColor, look.cometHaloAlpha * headAlpha);
        }

        void ConfigureComet()
        {
            bool on = effect == WandEffect.Comet;
            if (!on && !cometTail) return;
            if (!cometTail)
            {
                cometTail = PathManager.CreateLine("Comet Tail", root, 0.1f, Color.white, 12);
                cometTail.sharedMaterial = new Material(Art.UnlitSpriteMaterial)
                    { name = "Comet Tail", mainTexture = Art.SoftLineMaterial.mainTexture, hideFlags = HideFlags.DontSave };
                cometTail.textureMode = LineTextureMode.Stretch;
                cometTail.numCapVertices = 4;
                cometTail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // tapers away from the head
                cometHalo = NewSprite("Comet Halo", Art.SoftCircle, 12);
                cometHead = NewSprite("Comet Head", Art.Mote, 13);
            }
            cometTail.enabled = on;
            cometTail.widthMultiplier = look.cometTailWidth;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(look.cometHeadColor, 0f), new GradientColorKey(look.cometTailColor, 0.35f), new GradientColorKey(look.cometTailColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0f, 1f) });
            cometTail.colorGradient = gradient;
            cometHead.transform.localScale = Vector3.one * look.cometHeadSize;
            cometHalo.transform.localScale = Vector3.one * (look.cometHeadSize * look.cometHaloScale);
            if (!on)
            {
                cometTail.positionCount = 0;
                cometHead.enabled = cometHalo.enabled = false;
                tailCount = 0;
            }
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(root, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = Art.UnlitSpriteMaterial;
            sr.sortingOrder = sortingOrder;
            sr.enabled = false;
            return sr;
        }

        // ---------- particle setup ----------

        void ConfigureDust()
        {
            var main = dust.main;
            main.maxParticles = Mathf.Max(1, look.dustMaxParticles);
            dust.textureSheetAnimation.SetSprite(0, WandLook.Sprite(look.dustShape));
            dustRenderer.renderMode = look.dustStretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            dustRenderer.velocityScale = look.dustStretch;
            dustRenderer.lengthScale = 1f;
            var force = dust.forceOverLifetime;
            force.enabled = look.dustGravity != 0f;
            force.space = ParticleSystemSimulationSpace.Local;
            force.x = force.z = 0f;
            force.y = -look.dustGravity;

            var size = dust.sizeOverLifetime;
            size.enabled = true;
            var curve = look.dustTwinkle ? WandLook.TwinkleCurve : WandLook.ShrinkCurve;
            size.separateAxes = look.dustFlip > 0f;
            if (size.separateAxes)
            {
                // Tumbling flakes: the width flips over while the height just shrinks.
                float flips = look.dustFlip * (look.dustLifetime.x + look.dustLifetime.y) * 0.5f;
                size.x = new ParticleSystem.MinMaxCurve(1f, WandLook.FlipCurve(flips));
                size.y = new ParticleSystem.MinMaxCurve(1f, curve);
                size.z = 1f;
            }
            else size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var noise = dust.noise;
            noise.enabled = look.dustFlutter > 0f;
            if (noise.enabled)
            {
                noise.quality = ParticleSystemNoiseQuality.Low;
                noise.strength = look.dustFlutter;
                noise.frequency = look.dustFlutterFrequency;
                noise.scrollSpeed = look.dustFlutterFrequency * 0.5f;
                noise.damping = true;
            }
        }

        void ConfigureBurst()
        {
            var main = burst.main;
            main.maxParticles = 64;
            burst.textureSheetAnimation.SetSprite(0, WandLook.Sprite(look.burstShape));
            var limit = burst.limitVelocityOverLifetime;
            limit.dampen = 0.18f; // shoot out, then slow down fast
            var size = burst.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, WandLook.ShrinkCurve);

            var flashMain = flash.main;
            flashMain.maxParticles = 16;
            flash.textureSheetAnimation.SetSprite(0, Art.Mote);
            var flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, WandLook.FlashCurve);
        }

        static ParticleSystem CreateSystem(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // tilts with the board
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
            r.sharedMaterial = Art.UnlitSpriteMaterial; // dust is light: the 2D lights must not dim it
            r.sortingOrder = sortingOrder;
            ps.Play();
            return ps;
        }
    }
}
