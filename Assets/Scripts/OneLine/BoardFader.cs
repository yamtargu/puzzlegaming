using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OneLine
{
    /// <summary>
    /// Fades the board (everything under PathManager.BoardPivot: stars, lines, trail, glow lights) in or out with a
    /// slight scale, then hides it. Colors are captured when a fade starts and re-applied scaled each frame, after
    /// everything else has updated.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BoardFader : MonoBehaviour
    {
        public PathManager pathManager;
        public UIStyle style;

        readonly List<SpriteRenderer> sprites = new();
        readonly List<Color> spriteColors = new();
        readonly List<LineRenderer> lines = new();
        readonly List<Color> lineStart = new(), lineEnd = new();
        readonly List<Light2D> lights = new();
        readonly List<float> lightIntensity = new();
        float alpha = 1f;
        bool fading;
        Tween tween;

        public void Show(bool visible, bool instant)
        {
            var pivot = pathManager ? pathManager.BoardPivot : null;
            if (!pivot) return;
            tween?.Kill();
            if (!visible && !pivot.gameObject.activeSelf) return;
            float current = alpha;
            if (fading) { alpha = 1f; Apply(); } // interrupted mid-fade: put the true colors back before capturing again
            pivot.gameObject.SetActive(true);
            Capture(pivot);
            float from = visible ? 0f : current, to = visible ? 1f : 0f;
            float scaleFrom = visible ? style.boardScaleFrom : 1f, scaleTo = visible ? 1f : style.boardScaleFrom;
            if (instant)
            {
                Finish(pivot, visible);
                return;
            }
            fading = true;
            alpha = from;
            Apply();
            tween = Tween.Run(this, style.screenFadeTime, Ease.OutCubic, k =>
            {
                alpha = Mathf.Lerp(from, to, k);
                pivot.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, k);
            }, () => Finish(pivot, visible));
        }

        void Finish(Transform pivot, bool visible)
        {
            fading = false;
            alpha = 1f;
            Apply(); // put the captured colors back
            pivot.localScale = Vector3.one;
            pivot.gameObject.SetActive(visible);
        }

        void Capture(Transform root)
        {
            root.GetComponentsInChildren(true, sprites);
            spriteColors.Clear();
            foreach (var s in sprites) spriteColors.Add(s.color);
            root.GetComponentsInChildren(true, lines);
            lineStart.Clear();
            lineEnd.Clear();
            foreach (var l in lines) { lineStart.Add(l.startColor); lineEnd.Add(l.endColor); }
            root.GetComponentsInChildren(true, lights);
            lightIntensity.Clear();
            foreach (var l in lights) lightIntensity.Add(l.intensity);
        }

        void LateUpdate()
        {
            if (fading) Apply();
        }

        void Apply()
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i]) continue;
                var c = spriteColors[i];
                c.a *= alpha;
                sprites[i].color = c;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i]) continue;
                Color a = lineStart[i], b = lineEnd[i];
                a.a *= alpha;
                b.a *= alpha;
                lines[i].startColor = a;
                lines[i].endColor = b;
            }
            for (int i = 0; i < lights.Count; i++)
                if (lights[i]) lights[i].intensity = lightIntensity[i] * alpha;
        }
    }
}
