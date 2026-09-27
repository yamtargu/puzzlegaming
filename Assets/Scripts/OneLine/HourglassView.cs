using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneLine
{
    /// <summary>
    /// The antique celestial hourglass of timed levels (bottom-left, right above the Restart button). Procedural: an antique-gold frame (end
    /// caps, turned posts, the current zodiac glyph engraved in a medallion on the top cap), slender glass bulbs with a
    /// faint sheen and glowing gold stardust — the top bulb drains, the bottom pile grows, a thin sparkling stream falls
    /// through the neck (pooled motes). The top sand's surface dips in the middle, the bottom pile builds a small mound;
    /// both are found by area, so they track the timer exactly. The sand goes gold → amber → soft red as time runs low,
    /// silver-blue while Lunar Stillness slows it; the "0:42" underneath brightens in the last seconds.
    /// Motion: a new level waits with the sand in the bottom bulb; the first touch (LevelTimer.Started) flips it 180°
    /// (ease-in-out-back, a 1 → 1.15 → 1 punch, a small gold burst as it lands) and the sand slides down onto the neck.
    /// Everything that loops (stream, glints, glow) stops while the timer is paused. The last seconds pulse and tick; time up cracks the glass with a
    /// red flash and a small shake; Saturn's Gift pours the sand back up ("+15s"); a win makes the sand glow and sends
    /// a few stardust motes toward the completion animation. Only listens to LevelTimer — never changes it.
    /// </summary>
    public class HourglassView : MonoBehaviour
    {
        const float CapHalfWidth = 58f, CapInner = 74f, CapOuter = 86f, LipTop = 91f, PostX = 50f, MedallionY = 80f, MedallionR = 10f;

        TimerSettings s;
        UIStyle style;
        LevelTimer timer;
        GameFeedback feedback;
        RectTransform flyLayer;
        bool subscribed;

        RectTransform body;
        CanvasGroup group;
        HourglassSand sand;
        VectorGraphic glass, frame, crack, engraving;
        Image redGlow, topGlow, bottomGlow, stream;
        Image[] motes, sparkles, flyers, burstSparks;
        Image burstFlash;
        float[] motePhase;
        Vector2[] sparkleSpot;
        TextMeshProUGUI label, floatText;
        readonly char[] labelChars = new char[6];
        int shownSecond = -1, glyphSign = -1;

        float shown = 1f, slowBlend, glowBoost, redAlpha;
        float settle;         // 1 = the sand still lies against the top cap after the flip, 0 = resting on the neck
        float flipScale = 1f; // the punch during the flip
        float animTime;       // clock of the looping animations; stands still while the timer is paused
        bool refilling, flipping, waiting;
        Tween flipTween, settleTween, burstTween, refillTween, crackTween, slowTween, glowTween, shakeTween, floatTween;

        /// <summary>The rotating part — BoostBar aims Saturn's spiral at it.</summary>
        public RectTransform Body => body;

        /// <summary>
        /// How far the frame reaches from the center while it flips (the cap corners, with the scale punch), in
        /// reference units — the HUD keeps this much room around the center so the flip never leaves the screen.
        /// </summary>
        public float FlipReach => new Vector2(CapHalfWidth, CapOuter).magnitude * s.hourglassSize.y / 190f * Mathf.Max(1f, s.flipPunch);

        /// <summary>How far the "0:42" label hangs below the hourglass rect (reference units).</summary>
        public float LabelDrop => s.timeLabelGap + s.timeLabelSize * (0.6f + 0.65f);

        public static HourglassView Create(RectTransform parent, Vector2 anchor, Vector2 pos, UIStyle style, TimerSettings settings,
            LevelTimer timer, GameFeedback feedback, RectTransform flyLayer)
        {
            var rt = UIKit.Rect(parent, "Hourglass", anchor, pos, settings.hourglassSize);
            var view = rt.gameObject.AddComponent<HourglassView>();
            view.s = settings;
            view.style = style;
            view.timer = timer;
            view.feedback = feedback;
            view.flyLayer = flyLayer;
            view.Build();
            view.Subscribe();
            rt.gameObject.SetActive(false);
            return view;
        }

        void OnEnable() => Subscribe();
        void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            if (subscribed || !timer) return;
            subscribed = true;
            timer.Configured += OnConfigured;
            timer.Started += OnStarted;
            timer.TimeUp += OnTimeUp;
            timer.Extended += OnExtended;
            timer.SlowStarted += OnSlowStarted;
            timer.SlowEnded += OnSlowEnded;
            timer.Won += OnWon;
        }

        void Unsubscribe()
        {
            if (!subscribed || !timer) return;
            subscribed = false;
            timer.Configured -= OnConfigured;
            timer.Started -= OnStarted;
            timer.TimeUp -= OnTimeUp;
            timer.Extended -= OnExtended;
            timer.SlowStarted -= OnSlowStarted;
            timer.SlowEnded -= OnSlowEnded;
            timer.Won -= OnWon;
        }

        /// <summary>Engraves this zodiac sign on the top cap.</summary>
        public void SetSign(int sign)
        {
            if (sign == glyphSign) return;
            glyphSign = sign;
            engraving.Clear();
            var gold = style.Zodiac.gold;
            engraving.Disc(new Vector2(0f, MedallionY), MedallionR, UIKit.WithAlpha(DarkDisc, 0.95f));
            engraving.Circle(new Vector2(0f, MedallionY), MedallionR, 1.2f, gold, 40);
            engraving.Glyph(ZodiacGlyphs.Sign(sign), new Vector2(0f, MedallionY), MedallionR * 0.6f, 1.1f, gold);
        }

        Color DarkDisc => style.starStyle ? style.starStyle.backgroundCenter : new Color(0.07f, 0.09f, 0.16f);

        // ---------- timer events ----------

        void OnConfigured(bool timed)
        {
            gameObject.SetActive(timed);
            if (!timed) return;
            refillTween?.Kill();
            crackTween?.Kill();
            refilling = false;
            crack.color = UIKit.WithAlpha(Color.white, 0f);
            redAlpha = 0f;
            glowBoost = 0f;
            slowBlend = timer.IsSlowed ? 1f : 0f;
            shownSecond = -1;
            body.anchoredPosition = Vector2.zero;
            body.localRotation = Quaternion.identity;
            if (floatText) floatText.gameObject.SetActive(false);
            flipTween?.Kill();
            settleTween?.Kill();
            flipping = false;
            flipScale = 1f;
            settle = 0f;
            // Not started yet: the sand waits in the bottom bulb for the first touch. Resumed from the map: as it was.
            waiting = timer.State == TimerState.Ready;
            shown = waiting ? 0f : timer.Remaining01;
        }

        // First touch: the timer starts, the hourglass turns over.
        void OnStarted()
        {
            if (!waiting) return;
            waiting = false;
            flipping = true;
            shown = 0f; // the full bottom bulb swings up to the top
            flipTween?.Kill();
            flipTween = Tween.Run(body, s.flipTime, Ease.Linear, k =>
            {
                body.localRotation = Quaternion.Euler(0f, 0f, 180f * Ease.InOutBack(k));
                flipScale = 1f + (s.flipPunch - 1f) * Mathf.Sin(k * Mathf.PI);
            }, Landed);
        }

        // Upright again (180° looks the same): the sand now lies against the top cap and slides down onto the neck.
        void Landed()
        {
            body.localRotation = Quaternion.identity;
            flipScale = 1f;
            flipping = false;
            settle = 1f;
            settleTween?.Kill();
            settleTween = Tween.Run(body, s.settleTime, Ease.InOutSine, k => settle = 1f - k, () => settle = 0f);
            Burst();
            if (feedback) feedback.PlayChime(0, 1);
        }

        // A small gold light burst: a soft flash and a ring of sparkles flying out.
        void Burst()
        {
            burstTween?.Kill();
            burstFlash.gameObject.SetActive(true);
            foreach (var sp in burstSparks) sp.gameObject.SetActive(true);
            var gold = style.Gold;
            float r = s.flipBurstSize * 0.5f;
            burstTween = Tween.Run(burstFlash, 0.6f, Ease.Linear, k =>
            {
                float grow = Ease.OutCubic(k), fade = 1f - Ease.InOutSine(k);
                burstFlash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.2f, grow);
                burstFlash.color = UIKit.WithAlpha(gold, 0.45f * fade);
                for (int i = 0; i < burstSparks.Length; i++)
                {
                    float a = (i + 0.5f) * 2f * Mathf.PI / burstSparks.Length;
                    var rt = burstSparks[i].rectTransform;
                    rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * grow;
                    rt.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
                    rt.localScale = Vector3.one * (1f - 0.5f * k);
                    burstSparks[i].color = UIKit.WithAlpha(Color.Lerp(gold, Color.white, 0.4f), fade);
                }
            }, () =>
            {
                burstFlash.gameObject.SetActive(false);
                foreach (var sp in burstSparks) sp.gameObject.SetActive(false);
            });
        }

        void OnTimeUp()
        {
            // The glass cracks, a red flash, a small shake.
            if (feedback) feedback.PlayTimeUp();
            crackTween?.Kill();
            crackTween = Tween.Run(body, s.crackTime, Ease.Linear, k =>
            {
                crack.color = UIKit.WithAlpha(Color.white, 0.85f * Ease.OutCubic(Mathf.Clamp01(k * 2.5f)));
                redAlpha = k < 0.2f ? k / 0.2f : Mathf.Lerp(1f, 0.35f, (k - 0.2f) / 0.8f);
            });
            shakeTween?.Kill();
            shakeTween = Tween.Run(body, s.crackTime, Ease.Linear, k =>
                body.anchoredPosition = k >= 1f ? Vector2.zero : Random.insideUnitCircle * s.timeUpShake * (1f - k));
        }

        void OnExtended(float seconds)
        {
            // Sand pours back up into the top bulb; the crack heals.
            crackTween?.Kill();
            crack.color = UIKit.WithAlpha(Color.white, 0f);
            redAlpha = 0f;
            body.anchoredPosition = Vector2.zero;
            FloatText($"+{Mathf.RoundToInt(seconds)}s");
            if (waiting) return; // still waiting for the first touch: the sand stays below until the flip
            refillTween?.Kill();
            refilling = true;
            float from = shown;
            refillTween = Tween.Run(body, s.refillTime, Ease.InOutSine, k => shown = Mathf.Lerp(from, timer.Remaining01, k),
                () => refilling = false);
            glowTween?.Kill();
            glowTween = Tween.Run(body, s.refillTime * 1.5f, Ease.Linear, k => glowBoost = Mathf.Sin(k * Mathf.PI));
        }

        void OnSlowStarted() => BlendSlow(1f);
        void OnSlowEnded() => BlendSlow(0f);

        void BlendSlow(float to)
        {
            slowTween?.Kill();
            float from = slowBlend;
            slowTween = Tween.Run(body, s.moonFadeTime, Ease.InOutSine, k => slowBlend = Mathf.Lerp(from, to, k));
        }

        void OnWon(int stars)
        {
            if (!timer.IsTimed || !gameObject.activeInHierarchy) return;
            glowTween?.Kill();
            glowTween = Tween.Run(body, s.winMoteTime + 0.6f, Ease.Linear, k => glowBoost = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f);
            FlyMotes();
        }

        // A few stardust motes leave the remaining sand and fly toward the fusion in the middle of the screen.
        void FlyMotes()
        {
            if (!flyLayer || timer.Remaining01 <= 0f) return;
            Vector2 start = flyLayer.InverseTransformPoint(body.position);
            var target = new Vector2(0f, flyLayer.rect.height * 0.08f);
            var color = s.SandColor(shown);
            for (int i = 0; i < flyers.Length; i++)
            {
                var m = flyers[i];
                var rt = m.rectTransform;
                m.gameObject.SetActive(true);
                float delay = i * 0.07f, total = s.winMoteTime + delay;
                var bend = new Vector2(Random.Range(-160f, 160f), Random.Range(60f, 220f));
                var from = start + new Vector2(Random.Range(-18f, 18f), Random.Range(0f, 40f));
                Tween.Run(m, total, Ease.Linear, k =>
                {
                    float t = Ease.InOutSine(Mathf.Clamp01((k * total - delay) / s.winMoteTime));
                    // quadratic curve through a random bend point
                    var a = Vector2.Lerp(from, from + bend, t);
                    var b = Vector2.Lerp(from + bend, target, t);
                    rt.anchoredPosition = Vector2.Lerp(a, b, t);
                    rt.localScale = Vector3.one * (1f - 0.6f * t);
                    m.color = UIKit.WithAlpha(color, t <= 0f ? 0f : Mathf.Sin(Mathf.Min(1f, t * 1.2f) * Mathf.PI) * 0.9f + 0.1f * (1f - t));
                }, () => m.gameObject.SetActive(false));
            }
        }

        void FloatText(string text)
        {
            floatText.text = text;
            floatText.gameObject.SetActive(true);
            floatTween?.Kill();
            var rt = floatText.rectTransform;
            floatTween = Tween.Run(floatText, s.floatTextTime, Ease.Linear, k =>
            {
                // Rises over the glass.
                rt.anchoredPosition = new Vector2(0f, -s.hourglassSize.y * 0.15f + s.floatTextRise * Ease.OutCubic(k));
                floatText.alpha = k < 0.15f ? k / 0.15f : 1f - Ease.InOutSine((k - 0.15f) / 0.85f);
            }, () => floatText.gameObject.SetActive(false));
        }

        // ---------- per frame (no allocations) ----------

        void Update()
        {
            if (!timer || !timer.IsTimed) return;
            float remaining = timer.Remaining;
            // Tied to the timer; right after the flip the sand eases from full while it settles onto the neck.
            if (!refilling && !flipping && !waiting) shown = Mathf.Lerp(timer.Remaining01, 1f, settle);
            bool running = timer.State == TimerState.Running && !timer.IsPaused;
            if (!timer.IsPaused) animTime += Time.unscaledDeltaTime;
            bool warning = timer.State == TimerState.Running && remaining > 0f && remaining <= s.warningSeconds;

            UpdateLabel(remaining, warning, running);

            // Last seconds: the glass breathes once per second, in step with the ticks.
            float pulse = warning ? Mathf.Pow(0.5f + 0.5f * Mathf.Cos((Mathf.Ceil(remaining) - remaining) * 2f * Mathf.PI), 3f) : 0f;
            body.localScale = Vector3.one * ((1f + s.warningPulse * pulse) * flipScale);
            var gold = style.Zodiac.gold;
            glass.color = Color.Lerp(Color.white, Color.Lerp(Color.white, s.sandRed, 0.5f), pulse * 0.8f);

            // Color follows the time left (not the sand shown: waiting / flipping shows it all below, but time is full).
            float colorShare = waiting || flipping ? timer.Remaining01 : shown;
            var sandColor = Color.Lerp(s.SandColor(colorShare), s.sandSilver, slowBlend);
            sand.Set(shown, sandColor, settle, s.sandDip);

            float t = animTime;
            float glow = s.sandGlowAlpha * (1f + 1.5f * glowBoost) * (0.85f + 0.15f * Mathf.Sin(t * 2.1f));
            topGlow.color = UIKit.WithAlpha(sandColor, glow * Mathf.Clamp01(shown * 3f));
            topGlow.rectTransform.anchoredPosition = new Vector2(0f, (sand.TopBase + sand.TopSurface) * 0.5f);
            bottomGlow.color = UIKit.WithAlpha(sandColor, glow * Mathf.Clamp01((1f - shown) * 3f));
            bottomGlow.rectTransform.anchoredPosition = new Vector2(0f, (-HourglassSand.GlassHalf + sand.PileTop) * 0.5f);
            redGlow.color = UIKit.WithAlpha(s.sandRed, 0.45f * redAlpha + 0.12f * pulse);

            UpdateStream(running, sandColor);
            UpdateSparkles(t, sandColor);
        }

        void UpdateLabel(float remaining, bool warning, bool running)
        {
            int sec = Mathf.CeilToInt(remaining);
            if (sec != shownSecond)
            {
                // The soft tick of the last seconds.
                if (running && shownSecond > 0 && sec < shownSecond && sec < s.warningSeconds && feedback)
                    feedback.PlayTimerTick(sec <= 3);
                shownSecond = sec;
                int m = sec / 60, r = sec % 60, n = 0;
                if (m >= 10) labelChars[n++] = (char)('0' + m / 10 % 10);
                labelChars[n++] = (char)('0' + m % 10);
                labelChars[n++] = ':';
                labelChars[n++] = (char)('0' + r / 10);
                labelChars[n++] = (char)('0' + r % 10);
                label.SetCharArray(labelChars, 0, n);
            }
            var c = warning || timer.State == TimerState.TimeUp ? Color.Lerp(style.text, s.sandRed, 0.35f) : style.text;
            label.color = UIKit.WithAlpha(c, warning ? s.labelWarningAlpha : s.labelAlpha);
        }

        // A thin thread of stardust through the neck; motes fall along it (rise while Saturn refills the bulb).
        void UpdateStream(bool running, Color color)
        {
            // Only while the clock actually runs: not waiting, flipping, settling, paused, out of time or won.
            bool on = !flipping && !waiting && settle <= 0.01f && (refilling || running && shown > 0.001f);
            float top = -2f, bottom = sand.PileTop;
            float length = Mathf.Max(0f, top - bottom);
            stream.gameObject.SetActive(on && length > 1f);
            if (on)
            {
                var rt = stream.rectTransform;
                rt.anchoredPosition = new Vector2(0f, (top + bottom) * 0.5f);
                rt.sizeDelta = new Vector2(1.6f, length);
                stream.color = UIKit.WithAlpha(color, 0.55f);
            }
            float speed = s.streamSpeed * (timer.IsSlowed ? timer.Speed : 1f) * (refilling ? -1.5f : 1f);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < motes.Length; i++)
            {
                var m = motes[i];
                m.gameObject.SetActive(on && length > 1f);
                if (!on || length <= 1f) continue;
                motePhase[i] = Mathf.Repeat(motePhase[i] + speed * dt / Mathf.Max(20f, length), 1f);
                float y = Mathf.Lerp(top, bottom, motePhase[i]);
                m.rectTransform.anchoredPosition = new Vector2(Mathf.Sin((motePhase[i] + i) * 9f) * 0.8f, y);
                float twinkle = 0.5f + 0.5f * Mathf.Sin(animTime * 12f + i * 1.7f);
                m.color = UIKit.WithAlpha(Color.Lerp(color, Color.white, 0.4f * twinkle), 0.5f + 0.5f * twinkle);
            }
        }

        // Glints inside the sand (top and bottom), placed by a fixed spot in each region so they move with the level.
        void UpdateSparkles(float t, Color color)
        {
            for (int i = 0; i < sparkles.Length; i++)
            {
                var sp = sparkles[i];
                bool topHalf = (i & 1) == 0;
                float share = topHalf ? shown : 1f - shown;
                sp.gameObject.SetActive(share > 0.05f);
                if (share <= 0.05f) continue;
                var spot = sparkleSpot[i];
                float y = topHalf ? Mathf.Lerp(sand.TopBase + 3f, sand.TopSurface - 3f, spot.y)
                                  : Mathf.Lerp(-HourglassSand.GlassHalf + 3f, sand.PileTop - 5f, spot.y);
                float x = spot.x * HourglassSand.HalfWidth(y) * 0.7f;
                sp.rectTransform.anchoredPosition = new Vector2(x, y);
                float tw = Mathf.Max(0f, Mathf.Sin(t * (1.3f + 0.4f * i) + i * 2.4f));
                sp.color = UIKit.WithAlpha(Color.Lerp(color, Color.white, 0.6f), tw * tw * 0.9f);
                sp.rectTransform.localScale = Vector3.one * (0.5f + 0.5f * tw);
            }
        }

        // ---------- building ----------

        void Build()
        {
            var root = (RectTransform)transform;
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var mid = new Vector2(0.5f, 0.5f);
            var size = s.hourglassSize;
            var gold = style.Zodiac.gold;

            body = UIKit.Rect(root, "Body", mid, Vector2.zero, size);
            float scale = size.y / 190f; // the drawing is laid out for a 130 x 190 box
            body.localScale = Vector3.one;
            var art = UIKit.Rect(body, "Art", mid, Vector2.zero, new Vector2(130f, 190f));
            art.localScale = Vector3.one * scale;

            redGlow = UIKit.Image(art, "Red Glow", Art.SoftCircle, Color.clear, mid, Vector2.zero, new Vector2(230f, 280f));
            topGlow = UIKit.Image(art, "Top Glow", Art.SoftCircle, Color.clear, mid, Vector2.zero, new Vector2(100f, 90f));
            bottomGlow = UIKit.Image(art, "Bottom Glow", Art.SoftCircle, Color.clear, mid, Vector2.zero, new Vector2(110f, 80f));

            var sandRt = UIKit.Rect(art, "Sand", mid, Vector2.zero, new Vector2(130f, 190f));
            sandRt.gameObject.AddComponent<CanvasRenderer>();
            sand = sandRt.gameObject.AddComponent<HourglassSand>();
            sand.raycastTarget = false;
            sand.glassFill = UIKit.WithAlpha(style.text, 0.05f);

            stream = UIKit.Image(art, "Stream", Art.Square, Color.clear, mid, Vector2.zero, new Vector2(1.6f, 10f));
            motes = new Image[Mathf.Max(1, s.streamMotes)];
            motePhase = new float[motes.Length];
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i] = UIKit.Image(art, "Mote", Art.SoftCircle, Color.clear, mid, Vector2.zero, new Vector2(5f, 5f));
                motePhase[i] = i / (float)motes.Length;
            }
            sparkles = new Image[Mathf.Max(0, s.sandSparkles)];
            sparkleSpot = new Vector2[sparkles.Length];
            var rnd = new System.Random(5);
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkles[i] = UIKit.Image(art, "Glint", UIKit.Sparkle, Color.clear, mid, Vector2.zero, new Vector2(9f, 9f));
                sparkleSpot[i] = new Vector2((float)rnd.NextDouble() * 2f - 1f, 0.15f + 0.8f * (float)rnd.NextDouble());
            }

            glass = UIKit.Vector(art, "Glass", mid, Vector2.zero, 190f);
            DrawGlass(glass, UIKit.WithAlpha(Color.Lerp(style.text, gold, 0.3f), s.glassAlpha), s.sheenAlpha);
            crack = UIKit.Vector(art, "Crack", mid, Vector2.zero, 190f);
            DrawCrack(crack);
            crack.color = UIKit.WithAlpha(Color.white, 0f);
            frame = UIKit.Vector(art, "Frame", mid, Vector2.zero, 190f);
            DrawFrame(frame, gold);
            engraving = UIKit.Vector(art, "Engraving", mid, Vector2.zero, 190f);
            SetSign(0);

            label = UIKit.Text(root, "Time", UIKit.BodyFont(style), s.timeLabelSize, UIKit.WithAlpha(style.text, s.labelAlpha),
                new Vector2(0.5f, 0f), new Vector2(0f, -(s.timeLabelGap + s.timeLabelSize * 0.6f)), new Vector2(size.x, s.timeLabelSize * 1.3f),
                2f, FontStyles.Bold);
            floatText = UIKit.Text(root, "Float", UIKit.TitleFont(style), 44f, style.Gold, mid, Vector2.zero, new Vector2(200f, 60f), 2f,
                FontStyles.Bold);
            UIKit.Glow(floatText, UIKit.WithAlpha(style.Gold, 0.6f), 0.4f);
            floatText.gameObject.SetActive(false);

            // The burst when the flip lands (not rotated with the body).
            burstFlash = UIKit.Image(root, "Flip Flash", Art.SoftCircle, Color.clear, mid, Vector2.zero, Vector2.one * s.flipBurstSize);
            burstFlash.gameObject.SetActive(false);
            burstSparks = new Image[8];
            for (int i = 0; i < burstSparks.Length; i++)
            {
                burstSparks[i] = UIKit.Image(root, "Flip Spark", UIKit.Sparkle, Color.clear, mid, Vector2.zero,
                    Vector2.one * (i % 2 == 0 ? 22f : 14f));
                burstSparks[i].gameObject.SetActive(false);
            }

            flyers = new Image[Mathf.Max(0, s.winMotes)];
            for (int i = 0; i < flyers.Length; i++)
            {
                flyers[i] = UIKit.Image(flyLayer ? flyLayer : root, "Stardust", UIKit.Sparkle, Color.clear, mid, Vector2.zero,
                    new Vector2(22f, 22f));
                flyers[i].gameObject.SetActive(false);
            }
        }

        static readonly Vector2[] Buffer = new Vector2[48];
        static readonly System.Collections.Generic.List<Vector2> Points = new(48);

        // Bulb outline (both sides) and a faint sheen down the left of each bulb.
        static void DrawGlass(VectorGraphic g, Color line, float sheenAlpha)
        {
            g.Clear();
            const int n = 40;
            float G = HourglassSand.GlassHalf;
            for (int side = -1; side <= 1; side += 2)
            {
                Points.Clear();
                for (int i = 0; i <= n; i++)
                {
                    float y = Mathf.Lerp(G, -G, i / (float)n);
                    Points.Add(new Vector2(side * HourglassSand.HalfWidth(y), y));
                }
                g.Polyline(Points, 1.6f, line);
            }
            var sheen = UIKit.WithAlpha(Color.white, sheenAlpha);
            for (int bulb = -1; bulb <= 1; bulb += 2)
            {
                Points.Clear();
                for (int i = 0; i <= 12; i++)
                {
                    float y = bulb * Mathf.Lerp(0.3f, 0.85f, i / 12f) * G;
                    Points.Add(new Vector2(-HourglassSand.HalfWidth(y) * 0.72f, y));
                }
                g.Polyline(Points, 2.4f, sheen);
                g.Disc(new Vector2(-HourglassSand.HalfWidth(bulb * 0.55f * G) * 0.45f, bulb * 0.62f * G), 1.6f, UIKit.WithAlpha(Color.white, sheenAlpha * 1.4f));
            }
        }

        // Caps with a lip and a finial, two turned posts with beads.
        static void DrawFrame(VectorGraphic g, Color gold)
        {
            g.Clear();
            var faint = UIKit.WithAlpha(gold, gold.a * 0.55f);
            for (int end = -1; end <= 1; end += 2)
            {
                float inner = end * CapInner, outer = end * CapOuter, lip = end * LipTop;
                Rect(g, -CapHalfWidth, CapHalfWidth, inner, outer, 1.6f, gold);
                g.Line(new Vector2(-52f, inner + end * 3f), new Vector2(52f, inner + end * 3f), 0.8f, faint);
                Rect(g, -46f, 46f, outer, lip, 1.3f, gold);
                g.Disc(new Vector2(0f, lip + end * 2.6f), 2.6f, gold);
                g.Disc(new Vector2(-54f, outer - end * 6f), 1.4f, faint);
                g.Disc(new Vector2(54f, outer - end * 6f), 1.4f, faint);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * PostX;
                g.Line(new Vector2(x, -CapInner), new Vector2(x, CapInner), 2.2f, gold);
                g.Disc(new Vector2(x, 0f), 3f, gold);
                g.Disc(new Vector2(x, 36f), 2f, gold);
                g.Disc(new Vector2(x, -36f), 2f, gold);
            }
            g.Disc(new Vector2(0f, -(CapInner + CapOuter) * 0.5f), 2f, faint);
        }

        static void Rect(VectorGraphic g, float x0, float x1, float y0, float y1, float width, Color c)
        {
            Buffer[0] = new Vector2(x0, y0);
            Buffer[1] = new Vector2(x1, y0);
            Buffer[2] = new Vector2(x1, y1);
            Buffer[3] = new Vector2(x0, y1);
            Points.Clear();
            for (int i = 0; i < 4; i++) Points.Add(Buffer[i]);
            g.Polyline(Points, width, c, true);
        }

        // Hairline fractures across both bulbs (shown on time up).
        static void DrawCrack(VectorGraphic g)
        {
            g.Clear();
            var c = Color.white;
            g.Polyline(new[] { new Vector2(6f, 58f), new Vector2(13f, 47f), new Vector2(9f, 38f), new Vector2(19f, 27f), new Vector2(14f, 15f) }, 1.3f, c);
            g.Polyline(new[] { new Vector2(13f, 47f), new Vector2(24f, 50f), new Vector2(30f, 44f) }, 1f, c);
            g.Polyline(new[] { new Vector2(9f, 38f), new Vector2(-4f, 33f) }, 0.9f, c);
            g.Polyline(new[] { new Vector2(-10f, -22f), new Vector2(-5f, -33f), new Vector2(-15f, -44f), new Vector2(-9f, -56f) }, 1.2f, c);
            g.Polyline(new[] { new Vector2(-15f, -44f), new Vector2(-26f, -40f) }, 0.9f, c);
        }
    }

    /// <summary>
    /// The hourglass glass and its stardust as one uGUI mesh: a faint glass fill, the sand left in the top bulb
    /// (level found by area, so it drains like real sand) and the pile in the bottom bulb with a small mound.
    /// Rebuilt only when the level or color changes noticeably.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class HourglassSand : MaskableGraphic
    {
        public const float GlassHalf = 70f, Neck = 3.5f, BulbRadius = 40f;
        const int Samples = 48;
        [Tooltip("Share of a bulb's height the full sand reaches.")]
        public float fullHeight = 0.8f;
        public Color glassFill = new(1f, 1f, 1f, 0.05f);

        static float[] areaFromNeck, areaFromEnd; // cumulative area, sample i = i / Samples of GlassHalf
        float fill = 1f, settle, dip;
        Color sandColor = Color.white;

        /// <summary>Top of the sand in the top bulb (at the walls), and of the pile in the bottom bulb (local y).</summary>
        public float TopSurface { get; private set; }
        /// <summary>Bottom of the top bulb's sand: the neck, or higher while it settles after the flip.</summary>
        public float TopBase { get; private set; }
        public float PileTop { get; private set; } = -GlassHalf;

        /// <summary>Half the glass width at height <paramref name="y"/>: a slender pear, narrowest at the neck.</summary>
        public static float HalfWidth(float y)
        {
            float t = Mathf.Clamp01(Mathf.Abs(y) / GlassHalf);
            float shape = t < 0.65f ? Mathf.Sin(t / 0.65f * Mathf.PI * 0.5f) : 1f - 0.18f * Sq((t - 0.65f) / 0.35f);
            return Neck + (BulbRadius - Neck) * shape;
        }

        static float Sq(float x) => x * x;

        /// <param name="settleShare">1 = the top sand still lies against the top cap (just flipped), 0 = on the neck.</param>
        /// <param name="surfaceDip">How far the top surface sinks in the middle.</param>
        public void Set(float topShare, Color color, float settleShare = 0f, float surfaceDip = 0f)
        {
            topShare = Mathf.Clamp01(topShare);
            if (Mathf.Abs(topShare - fill) < 0.0015f && Approximately(color, sandColor) && Mathf.Abs(settleShare - settle) < 0.002f &&
                Mathf.Approximately(surfaceDip, dip) && TopSurface != 0f) return;
            fill = topShare;
            sandColor = color;
            settle = settleShare;
            dip = surfaceDip;
            Levels();
            SetVerticesDirty();
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < 0.01f;

        static void Tables()
        {
            if (areaFromNeck != null) return;
            areaFromNeck = new float[Samples + 1];
            areaFromEnd = new float[Samples + 1];
            float step = GlassHalf / Samples;
            for (int i = 1; i <= Samples; i++)
            {
                float y0 = (i - 1) * step, y1 = i * step;
                areaFromNeck[i] = areaFromNeck[i - 1] + (HalfWidth(y0) + HalfWidth(y1)) * step; // 2 * half width * dy / 2 * 2
                float e0 = GlassHalf - (i - 1) * step, e1 = GlassHalf - i * step;
                areaFromEnd[i] = areaFromEnd[i - 1] + (HalfWidth(e0) + HalfWidth(e1)) * step;
            }
        }

        // Height (distance along the table) holding this much area.
        static float HeightFor(float[] table, float area)
        {
            for (int i = 1; i <= Samples; i++)
                if (table[i] >= area)
                {
                    float k = (area - table[i - 1]) / Mathf.Max(1e-4f, table[i] - table[i - 1]);
                    return (i - 1 + k) * GlassHalf / Samples;
                }
            return GlassHalf;
        }

        float pileLevel, mound;

        void Levels()
        {
            Tables();
            float fullArea = Area(areaFromNeck, fullHeight * GlassHalf);
            float height = fill > 0f ? Mathf.Max(1f, HeightFor(areaFromNeck, fill * fullArea)) : 0.001f;
            // Settling: the block of sand slides from the top cap down to the neck.
            TopBase = 0.5f + settle * Mathf.Max(0f, GlassHalf - 1.5f - height);
            TopSurface = TopBase + height;
            float bottomShare = 1f - fill;
            pileLevel = HeightFor(areaFromEnd, bottomShare * fullArea * 0.92f); // the mound holds the rest
            mound = bottomShare > 0.001f ? Mathf.Lerp(3f, 12f, Mathf.Sqrt(bottomShare)) : 0f;
            PileTop = bottomShare > 0.001f ? -GlassHalf + pileLevel + mound : -GlassHalf;
        }

        static float Area(float[] table, float height)
        {
            float f = Mathf.Clamp(height / GlassHalf * Samples, 0f, Samples);
            int i = Mathf.Min(Samples - 1, (int)f);
            return Mathf.Lerp(table[i], table[i + 1], f - i);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (TopSurface == 0f) Levels();
            // Faint glass
            Strips(vh, -GlassHalf, GlassHalf, 40, 0f, glassFill, glassFill, 0f, 0f);
            Color deep = Color.Lerp(sandColor, Color.black, 0.25f);
            deep.a = sandColor.a;
            // Top bulb: sand from the neck up to its surface, brighter near the top; the surface dips in the middle.
            if (fill > 0.001f) TopSand(vh, deep, sandColor);
            // Bottom bulb: the pile, a cone mound on top.
            if (PileTop > -GlassHalf + 0.5f)
                Strips(vh, -GlassHalf, PileTop, 20, 1.2f, deep, Color.Lerp(sandColor, Color.white, 0.2f), -GlassHalf + pileLevel, mound);
        }

        // Strips up to the lowest point of the surface, then a cap whose top edge sinks toward the middle (a crater).
        void TopSand(VertexHelper vh, Color c0, Color c1)
        {
            float bottom = TopBase, top = TopSurface;
            float depth = Mathf.Min(dip * (1f - settle), (top - bottom) * 0.6f);
            float low = top - depth;
            float share = (low - bottom) / Mathf.Max(0.01f, top - bottom);
            Color mid = Color.Lerp(c0, c1, share);
            Strips(vh, bottom, low, 16, 1.2f, c0, mid, 0f, 0f);
            if (depth <= 0.05f) return;
            const int columns = 14;
            float wLow = Mathf.Max(0f, HalfWidth(low) - 1.2f), wTop = Mathf.Max(0f, HalfWidth(top) - 1.2f);
            int first = vh.currentVertCount;
            Color32 cLow = mid * color, cTop = c1 * color;
            for (int i = 0; i <= columns; i++)
            {
                float u = i / (float)columns * 2f - 1f;
                vh.AddVert(new Vector3(u * wLow, low), cLow, Vector4.zero);
                vh.AddVert(new Vector3(u * wTop, top - depth * (1f - u * u)), cTop, Vector4.zero);
            }
            for (int i = 0; i < columns; i++)
            {
                int a = first + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        // Horizontal strips from y0 to y1, glass-shaped (inset by `inset`); above `coneBase` a cone of height `cone`.
        void Strips(VertexHelper vh, float y0, float y1, int count, float inset, Color c0, Color c1, float coneBase, float cone)
        {
            if (y1 - y0 < 0.1f) return;
            int first = vh.currentVertCount;
            float coneWidth = cone > 0f ? Mathf.Max(0f, HalfWidth(coneBase) - inset) : 0f;
            for (int i = 0; i <= count; i++)
            {
                float k = i / (float)count;
                float y = Mathf.Lerp(y0, y1, k);
                float w = Mathf.Max(0f, HalfWidth(y) - inset);
                // A rounded mound: its profile is a parabola, so the width shrinks with the square root toward the tip.
                if (cone > 0f && y > coneBase) w = Mathf.Min(w, coneWidth * Mathf.Sqrt(Mathf.Clamp01((coneBase + cone - y) / cone)));
                Color32 c = Color.Lerp(c0, c1, k) * color;
                vh.AddVert(new Vector3(-w, y), c, Vector4.zero);
                vh.AddVert(new Vector3(w, y), c, Vector4.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int a = first + i * 2;
                vh.AddTriangle(a, a + 2, a + 3);
                vh.AddTriangle(a, a + 3, a + 1);
            }
        }
    }
}
