using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneLine
{
    /// <summary>
    /// The level-complete "constellation fusion" (~3 s), started by PathManager's win:
    /// 1. recognition — the drawn figure brightens, its lines thicken, a soft halo appears;
    /// 2. trace pulse — a light runs along the path and each star pings with a rising chime;
    /// 3. merge — a gathering orb reels the figure in along the path the player drew: the line behind it retracts,
    ///    each star it reaches slides into it (ping, chime, the orb grows); the camera eases in, the sky dims;
    /// 4. glide — the full orb moves to the fusion point (the centroid, pulled toward the screen center);
    /// 5. fusion + flare — flash, ray burst, shockwave ring(s), a little shake, the swell, a haptic, and the house's
    ///    zodiac glyph lighting up inside the flare;
    /// 6. rise — the glyph lifts off the board toward the viewer, glowing (halo, slow rays, orbiting sparkles, a glow
    ///    ring), while "♉ TAURUS" and the level name fade in beneath it under a sweep of gold light
    ///    (<see cref="Arrived"/>: the HUD title gets the same sweep and its medallion pulses);
    /// 7. exit — the sign dissolves into a puff of gold stardust, then <see cref="Finished"/> (win panel / next level).
    /// The next level's stars then grow out of the center. A tap fast-forwards to the end; Reduce Motion plays only the
    /// flare and the name. BOSS / HARD levels get a bigger version. Colors come from the stars (so equipped line skins
    /// carry through), with gold for the orb's core, the flare and the name.
    ///
    /// The drawn figure is taken over by pooled stand-ins under the (tilting) board pivot at the stars' own positions,
    /// so the board's tilt and the stars' 2.5D height carry over; nothing is allocated per frame.
    /// </summary>
    public class CompletionSequence : MonoBehaviour
    {
        public PathManager pathManager;
        public LevelManager levelManager;
        public CompletionSettings settings;
        public StarStyle style;
        [Tooltip("Fonts, the level name and the sign line under the rising glyph.")]
        public UIStyle ui;
        [Tooltip("Gold and the zodiac glyphs.")]
        public ZodiacTheme theme;
        [Tooltip("Plays the chimes, the fusion swell and the haptic.")]
        public GameFeedback feedback;
        public WandTrail trail;

        /// <summary>The sign rises with the level name (or the sequence was skipped): the HUD title celebrates.</summary>
        public event Action Arrived;
        /// <summary>The sequence is over: the win panel / next level may follow.</summary>
        public event Action Finished;
        public bool Playing { get; private set; }

        sealed class Star
        {
            public Transform t;
            public SpriteRenderer sprite, halo, sparkle;
            public TrailRenderer streak;
            public Vector3 home;
            public Color color;
            public float size, ping = -1f, absorb = -1f;
        }

        sealed class Segment
        {
            public LineRenderer core, glow;
        }

        readonly List<Star> stars = new();
        readonly List<Segment> segments = new();
        readonly List<float> shares = new();          // share of the path length at each star
        readonly List<LineRenderer> edges = new();
        readonly List<Color> edgeColors = new();
        readonly Sprite[] glyphs = new Sprite[Zodiac.SignCount], softGlyphs = new Sprite[Zodiac.SignCount];
        readonly SpriteRenderer[] rings = new SpriteRenderer[2], heroRings = new SpriteRenderer[2];
        readonly float[] ringStart = { -1f, -1f };
        SpriteRenderer[] heroSparkles;

        Transform root, flight, hero;
        SpriteRenderer figureHalo, pulse, pulseSparkle, orb, orbGlow, orbCore, core, flash, glyph, dim;
        SpriteRenderer heroGlyph, heroBlur, heroHalo, heroRays;
        TrailRenderer orbTrail;
        ParticleSystem puff;
        RayBurst rays;
        Camera cam;

        // name highlight
        RectTransform nameBlock, signPart, namePart;
        CanvasGroup nameGroup, signGroup, namePartGroup;
        SignLine signLine;
        TextMeshProUGUI nameText;
        Vector2 signBase, nameBase;
        bool shimmerCleared;

        int count, sign, number, absorbed;
        Vector3 centroid, fusion, glideFrom, heroStart, heroEnd;
        float radius, intensity, flareTime, mergeTime, riseTotal, heroScale0, heroScale1, exitDim, carry;
        bool boss, reduced, skip, abort, arrivedSent, emergePending;
        Color gold;
        Tween emergeTween;
        Action<float> recognition, trace, merge, glide, flare, rise, reducedFlare, nameOnly, exit;

        bool Stopped => skip || abort;
        float NameTime => settings.shimmerDelay + settings.shimmerTime + settings.nameHoldTime;

        void Awake()
        {
            cam = Camera.main;
            recognition = Recognition;
            trace = Trace;
            merge = Merge;
            glide = Glide;
            flare = Flare;
            rise = Rise;
            reducedFlare = ReducedFlare;
            nameOnly = k => NameHighlight(k * NameTime);
            exit = Exit;
        }

        void OnEnable()
        {
            if (!pathManager) return;
            pathManager.Completed += OnCompleted;
            pathManager.BoardBuilt += OnBoardBuilt;
        }

        void OnDisable()
        {
            if (!pathManager) return;
            pathManager.Completed -= OnCompleted;
            pathManager.BoardBuilt -= OnBoardBuilt;
        }

        void OnCompleted()
        {
            if (!settings || !style || pathManager.Path.Count == 0)
            {
                Arrived?.Invoke();
                Finished?.Invoke();
                return;
            }
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        // ---------- the sequence ----------

        IEnumerator Run()
        {
            Playing = true;
            skip = abort = arrivedSent = false;
            reduced = SaveService.ReduceMotion;
            carry = 0f;
            Capture();
            yield return Phase(settings.recognitionTime, recognition);
            if (reduced)
            {
                if (!Stopped) { BeginReduced(); yield return Phase(settings.reduceMotionTime, reducedFlare); }
                if (!Stopped)
                {
                    BeginName(pathManager.BoardPivot.TransformPoint(fusion), settings.glyphSize);
                    yield return Phase(NameTime, nameOnly);
                }
            }
            else
            {
                if (!Stopped) yield return Phase(settings.traceTime, trace);
                if (!Stopped) { BeginMerge(); yield return Phase(mergeTime, merge); }
                if (!Stopped) { BeginGlide(); yield return Phase(settings.glideTime, glide); }
                if (!Stopped) { BeginFlare(); yield return Phase(flareTime, flare); }
                if (!Stopped) { BeginRise(); yield return Phase(riseTotal, rise); }
            }
            if (!Stopped) { BeginExit(); yield return Phase(settings.exitTime, exit); }
            End();
        }

        // Runs step(0..1) over the duration, one call per frame; a tap skips, leaving the game screen aborts.
        // A phase's overshoot past its end (up to a frame) is carried into the next, so the total stays exact.
        IEnumerator Phase(float duration, Action<float> step)
        {
            float t = carry;
            while (t < duration)
            {
                step(t / duration);
                yield return null;
                if (Interrupted()) yield break;
                t += Time.deltaTime;
            }
            carry = t - duration;
            step(1f);
        }

        bool Interrupted()
        {
            if (pathManager.suspended) abort = true;
            else
            {
                var pointer = Pointer.current;
                if (pointer != null && pointer.press.wasPressedThisFrame) skip = true; // never block an impatient player
            }
            return Stopped;
        }

        void End()
        {
            HideAll();
            if (Stopped)
            {
                if (rays) rays.FadeOut(0.15f);
                ringStart[0] = ringStart[1] = -1f;
                foreach (var r in rings) r.color = Color.clear;
                orbTrail.emitting = false;
                orbTrail.Clear();
            }
            pathManager.SetCameraEffects(1f, Vector2.zero);
            SetDim(0f);
            Playing = false;
            emergePending = !abort;
            if (abort) return;
            if (!arrivedSent) { arrivedSent = true; Arrived?.Invoke(); }
            Finished?.Invoke();
        }

        // ---------- 1. recognition ----------

        void Recognition(float k)
        {
            float e = Ease.OutQuad(k);
            for (int i = 0; i < count; i++)
            {
                var s = stars[i];
                s.sprite.color = Bright(s, e);
                s.t.localScale = Vector3.one * (s.size * (1f + 0.1f * e));
                s.halo.color = UIKit.WithAlpha(s.color, 0.3f + 0.15f * e);
            }
            SetLineWidths(1f + (settings.lineThicken - 1f) * e);
            figureHalo.color = UIKit.WithAlpha(gold, settings.haloAlpha * e);
            for (int j = 0; j < edges.Count; j++)
            {
                if (!edges[j]) continue;
                var c = edgeColors[j];
                c.a *= 1f - e; // the unused dashed connections fade away
                edges[j].startColor = edges[j].endColor = c;
            }
        }

        Color Bright(Star s, float amount) => Color.Lerp(s.color, Color.white, settings.brighten * amount);

        void SetLineWidths(float scale)
        {
            for (int i = 0; i < count - 1; i++)
            {
                segments[i].core.widthMultiplier = style.trailWidth * scale;
                segments[i].glow.widthMultiplier = style.trailWidth * style.trailGlowWidth * scale;
            }
        }

        // ---------- 2. trace pulse ----------

        void Trace(float k)
        {
            float along = Ease.InOutSine(Mathf.Clamp01(k / 0.85f)); // reaches the last star a little early
            pulse.enabled = pulseSparkle.enabled = true;
            pulse.transform.localPosition = PointAlong(along) + Vector3.back * 0.05f;
            float a = k < 0.85f ? 1f : 1f - (k - 0.85f) / 0.15f;
            pulse.color = UIKit.WithAlpha(Color.Lerp(gold, Color.white, 0.6f), 0.9f * a);
            pulse.transform.localScale = Vector3.one * (settings.pulseSize * intensity);
            pulseSparkle.color = UIKit.WithAlpha(Color.white, a);
            pulseSparkle.transform.localRotation = Quaternion.Euler(0f, 0f, k * 180f);

            for (int i = 0; i < count; i++)
            {
                var s = stars[i];
                if (s.ping < 0f && shares[i] <= along + 1e-4f)
                {
                    s.ping = Time.time;
                    if (feedback) feedback.PlayChime(i, count);
                }
                Ping(s);
            }
        }

        // Scale punch + a tiny sparkle when the pulse passes a star.
        void Ping(Star s)
        {
            float punch = 0f;
            if (s.ping >= 0f)
            {
                float u = (Time.time - s.ping) / settings.pingTime;
                if (u < 1f) punch = Mathf.Sin(Mathf.PI * u);
                Sparkle(s, punch, u);
            }
            s.t.localScale = Vector3.one * (s.size * 1.1f * (1f + (settings.pingScale - 1f) * punch));
            s.sprite.color = Color.Lerp(Bright(s, 1f), Color.white, punch * 0.6f);
        }

        // The star's sparkle keeps a constant world size while the star itself scales.
        void Sparkle(Star s, float amount, float turn)
        {
            s.sparkle.color = UIKit.WithAlpha(Color.white, amount);
            s.sparkle.transform.localScale = Vector3.one * (settings.sparkleSize * (0.4f + amount) / Mathf.Max(0.01f, s.t.localScale.x));
            s.sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp01(turn) * 90f);
        }

        Vector3 PointAlong(float share)
        {
            for (int i = 1; i < count; i++)
            {
                if (share > shares[i] && i < count - 1) continue;
                float span = shares[i] - shares[i - 1];
                return Vector3.Lerp(stars[i - 1].home, stars[i].home, span > 0f ? Mathf.Clamp01((share - shares[i - 1]) / span) : 1f);
            }
            return count > 0 ? stars[0].home : centroid;
        }

        // ---------- 3. merge along the drawn path ----------

        void BeginMerge()
        {
            pulse.enabled = pulseSparkle.enabled = false;
            orb.enabled = orbGlow.enabled = orbCore.enabled = true;
            PlaceOrb(stars[0].home, 0);
            orbTrail.Clear();
            orbTrail.emitting = true;
            absorbed = 0;
            Absorb();
        }

        // The orb takes the next star: it slides in with a ping and a chime a little higher than the last.
        void Absorb()
        {
            var s = stars[absorbed];
            s.absorb = Time.time;
            s.streak.Clear();
            s.streak.startColor = UIKit.WithAlpha(Color.Lerp(s.color, Color.white, 0.3f), 0.8f);
            s.streak.endColor = UIKit.WithAlpha(s.color, 0f);
            s.streak.emitting = true;
            if (feedback) feedback.PlayChime(absorbed, count);
            absorbed++;
        }

        void Merge(float k)
        {
            // Progress in segments (1 = the second star), through the acceleration curve.
            float p = count > 1 ? Mathf.Clamp01(settings.mergeCurve.Evaluate(k)) * (count - 1) : 0f;
            int i = Mathf.Min((int)p, Mathf.Max(0, count - 2));
            float f = Mathf.Clamp01(p - i);
            var at = count > 1 ? Vector3.Lerp(stars[i].home, stars[i + 1].home, f) : stars[0].home;
            while (absorbed < count && absorbed <= p + 1e-3f) Absorb();
            PlaceOrb(at, absorbed);
            UpdateStars(at);

            // The line is reeled in: segments behind the orb are gone, the one it runs along starts at the orb.
            for (int q = 0; q < count - 1; q++)
            {
                var seg = segments[q];
                if (q < i || (q == i && f >= 1f))
                {
                    if (seg.core.gameObject.activeSelf) seg.core.gameObject.SetActive(false);
                    continue;
                }
                if (q == i) SetSegment(seg, Flat(at), Flat(stars[q + 1].home));
            }
            figureHalo.color = UIKit.WithAlpha(gold, settings.haloAlpha * (1f - k));
            float z = Ease.InOutSine(k);
            pathManager.SetCameraEffects(Mathf.Lerp(1f, settings.cameraZoom, z), Vector2.zero);
            SetDim(settings.backgroundDim * z);
        }

        // Stars not reached yet keep the trace's glow; absorbed ones slide into the orb and vanish.
        void UpdateStars(Vector3 orbAt)
        {
            for (int j = 0; j < count; j++)
            {
                var s = stars[j];
                if (s.absorb < 0f) { Ping(s); continue; }
                if (!s.sprite.enabled) continue;
                float u = (Time.time - s.absorb) / settings.absorbTime;
                if (u >= 1f)
                {
                    s.sprite.enabled = false;
                    s.halo.color = s.sparkle.color = Color.clear;
                    s.streak.emitting = false; // what's left of the streak fades on its own
                    continue;
                }
                float e = Ease.OutQuad(u), pop = Mathf.Sin(Mathf.PI * u);
                s.t.localPosition = Vector3.Lerp(s.home, orbAt, e);
                s.t.localScale = Vector3.one * Mathf.Max(0.001f, s.size * 1.1f * (1f - e) * (1f + 0.4f * pop));
                s.sprite.color = Color.Lerp(Bright(s, 1f), Color.white, pop * 0.7f);
                s.halo.color = UIKit.WithAlpha(s.color, 0.45f * (1f - e));
                Sparkle(s, pop, u);
            }
        }

        // The orb grows and whitens with every star it holds.
        void PlaceOrb(Vector3 at, int held)
        {
            float share = count > 0 ? held / (float)count : 1f;
            orb.transform.localPosition = at + Vector3.back * 0.12f;
            orb.transform.localScale = Vector3.one * (settings.orbSize * Mathf.Sqrt(intensity) * (1f + settings.orbGrowth * share));
            orb.color = Color.Lerp(gold, Color.white, 0.5f + 0.5f * share);
            orbGlow.color = UIKit.WithAlpha(Color.Lerp(gold, Color.white, 0.25f), 0.5f + 0.3f * share);
            orbCore.color = Color.white;
            orbCore.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
        }

        void SetSegment(Segment seg, Vector3 a, Vector3 b)
        {
            seg.core.SetPosition(0, a);
            seg.core.SetPosition(1, b);
            seg.glow.SetPosition(0, a);
            seg.glow.SetPosition(1, b);
        }

        static Vector3 Flat(Vector3 p) => new(p.x, p.y, 0f);

        float FigureHaloSize => (2f * radius + 1f) * settings.haloScale;

        // ---------- 4. glide to the fusion point ----------

        void BeginGlide()
        {
            glideFrom = stars[count - 1].home;
            // The centroid, pulled toward the middle of the screen (where the view's center meets the board).
            var center = centroid;
            var pivot = pathManager.BoardPivot;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (new Plane(pivot.forward, pivot.position).Raycast(ray, out float d))
            {
                center = pivot.InverseTransformPoint(ray.GetPoint(d));
                center.z = centroid.z;
            }
            fusion = Vector3.Lerp(centroid, center, settings.fusionCenterBias);
        }

        void Glide(float k)
        {
            var at = Vector3.Lerp(glideFrom, fusion, Ease.InOutSine(k));
            PlaceOrb(at, count);
            UpdateStars(at);
        }

        // ---------- 5. fusion + flare ----------

        void BeginFlare()
        {
            for (int i = 0; i < count; i++)
            {
                var s = stars[i];
                s.sprite.enabled = false;
                s.halo.color = s.sparkle.color = Color.clear;
                s.streak.emitting = false;
            }
            for (int i = 0; i < segments.Count; i++) segments[i].core.gameObject.SetActive(false);
            figureHalo.color = Color.clear;
            orb.enabled = orbGlow.enabled = orbCore.enabled = false;
            orbTrail.emitting = false;
            PlaceFlare(fusion);

            // The completion burst's rays, from the new star, in the path's own (skin) color.
            var rayColor = count > 0 ? stars[count - 1].color : gold;
            rays.Play(fusion, rayColor, Mathf.RoundToInt(style.rayCount * intensity), settings.rayLengthScale * intensity,
                0.5f * intensity);
            ringStart[1] = boss ? Time.time + settings.secondRingDelay : -1f;
            if (feedback) feedback.PlayFusion(intensity);
        }

        void PlaceFlare(Vector3 at)
        {
            core.enabled = flash.enabled = glyph.enabled = true;
            core.transform.localPosition = at + Vector3.back * 0.1f;
            flash.transform.localPosition = at + Vector3.back * 0.05f;
            glyph.transform.localPosition = at + Vector3.back * 0.15f;
            glyph.sprite = GlyphSprite(sign);
            foreach (var r in rings) r.transform.localPosition = at;
            ringStart[0] = Time.time;
        }

        void Flare(float k)
        {
            float size = settings.coreSize * intensity;
            float s = k < 0.25f ? Mathf.Lerp(0.2f, 1.6f, Ease.OutQuad(k / 0.25f)) : Mathf.Lerp(1.6f, 1f, Ease.OutCubic((k - 0.25f) / 0.75f));
            core.transform.localScale = Vector3.one * (size * s);
            core.color = Color.Lerp(Color.white, gold, 0.2f + 0.3f * k);

            float f = k < 0.12f ? k / 0.12f : 1f - Ease.OutCubic((k - 0.12f) / 0.88f);
            flash.color = UIKit.WithAlpha(Color.Lerp(Color.white, gold, 0.4f), settings.flashAlpha * f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, settings.flashSize * intensity, Ease.OutCubic(k));

            // The house's glyph lights up inside the flare (and stays: it becomes the rising sign).
            glyph.color = UIKit.WithAlpha(gold, settings.glyphAlpha * Ease.OutQuad(Mathf.Clamp01(k * 2f)));
            glyph.transform.localScale = Vector3.one * (settings.glyphSize * intensity * (0.85f + 0.3f * k));

            float worldPerPixel = 2f * cam.orthographicSize / Mathf.Max(1, Screen.height);
            var shake = UnityEngine.Random.insideUnitCircle * (settings.shakePixels * worldPerPixel * intensity * (1f - k));
            pathManager.SetCameraEffects(settings.cameraZoom, shake);
            SetDim(settings.backgroundDim);
        }

        // ---------- 6. the sign rises, the name lights up ----------

        void BeginRise()
        {
            heroStart = glyph.transform.position;
            heroScale0 = glyph.transform.lossyScale.x;
            // The final size ignores the flare's HARD/BOSS scaling (else a BOSS sign would compound it), capped to the view.
            heroScale1 = Mathf.Min(settings.glyphSize * 1.15f * settings.riseScale * (boss ? settings.bossRiseScale : 1f),
                2f * cam.orthographicSize * settings.maxRiseViewShare);
            // Up the screen; with a perspective camera also toward it.
            heroEnd = heroStart + cam.transform.up * settings.riseHeight;
            if (!cam.orthographic) heroEnd += (cam.transform.position - heroStart).normalized * settings.riseDepth;
            glyph.enabled = false;

            hero.gameObject.SetActive(true);
            hero.rotation = cam.transform.rotation; // faces the viewer, not the tilted board
            heroGlyph.sprite = GlyphSprite(sign);
            heroBlur.sprite = SoftGlyphSprite(sign);
            heroRings[1].enabled = boss;
            riseTotal = Mathf.Max(settings.riseTime, settings.shimmerDelay + settings.shimmerTime) + settings.nameHoldTime;
            exitDim = settings.riseDim;
            ApplyHero(0f, 1f);
            BeginName(heroEnd, heroScale1);
        }

        void Rise(float k)
        {
            float t = k * riseTotal;
            ApplyHero(t, 1f);
            NameHighlight(t);
            // The flare's star fades behind the rising sign.
            float c = Ease.OutQuad(Mathf.Clamp01(t / 0.3f));
            core.color = UIKit.WithAlpha(core.color, 1f - c);
            core.transform.localScale = Vector3.one * (settings.coreSize * intensity * (1f - 0.5f * c));
            SetDim(Mathf.Lerp(settings.backgroundDim, settings.riseDim, Ease.OutQuad(Mathf.Clamp01(t / settings.riseTime))));
        }

        // The rising glyph and its glow at time t since the rise began; fade = 1 while showing, down to 0 on exit.
        void ApplyHero(float t, float fade)
        {
            float r = Mathf.Clamp01(t / settings.riseTime);
            hero.localScale = Vector3.one * Mathf.LerpUnclamped(heroScale0, heroScale1, Ease.OutBack(r));
            hero.position = Vector3.Lerp(heroStart, heroEnd, Ease.OutCubic(r));
            float appear = Ease.OutQuad(Mathf.Clamp01(t / 0.25f)) * fade;
            float pulseAmount = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / settings.glowPulsePeriod);

            heroGlyph.color = UIKit.WithAlpha(Color.Lerp(gold, Color.white, 0.2f + 0.35f * pulseAmount), fade);
            heroBlur.color = UIKit.WithAlpha(gold, (0.35f + 0.2f * pulseAmount) * fade);
            heroHalo.color = UIKit.WithAlpha(gold, (0.22f + 0.12f * pulseAmount) * appear);
            heroRays.color = UIKit.WithAlpha(gold, settings.raysAlpha * appear);
            heroRays.transform.localRotation = Quaternion.Euler(0f, 0f, t * settings.raysSpin);
            for (int j = 0; j < heroRings.Length; j++)
            {
                if (!heroRings[j].enabled) continue;
                float breathe = 1f + 0.06f * Mathf.Sin(t * 2f * Mathf.PI / settings.glowPulsePeriod + j * Mathf.PI);
                heroRings[j].transform.localScale = Vector3.one * ((1.3f + 0.3f * j) * breathe);
                heroRings[j].color = UIKit.WithAlpha(gold, (0.45f - 0.15f * j) * appear);
            }
            for (int j = 0; j < heroSparkles.Length; j++)
            {
                float a = (t * settings.sparkleOrbitSpeed + j * 360f / heroSparkles.Length) * Mathf.Deg2Rad;
                float twinkle = 0.5f + 0.5f * Mathf.Sin(t * 5f + j * 1.7f);
                var sp = heroSparkles[j];
                sp.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.8f, -0.01f) * 0.95f;
                sp.transform.localScale = Vector3.one * (0.14f * (0.7f + 0.5f * twinkle));
                sp.transform.localRotation = Quaternion.Euler(0f, 0f, t * 120f);
                sp.color = UIKit.WithAlpha(Color.white, appear * (0.4f + 0.6f * twinkle));
            }
        }

        // "♉ TAURUS" and the level name, just under the sign; the HUD title celebrates at the same moment.
        void BeginName(Vector3 anchor, float glyphWorldSize)
        {
            arrivedSent = true;
            Arrived?.Invoke();
            if (!nameBlock) return;
            nameGroup.alpha = 1f;
            signGroup.alpha = namePartGroup.alpha = 0f;
            nameBlock.anchoredPosition = Vector2.zero;
            signLine.Set(sign, Zodiac.UpperNames[sign]);
            nameText.text = ui.LevelTitle(number);
            nameText.ForceMeshUpdate();
            UIKit.SetGlowColor(nameText, UIKit.WithAlpha(gold, 0.2f));
            shimmerCleared = false;

            // Just below the glow ring(s) around the glyph (radius ~0.65 of the glyph's size, ~0.8 with BOSS's second).
            var screen = cam.WorldToScreenPoint(anchor - cam.transform.up * glyphWorldSize * (boss && !reduced ? 0.85f : 0.7f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(nameBlock, screen, null, out var local);
            float signY = local.y - settings.nameGap - settings.signTextSize * 0.8f;
            float nameY = signY - settings.signTextSize * 0.9f - settings.nameTextSize * 0.65f;
            // Keep it clear of the bottom buttons.
            float floor = -nameBlock.rect.height * 0.5f + ui.bottomBand + settings.nameTextSize * 0.6f;
            if (nameY < floor) { signY += floor - nameY; nameY = floor; }
            signBase = new Vector2(0f, signY);
            nameBase = new Vector2(0f, nameY);
        }

        void NameHighlight(float t)
        {
            if (!nameBlock) return;
            Appear(signPart, signGroup, signBase, t);
            Appear(namePart, namePartGroup, nameBase, t - settings.nameStagger);

            // A band of gold light sweeps across, then a soft glow holds.
            float s = (t - settings.shimmerDelay) / settings.shimmerTime;
            var shine = Color.Lerp(gold, Color.white, 0.6f);
            if (s >= 0f && s <= 1f)
            {
                float sweep = Mathf.Lerp(-0.3f, 1.3f, s);
                UIKit.Shimmer(nameText, sweep, shine, ui.shimmerWidth);
                UIKit.Shimmer(signLine.Label, sweep, shine, ui.shimmerWidth);
            }
            else if (s > 1f && !shimmerCleared)
            {
                shimmerCleared = true;
                nameText.ForceMeshUpdate();
                signLine.Label.ForceMeshUpdate();
            }
            UIKit.SetGlowColor(nameText, UIKit.WithAlpha(gold, Mathf.Lerp(0.2f, 0.65f, Mathf.Clamp01((s - 0.6f) / 0.6f))));
        }

        void Appear(RectTransform part, CanvasGroup group, Vector2 at, float t)
        {
            float e = Ease.OutCubic(Mathf.Clamp01(t / settings.nameFadeTime));
            group.alpha = e;
            part.anchoredPosition = at + new Vector2(0f, -settings.nameRise * (1f - e));
        }

        // ---------- 7. exit ----------

        void BeginExit()
        {
            if (!hero.gameObject.activeSelf) return;
            // The sign dissolves into a puff of gold stardust drifting up.
            int n = Mathf.RoundToInt(settings.puffCount * (boss ? 1.5f : 1f));
            float spread = hero.localScale.x * 0.35f;
            var up = cam.transform.up;
            for (int i = 0; i < n; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = hero.position + (Vector3)(UnityEngine.Random.insideUnitCircle * spread),
                    velocity = up * (settings.puffSpeed * UnityEngine.Random.Range(0.5f, 1.2f))
                               + (Vector3)(UnityEngine.Random.insideUnitCircle * settings.puffSpeed * 0.4f),
                    startColor = Color.Lerp(gold, Color.white, UnityEngine.Random.value * 0.5f),
                    startSize = UnityEngine.Random.Range(0.04f, 0.11f),
                    startLifetime = UnityEngine.Random.Range(0.5f, 0.9f),
                };
                puff.Emit(p, 1);
            }
        }

        void Exit(float k)
        {
            float e = Ease.InOutSine(k);
            if (hero.gameObject.activeSelf)
            {
                ApplyHero(riseTotal + k * settings.exitTime, 1f - e);
                hero.localScale *= 1f + 0.12f * e;
                hero.position += cam.transform.up * (0.2f * e);
            }
            if (nameBlock)
            {
                nameGroup.alpha = 1f - e;
                nameBlock.anchoredPosition = new Vector2(0f, settings.nameRise * 0.5f * e);
            }
            pathManager.SetCameraEffects(Mathf.Lerp(reduced ? 1f : settings.cameraZoom, 1f, e), Vector2.zero);
            SetDim(exitDim * (1f - e));
        }

        // ---------- reduce motion: a short flare where the figure is, then the name ----------

        void BeginReduced()
        {
            fusion = centroid;
            PlaceFlare(fusion);
            exitDim = 0f;
            if (feedback) feedback.PlayFusion(1f);
        }

        void ReducedFlare(float k)
        {
            float fade = 1f - Ease.OutQuad(k);
            for (int i = 0; i < count; i++)
            {
                var s = stars[i];
                s.sprite.color = UIKit.WithAlpha(Bright(s, 1f), fade);
                s.halo.color = UIKit.WithAlpha(s.color, 0.45f * fade);
            }
            for (int i = 0; i < count - 1; i++) SetSegmentColors(segments[i], stars[i].color, stars[i + 1].color, fade);
            figureHalo.color = UIKit.WithAlpha(gold, settings.haloAlpha * fade);
            float g = Mathf.Sin(Mathf.PI * k);
            core.color = UIKit.WithAlpha(Color.Lerp(Color.white, gold, 0.3f), g);
            core.transform.localScale = Vector3.one * (settings.coreSize * (0.6f + 0.4f * g));
            flash.color = UIKit.WithAlpha(Color.Lerp(Color.white, gold, 0.4f), settings.flashAlpha * 0.6f * g);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, settings.flashSize * 0.5f, Ease.OutCubic(k));
            glyph.color = UIKit.WithAlpha(gold, settings.glyphAlpha * 0.8f * g);
            glyph.transform.localScale = Vector3.one * settings.glyphSize;
        }

        // ---------- shockwave rings (outlive the flare) ----------

        void Update()
        {
            if (!rings[0]) return;
            for (int j = 0; j < rings.Length; j++)
            {
                if (ringStart[j] < 0f) continue;
                float u = (Time.time - ringStart[j]) / settings.ringTime;
                if (u < 0f) { rings[j].color = Color.clear; continue; }
                if (u >= 1f) { ringStart[j] = -1f; rings[j].color = Color.clear; continue; }
                float size = settings.ringSize * (reduced ? 0.5f : intensity) * (j == 0 ? 1f : 1.35f);
                rings[j].transform.localScale = Vector3.one * Mathf.Lerp(0.3f, size, Ease.OutCubic(u));
                rings[j].color = UIKit.WithAlpha(gold, settings.ringAlpha * Mathf.Pow(1f - u, 1.5f));
            }
        }

        // ---------- next level: the stars grow out of the center ----------

        void OnBoardBuilt()
        {
            if (Playing)
            {
                StopAllCoroutines();
                abort = true;
                End();
            }
            if (rays) rays.FadeOut(0.2f);
            if (!emergePending) return;
            emergePending = false;
            if (!settings || SaveService.ReduceMotion || pathManager.Nodes.Count == 0) return;
            var level = pathManager.Nodes[0].transform.parent;
            emergeTween?.Kill();
            float from = settings.emergeFrom;
            level.localScale = Vector3.one * from;
            emergeTween = Tween.Run(level, settings.emergeTime, Ease.OutCubic,
                k => level.localScale = Vector3.one * Mathf.Lerp(from, 1f, k));
        }

        // ---------- taking over the drawn figure ----------

        void Capture()
        {
            EnsureBuilt();
            var path = pathManager.Path;
            var pivot = pathManager.BoardPivot;
            count = path.Count;
            number = levelManager ? levelManager.CurrentNumber : 1;
            boss = Difficulty.IsBoss(number);
            intensity = boss ? settings.bossScale : Difficulty.IsHard(number) ? settings.hardScale : 1f;
            flareTime = settings.flareTime * (boss ? settings.bossFlareLength : 1f);
            mergeTime = Mathf.Clamp((count - 1) * settings.perSegmentTime, settings.minMergeTime, settings.maxMergeTime);
            sign = Zodiac.SignOf(number);
            gold = theme ? theme.gold : style.finalColor;

            centroid = Vector3.zero;
            for (int i = 0; i < count; i++) centroid += pivot.InverseTransformPoint(path[i].transform.position);
            centroid /= Mathf.Max(1, count);
            fusion = centroid;
            radius = 0f;

            float length = 0f;
            shares.Clear();
            for (int i = 0; i < count; i++)
            {
                var node = path[i];
                var s = StarAt(i);
                s.home = pivot.InverseTransformPoint(node.transform.position);
                s.size = node.transform.localScale.x;
                s.color = node.CurrentColor;
                s.ping = s.absorb = -1f;
                s.t.gameObject.SetActive(true);
                s.t.localPosition = s.home;
                s.t.localScale = Vector3.one * s.size;
                s.sprite.enabled = true;
                s.sprite.color = s.color;
                s.halo.color = UIKit.WithAlpha(s.color, 0.3f);
                s.sparkle.color = Color.clear;
                s.streak.emitting = false;
                s.streak.Clear();
                radius = Mathf.Max(radius, Vector2.Distance(s.home, centroid));
                if (i > 0) length += Vector2.Distance(stars[i - 1].home, s.home);
                shares.Add(length);
            }
            for (int i = 0; i < count; i++) shares[i] = length > 0f ? shares[i] / length : 0f;
            for (int i = count; i < stars.Count; i++) stars[i].t.gameObject.SetActive(false);

            // Segments exactly where the wand trail drew them.
            for (int i = 1; i < count; i++)
            {
                var seg = SegmentAt(i - 1);
                seg.core.gameObject.SetActive(true);
                SetSegment(seg, Flat(stars[i - 1].home), Flat(stars[i].home));
                SetSegmentColors(seg, stars[i - 1].color, stars[i].color, 1f);
            }
            for (int i = Mathf.Max(0, count - 1); i < segments.Count; i++) segments[i].core.gameObject.SetActive(false);
            SetLineWidths(1f);

            figureHalo.transform.localPosition = centroid + Vector3.forward * 0.1f;
            figureHalo.transform.localScale = Vector3.one * FigureHaloSize;
            figureHalo.color = Color.clear;
            core.enabled = flash.enabled = glyph.enabled = false;
            pulse.enabled = pulseSparkle.enabled = false;
            orb.enabled = orbGlow.enabled = orbCore.enabled = false;
            hero.gameObject.SetActive(false);
            if (nameGroup) nameGroup.alpha = 0f;
            exitDim = settings.backgroundDim;

            HideLevelFigure(path[0].transform.parent);
            if (trail) trail.Clear();
        }

        // The level's own stars, glows and shadows step aside for the stand-ins; its dashed connections stay to fade.
        void HideLevelFigure(Transform level)
        {
            edges.Clear();
            edgeColors.Clear();
            foreach (var e in pathManager.EdgeLines)
            {
                edges.Add(e);
                edgeColors.Add(e.startColor);
            }
            for (int i = 0; i < level.childCount; i++)
            {
                var child = level.GetChild(i);
                if (child.TryGetComponent(out LineRenderer line) && edges.Contains(line)) continue;
                if (child.TryGetComponent(out SpriteRenderer sr) && sr.drawMode == SpriteDrawMode.Sliced) continue; // board plate
                child.gameObject.SetActive(false);
            }
        }

        void SetSegmentColors(Segment seg, Color a, Color b, float alpha)
        {
            seg.core.startColor = UIKit.WithAlpha(a, a.a * alpha);
            seg.core.endColor = UIKit.WithAlpha(b, b.a * alpha);
            seg.glow.startColor = UIKit.WithAlpha(a, a.a * style.trailGlowAlpha * alpha);
            seg.glow.endColor = UIKit.WithAlpha(b, b.a * style.trailGlowAlpha * alpha);
        }

        void HideAll()
        {
            for (int i = 0; i < stars.Count; i++) stars[i].t.gameObject.SetActive(false);
            for (int i = 0; i < segments.Count; i++) segments[i].core.gameObject.SetActive(false);
            if (!root) return;
            figureHalo.color = Color.clear;
            core.enabled = flash.enabled = glyph.enabled = false;
            pulse.enabled = pulseSparkle.enabled = false;
            orb.enabled = orbGlow.enabled = orbCore.enabled = false;
            hero.gameObject.SetActive(false);
            if (nameGroup) nameGroup.alpha = 0f;
        }

        void SetDim(float alpha)
        {
            if (!dim) return;
            dim.enabled = alpha > 0.001f;
            if (!dim.enabled) return;
            float h = 2.4f * cam.orthographicSize;
            dim.transform.localScale = new Vector3(h * cam.aspect, h, 1f);
            dim.color = UIKit.WithAlpha(Color.black, alpha);
        }

        // ---------- pools ----------

        void EnsureBuilt()
        {
            if (root) return;
            root = new GameObject("Completion Sequence").transform;
            root.SetParent(pathManager.BoardPivot, false); // tilts with the board
            flight = new GameObject("Completion Rise").transform;
            flight.SetParent(pathManager.transform, false); // the rising sign faces the viewer, not the board

            figureHalo = NewSprite("Figure Halo", root, Art.SoftCircle, 5);
            pulse = NewSprite("Pulse", root, Art.SoftCircle, 25);
            pulseSparkle = NewSprite("Pulse Sparkle", pulse.transform, UIKit.Sparkle, 26);
            pulseSparkle.transform.localScale = Vector3.one * 0.45f;

            orb = NewSprite("Gathering Orb", root, Art.SoftCircle, 32);
            orbGlow = NewSprite("Glow", orb.transform, Art.SoftCircle, 31);
            orbGlow.transform.localScale = Vector3.one * 2.8f;
            orbCore = NewSprite("Core", orb.transform, UIKit.Sparkle, 33);
            orbCore.transform.localScale = Vector3.one * 0.6f;
            orbTrail = NewTrail(orb.gameObject, settings.streakWidth * 2f, settings.streakTime * 1.5f, 30);
            orbTrail.startColor = UIKit.WithAlpha(Color.Lerp(Color.white, style.finalColor, 0.4f), 0.9f);
            orbTrail.endColor = UIKit.WithAlpha(style.finalColor, 0f);

            flash = NewSprite("Fusion Flash", root, Art.SoftCircle, 61);
            core = NewSprite("Fused Star", root, Art.Star, 62);
            glyph = NewSprite("Zodiac Glyph", root, null, 63);
            for (int j = 0; j < rings.Length; j++) rings[j] = NewSprite("Shockwave", root, ThinRing, 58);
            rays = RayBurst.Create(root, style);

            // The rising sign: rays and halo behind, a soft copy, the crisp glyph, glow ring(s), orbiting sparkles.
            hero = new GameObject("Rising Sign").transform;
            hero.SetParent(flight, false);
            heroRays = NewSprite("Rays", hero, RaysSprite, 64);
            heroRays.transform.localScale = Vector3.one * 3.4f;
            heroHalo = NewSprite("Halo", hero, Art.SoftCircle, 65);
            heroHalo.transform.localScale = Vector3.one * 2.6f;
            for (int j = 0; j < heroRings.Length; j++) heroRings[j] = NewSprite("Glow Ring", hero, ThinRing, 66);
            heroBlur = NewSprite("Soft Glyph", hero, null, 67);
            heroBlur.transform.localScale = Vector3.one * 1.25f;
            heroGlyph = NewSprite("Glyph", hero, null, 68);
            heroSparkles = new SpriteRenderer[Mathf.Max(0, settings.orbitSparkles)];
            for (int j = 0; j < heroSparkles.Length; j++) heroSparkles[j] = NewSprite("Sparkle", hero, UIKit.Sparkle, 69);
            hero.gameObject.SetActive(false);
            puff = CreatePuff(flight);

            if (cam)
            {
                // Between the sky (sorting -60..-30) and the board (-8 and up): dims only the background.
                dim = NewSprite("Completion Dim", cam.transform, Art.Square, -20);
                dim.transform.localPosition = new Vector3(0f, 0f, pathManager.BoardPivot.position.z - cam.transform.position.z);
                dim.enabled = false;
            }
            BuildName();
        }

        // Screen-space text under the rising sign: the sign line and the level name, each fading in on its own.
        void BuildName()
        {
            if (!ui) return;
            var canvas = UIKit.MakeCanvas(transform, "Completion Name Canvas", 11, false); // over the HUD, under the panels
            var safe = UIKit.Stretch(canvas.transform, "Safe Area");
            safe.gameObject.AddComponent<SafeArea>();
            nameBlock = UIKit.Stretch(safe, "Name");
            nameGroup = nameBlock.gameObject.AddComponent<CanvasGroup>();
            nameGroup.blocksRaycasts = false;
            nameGroup.alpha = 0f;
            var mid = new Vector2(0.5f, 0.5f);

            signPart = UIKit.Rect(nameBlock, "Sign", mid, Vector2.zero, new Vector2(ui.signLabelBox, settings.signTextSize * 1.6f));
            signGroup = signPart.gameObject.AddComponent<CanvasGroup>();
            signLine = new SignLine(signPart, ui, mid, Vector2.zero, settings.signTextSize, settings.signTextSpacing,
                settings.signTextSize * 1.2f, UIKit.WithAlpha(ui.Gold, 0.95f));

            namePart = UIKit.Rect(nameBlock, "Level Name", mid, Vector2.zero, new Vector2(UIKit.ReferenceResolution.x - 120f, settings.nameTextSize * 1.4f));
            namePartGroup = namePart.gameObject.AddComponent<CanvasGroup>();
            nameText = UIKit.Text(namePart, "Text", UIKit.TitleFont(ui), settings.nameTextSize, ui.text, mid, Vector2.zero,
                namePart.sizeDelta, ui.titleSpacing);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = settings.nameTextSize * 0.55f;
            nameText.fontSizeMax = settings.nameTextSize;
            UIKit.Glow(nameText, UIKit.WithAlpha(ui.Gold, 0.2f), 0.35f);
        }

        Star StarAt(int i)
        {
            while (stars.Count <= i)
            {
                var t = new GameObject("Star").transform;
                t.SetParent(root, false);
                var s = new Star
                {
                    t = t,
                    halo = NewSprite("Halo", t, Art.SoftCircle, 19),
                    sprite = t.gameObject.AddComponent<SpriteRenderer>(),
                    sparkle = NewSprite("Sparkle", t, UIKit.Sparkle, 26),
                };
                bool sdf = style && style.HasStarShader; // same look as the level's stars
                s.sprite.sprite = sdf ? Art.StarQuad : Art.Star;
                s.sprite.sharedMaterial = sdf ? style.MinorMaterial : Art.SpriteMaterial;
                s.sprite.sortingOrder = 20; // like the level's stars
                s.halo.transform.localScale = Vector3.one * 3f;
                s.streak = NewTrail(t.gameObject, settings.streakWidth, settings.streakTime, 18);
                stars.Add(s);
            }
            return stars[i];
        }

        Segment SegmentAt(int i)
        {
            while (segments.Count <= i)
            {
                var line = PathManager.CreateLine("Segment", root, style.trailWidth, Color.white, 10);
                var glow = PathManager.CreateLine("Glow", line.transform, style.trailWidth * style.trailGlowWidth, Color.white, 9);
                glow.sharedMaterial = Art.SoftLineMaterial;
                glow.textureMode = LineTextureMode.Stretch;
                line.positionCount = glow.positionCount = 2;
                segments.Add(new Segment { core = line, glow = glow });
            }
            return segments[i];
        }

        // ---------- procedural sprites (made once) ----------

        // The sign, crisp, with a faint glow around it (also used in the flare).
        Sprite GlyphSprite(int s)
        {
            if (glyphs[s]) return glyphs[s];
            const int size = 256;
            var raster = new AlphaRaster(size, size);
            var c = new Vector2(size * 0.5f, size * 0.5f);
            raster.Glow(c, size * 0.48f, 0.2f);
            raster.Glyph(ZodiacGlyphs.Sign(s), c, size * 0.3f, 11f, 1f, 0f);
            return glyphs[s] = ToSprite(raster, size);
        }

        // A wide, soft copy of the sign: drawn larger behind it, it reads as depth and bloom.
        Sprite SoftGlyphSprite(int s)
        {
            if (softGlyphs[s]) return softGlyphs[s];
            const int size = 128;
            var raster = new AlphaRaster(size, size);
            var c = new Vector2(size * 0.5f, size * 0.5f);
            raster.Glyph(ZodiacGlyphs.Sign(s), c, size * 0.3f, 16f, 0.55f, 0f);
            raster.Glyph(ZodiacGlyphs.Sign(s), c, size * 0.3f, 9f, 0.8f, 0f);
            return softGlyphs[s] = ToSprite(raster, size);
        }

        static Sprite ToSprite(AlphaRaster raster, int size) =>
            Sprite.Create(raster.ToTexture(), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        static Sprite thinRing, raysSprite;

        // A hairline circle for the shockwave and the glow rings (Art.Ring is too heavy at this size).
        static Sprite ThinRing
        {
            get
            {
                if (thinRing) return thinRing;
                const int size = 512;
                float half = size * 0.5f, radius = half - 4f, stroke = 2.5f;
                return thinRing = Procedural(size, (x, y) =>
                    Mathf.Clamp01(stroke * 0.5f + 0.5f - Mathf.Abs(new Vector2(x - half, y - half).magnitude - radius)));
            }
        }

        // Twelve soft light rays fading outward, turned slowly behind the rising sign.
        static Sprite RaysSprite
        {
            get
            {
                if (raysSprite) return raysSprite;
                const int size = 256;
                float half = size * 0.5f;
                return raysSprite = Procedural(size, (x, y) =>
                {
                    var p = new Vector2(x - half, y - half) / half;
                    float r = p.magnitude;
                    if (r >= 1f) return 0f;
                    float ray = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(Mathf.Atan2(p.y, p.x) * 6f)), 14f);
                    return ray * Mathf.Pow(1f - r, 1.6f) * Mathf.Clamp01(r * 5f);
                });
            }
        }

        static Sprite Procedural(int size, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = Art.SpriteMaterial;
            sr.sortingOrder = order;
            sr.color = Color.clear;
            return sr;
        }

        static TrailRenderer NewTrail(GameObject host, float width, float time, int order)
        {
            var tr = host.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Art.SpriteMaterial;
            tr.time = time;
            tr.widthMultiplier = width;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.minVertexDistance = 0.02f;
            tr.numCapVertices = 4;
            tr.sortingOrder = order;
            tr.emitting = false;
            return tr;
        }

        // Gold stardust for the sign's exit: explicit Emit() calls only, each mote slowing and fading.
        static ParticleSystem CreatePuff(Transform parent)
        {
            var go = new GameObject("Stardust Puff");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.06f;
            limit.limit = 0f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Sprites;
            tsa.SetSprite(0, Art.SoftCircle);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.SpriteMaterial;
            r.sortingOrder = 70;
            ps.Play();
            return ps;
        }
    }
}
