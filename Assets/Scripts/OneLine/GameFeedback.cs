using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Feedback, kept restrained: the star-collect sound (pooled, slight random pitch; a soft procedural tick without a
    /// clip) and a barely-there screen punch on each correct move, a small
    /// shake on a rejected move, and ONE big moment on level completion. With a CompletionSequence the win is its
    /// "constellation fusion" (this class plays its chimes, swell and haptic); without one, a burst of thin light
    /// rays from the final star plus a warm golden flash. Also the timed levels' sounds (hourglass tick, time up,
    /// Saturn's Gift chime, Lunar Stillness shimmer). Sounds are procedural placeholders if left empty.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class GameFeedback : MonoBehaviour
    {
        public PathManager pathManager;
        public StarStyle style;

        [Header("Star collect")]
        [Tooltip("Plays on every star touched, each with its own slight random pitch. Replaces the connect tick.")]
        public AudioClip starCollectClip;
        [Range(0f, 1f)] public float starCollectVolume = 1f;
        public Vector2 starCollectPitch = new(0.95f, 1.05f);

        [Header("Sounds (optional, placeholders are generated if empty)")]
        public AudioClip connectClip;
        public AudioClip winClip;
        public AudioClip failClip;
        [Tooltip("Each star pinging as the light pulse passes it (pitch rises along the path).")]
        public AudioClip chimeClip;
        [Tooltip("The warm swell as the constellation fuses into one star.")]
        public AudioClip fusionClip;

        [Header("Timed levels (optional, placeholders are generated if empty)")]
        public AudioClip tickClip;
        public AudioClip timeUpClip;
        [Tooltip("Saturn's Gift: a soft, deep chime.")]
        public AudioClip saturnClip;
        [Tooltip("Lunar Stillness: a cool, airy shimmer.")]
        public AudioClip lunarClip;

        [Tooltip("Plays the level-complete animation; the win's sounds and haptic then follow its phases.")]
        public CompletionSequence sequence;

        [Header("Extra win particles (optional, e.g. an imported prefab)")]
        public ParticleSystem winBurst;

        AudioSource audioSource, timerSource;
        readonly AudioSource[] chimeSources = new AudioSource[4]; // own pitch each, so quick chimes don't bend each other
        int nextChime;
        readonly AudioSource[] collectSources = new AudioSource[6]; // quick taps overlap instead of cutting each other
        int nextCollect;
        float lastChime = -1f;
        Camera cam;
        Tween punch, shake;
        float zoom = 1f;
        Vector2 offset;
        RayBurst burst;

        void OnEnable() { if (pathManager) pathManager.BoardBuilt += FadeOutBurst; }
        void OnDisable() { if (pathManager) pathManager.BoardBuilt -= FadeOutBurst; }

        // A new level appeared: let a leftover completion burst fade out quickly instead of lingering over it.
        void FadeOutBurst()
        {
            if (burst) burst.FadeOut(0.2f);
        }

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            AudioService.RouteSfx(audioSource);
            cam = Camera.main;
            if (!connectClip) connectClip = Tone("connect", 0.06f, 880f, 1040f, 0.18f);
            if (!winClip) winClip = Arpeggio("win", new[] { 523f, 659f, 784f, 1047f }, 0.09f);
            if (!failClip) failClip = Tone("fail", 0.12f, 240f, 160f, 0.25f);
            if (!chimeClip) chimeClip = Bell("chime", 1318.5f, 0.5f, 0.16f);
            if (!fusionClip) fusionClip = Swell("fusion", new[] { 261.6f, 392f, 523.3f, 659.3f, 1046.5f }, 1.6f);
            if (!tickClip) tickClip = Tone("tick", 0.035f, 2100f, 1700f, 0.07f);
            if (!timeUpClip) timeUpClip = Tone("time-up", 0.45f, 330f, 120f, 0.22f);
            if (!saturnClip) saturnClip = Bell("saturn", 196f, 1.6f, 0.3f);
            if (!lunarClip) lunarClip = Swell("lunar", new[] { 880f, 1318.5f, 1760f }, 1.4f);
            timerSource = gameObject.AddComponent<AudioSource>(); // own pitch: never bent by the connect ticks
            timerSource.playOnAwake = false;
            AudioService.RouteSfx(timerSource);
            for (int i = 0; i < chimeSources.Length; i++)
            {
                chimeSources[i] = gameObject.AddComponent<AudioSource>();
                chimeSources[i].playOnAwake = false;
                AudioService.RouteSfx(chimeSources[i]);
            }
            if (!starCollectClip) return;
            if (starCollectClip.loadState == AudioDataLoadState.Unloaded) starCollectClip.LoadAudioData();
            for (int i = 0; i < collectSources.Length; i++)
            {
                collectSources[i] = gameObject.AddComponent<AudioSource>();
                collectSources[i].playOnAwake = false;
                AudioService.RouteSfx(collectSources[i]);
            }
        }

        // Round-robin over the pool: a source's own pitch only bends its oldest, already fading, sound.
        void PlayStarCollect()
        {
            if (!AudioService.SfxOn) return;
            var source = collectSources[nextCollect++ % collectSources.Length];
            source.pitch = UnityEngine.Random.Range(starCollectPitch.x, starCollectPitch.y);
            source.PlayOneShot(starCollectClip, starCollectVolume);
        }

        /// <summary>A soft chime for star <paramref name="index"/> of <paramref name="count"/>, rising along the path.</summary>
        public void PlayChime(int index, int count)
        {
            if (!AudioService.SfxOn || Time.time - lastChime < 0.03f) return; // long paths: don't smear into noise
            lastChime = Time.time;
            var source = chimeSources[nextChime++ % chimeSources.Length];
            float along = count > 1 ? index / (count - 1f) : 1f;
            source.pitch = Mathf.Pow(2f, along * 7f / 12f); // up to a fifth
            source.PlayOneShot(chimeClip);
        }

        /// <summary>The hourglass tick of the last seconds; <paramref name="strong"/> for the very last ones.</summary>
        public void PlayTimerTick(bool strong) => PlayTimer(tickClip, strong ? 1f : 0.6f);
        public void PlayTimeUp()
        {
            PlayTimer(timeUpClip, 1f);
            Vibrate();
        }
        public void PlaySaturn() => PlayTimer(saturnClip, 1f);
        public void PlayLunar() => PlayTimer(lunarClip, 0.8f);

        void PlayTimer(AudioClip clip, float volume)
        {
            if (AudioService.SfxOn && timerSource && clip) timerSource.PlayOneShot(clip, volume);
        }

        /// <summary>The fusion: a warm swell and a haptic. <paramref name="intensity"/> is 1 normally, more on BOSS.</summary>
        public void PlayFusion(float intensity)
        {
            audioSource.pitch = 1f / Mathf.Sqrt(Mathf.Max(1f, intensity)); // bigger = a little deeper
            if (AudioService.SfxOn) audioSource.PlayOneShot(fusionClip, Mathf.Clamp01(0.75f + 0.15f * (intensity - 1f)));
            Vibrate();
        }

        static void Vibrate()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (AudioService.HapticsOn) Handheld.Vibrate();
#endif
        }

        /// <param name="step">How many stars are connected so far — the tick rises gently in pitch.</param>
        public void OnConnect(int step)
        {
            if (starCollectClip) PlayStarCollect();
            else
            {
                audioSource.pitch = 1f + step * 0.03f;
                if (AudioService.SfxOn) audioSource.PlayOneShot(connectClip);
            }
            if (!style || !pathManager) return;

            // Barely-there punch: quick in, eased out. No overshoot.
            punch?.Kill();
            float amount = style.movePunch - 1f;
            punch = Tween.Run(this, style.movePunchTime, Ease.Linear, k =>
            {
                float bump = k < 0.25f ? Ease.OutQuad(k / 0.25f) : 1f - Ease.OutCubic((k - 0.25f) / 0.75f);
                zoom = 1f + amount * bump;
                ApplyCamera();
            });
        }

        public void OnFail()
        {
            audioSource.pitch = 1f;
            if (AudioService.SfxOn) audioSource.PlayOneShot(failClip);
            if (!style || !pathManager) return;

            // A few pixels, dying out quickly.
            shake?.Kill();
            float worldPerPixel = 2f * cam.orthographicSize / Mathf.Max(1, Screen.height);
            float amplitude = style.failShakePixels * worldPerPixel;
            shake = Tween.Run(this, style.failShakeTime, Ease.Linear, k =>
            {
                offset = k >= 1f ? Vector2.zero : UnityEngine.Random.insideUnitCircle.normalized * amplitude * (1f - k);
                ApplyCamera();
            });
        }

        public void OnWin(Vector3 position, Color color)
        {
            if (sequence && sequence.isActiveAndEnabled) return; // the fusion sequence plays the win
            audioSource.pitch = 1f;
            if (AudioService.SfxOn) audioSource.PlayOneShot(winClip);
            if (style && pathManager) CompletionBurst(position);
            if (winBurst)
            {
                winBurst.transform.position = position;
                winBurst.Play();
            }
            Vibrate();
        }

        void ApplyCamera() => pathManager.SetCameraEffects(zoom, offset);

        // Thin light rays shooting out of the final star, over a warm radial glow. The one big moment.
        void CompletionBurst(Vector3 worldPosition)
        {
            if (!burst) burst = RayBurst.Create(pathManager.BoardPivot, style);
            burst.Play(pathManager.BoardPivot.InverseTransformPoint(worldPosition), style.finalColor, style.rayCount, 1f, 1f);
        }

        // --- Procedural placeholder sounds ---

        const int SampleRate = 44100;

        static AudioClip Tone(string name, float length, float freqStart, float freqEnd, float volume)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                phase += 2 * Mathf.PI * Mathf.Lerp(freqStart, freqEnd, k) / SampleRate;
                data[i] = Mathf.Sin(phase) * volume * (1f - k) * Mathf.Min(1f, i / 200f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A soft bell: a sine plus an inharmonic partial, fast attack, exponential decay.
        static AudioClip Bell(string name, float freq, float length, float volume)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, i / 90f);
                data[i] = (Mathf.Sin(2 * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-t * 14f))
                          * volume * env;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A warm swell: a chord that blooms in (~0.2 s) with a slow shimmer, then rings out.
        static AudioClip Swell(string name, float[] notes, float length)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f));
                float env = attack * Mathf.Exp(-Mathf.Max(0f, t - 0.2f) * 2.4f);
                float v = 0f;
                for (int j = 0; j < notes.Length; j++)
                {
                    float shimmer = 1f + 0.003f * Mathf.Sin(2 * Mathf.PI * (4f + j) * t); // gentle chorus
                    v += Mathf.Sin(2 * Mathf.PI * notes[j] * shimmer * t) / (1f + j * 0.6f);
                }
                data[i] = v * 0.16f * env;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] notes, float noteLength)
        {
            int per = (int)(SampleRate * noteLength);
            int tail = per * 3;
            var data = new float[per * notes.Length + tail];
            for (int j = 0; j < notes.Length; j++)
            {
                int len = per + (j == notes.Length - 1 ? tail : 0);
                for (int i = 0; i < len; i++)
                {
                    float k = (float)i / len;
                    float env = (1f - k) * Mathf.Min(1f, i / 200f);
                    data[j * per + i] += Mathf.Sin(2 * Mathf.PI * notes[j] * i / SampleRate) * 0.25f * env;
                }
            }
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>
    /// The completion burst: thin light rays shooting out over a warm radial flash, then fading. Pooled — one
    /// instance is reused for every win (GameFeedback, CompletionSequence), so a burst costs no new objects.
    /// </summary>
    public class RayBurst : MonoBehaviour
    {
        const float RaySpriteWidth = 16f / 128f; // Art.Ray is 16x128 px at 128 px per unit

        StarStyle style;
        SpriteRenderer flash;
        readonly List<SpriteRenderer> rays = new();
        readonly List<float> lengths = new();
        int count;
        float length, flashScale, fade = 1f;
        Color warm, rayColor;
        Tween tween, fadeTween;
        Action<float> step;
        Action hide;

        public static RayBurst Create(Transform parent, StarStyle style)
        {
            var go = new GameObject("Completion Burst");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<RayBurst>();
            b.style = style;
            b.flash = NewSprite("Warm Flash", go.transform, Art.SoftCircle, 55);
            b.step = b.Step;
            b.hide = b.Hide;
            go.SetActive(false);
            return b;
        }

        /// <param name="localPosition">In the parent's space (the tilted board).</param>
        /// <param name="lengthScale">Ray length as a share of StarStyle.rayLength.</param>
        public void Play(Vector3 localPosition, Color color, int rayCount, float lengthScale, float flashScale)
        {
            tween?.Kill();
            fadeTween?.Kill();
            fade = 1f;
            transform.localPosition = localPosition;
            gameObject.SetActive(true);
            warm = color;
            rayColor = Color.Lerp(color, Color.white, 0.35f);
            count = Mathf.Max(3, rayCount);
            length = style.rayLength * lengthScale;
            this.flashScale = flashScale;
            while (rays.Count < count)
            {
                rays.Add(NewSprite("Ray", transform, Art.Ray, 60));
                lengths.Add(0f);
            }
            for (int i = 0; i < rays.Count; i++)
            {
                rays[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                rays[i].transform.localRotation = Quaternion.Euler(0, 0, i * 360f / count + UnityEngine.Random.Range(-6f, 6f));
                lengths[i] = length * UnityEngine.Random.Range(0.55f, 1f);
            }
            tween = Tween.Run(this, style.burstTime, Ease.Linear, step, hide);
        }

        /// <summary>Fades a running burst out quickly (a new level appeared, or the sequence was skipped).</summary>
        public void FadeOut(float time)
        {
            if (!gameObject.activeSelf || (fadeTween != null && fadeTween.Alive)) return;
            float from = fade;
            fadeTween = Tween.Run(flash, time, Ease.OutQuad, k => fade = from * (1f - k), hide);
        }

        void Step(float k)
        {
            // Flash: warm glow blooms from the center, then fades.
            float f = k < 0.18f ? Ease.OutQuad(k / 0.18f) : 1f - Ease.OutCubic((k - 0.18f) / 0.82f);
            var fc = warm;
            fc.a = style.flashAlpha * f * fade;
            flash.color = fc;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(1f, style.flashSize * flashScale, Ease.OutCubic(k));

            // Rays: shoot out fast, thin out and fade.
            float grow = Ease.OutCubic(k);
            float alpha = (k < 0.1f ? k / 0.1f : 1f - Ease.InOutSine((k - 0.1f) / 0.9f)) * fade;
            for (int i = 0; i < count; i++)
            {
                rays[i].transform.localScale = new Vector3(
                    style.rayWidth / RaySpriteWidth * (1f - 0.5f * k), Mathf.Max(0.01f, lengths[i] * grow), 1f);
                var rc = rayColor;
                rc.a = alpha;
                rays[i].color = rc;
            }
        }

        void Hide()
        {
            tween?.Kill();
            gameObject.SetActive(false);
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
    }
}
