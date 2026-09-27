using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OneLine
{
    /// <summary>
    /// URP 2D glow: every node gets a small point Light2D that flashes when the node is reached and
    /// then keeps a soft glow while it's part of the path. Colored with the current path color. Visual only.
    /// </summary>
    public class NodeGlow : MonoBehaviour
    {
        public PathManager pathManager;
        public DepthSettings settings;

        class Glow
        {
            public Node node;
            public Light2D light;
            public bool wasVisited;
        }

        readonly List<Glow> glows = new();
        static FieldInfo sortingLayersField;

        void OnEnable() { if (pathManager) pathManager.BoardBuilt += Build; }
        void OnDisable() { if (pathManager) pathManager.BoardBuilt -= Build; }

        void Build()
        {
            glows.Clear();
            if (!settings || !settings.glowEnabled) return;
            foreach (var node in pathManager.Nodes)
            {
                // Under the (unscaled) level root, following the node each frame, so the radius is plain world units.
                var go = new GameObject($"Glow {node.Index}");
                go.transform.SetParent(node.transform.parent, false);
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.pointLightInnerRadius = 0f;
                light.pointLightOuterRadius = settings.glowRadius * node.transform.localScale.x;
                light.falloffIntensity = settings.glowFalloff;
                light.intensity = 0f;
                LightAllSortingLayers(light);
                glows.Add(new Glow { node = node, light = light });
            }
        }

        void LateUpdate()
        {
            if (!settings || !pathManager) return;
            float k = 1f - Mathf.Exp(-settings.glowFadeSpeed * Time.deltaTime);
            foreach (var g in glows)
            {
                if (!g.light || !g.node) continue;
                g.light.transform.localPosition = g.node.transform.localPosition; // follows the pop
                g.light.color = g.node.CurrentColor; // the star's own visit color
                if (g.node.Visited && !g.wasVisited) g.light.intensity = settings.glowFlashIntensity; // flash
                g.wasVisited = g.node.Visited;
                float target = g.node.Visited ? settings.glowIntensity : 0f;
                g.light.intensity = Mathf.Lerp(g.light.intensity, target, k);
                g.light.enabled = g.light.intensity > 0.01f; // idle nodes cost nothing
            }
        }

        // A Light2D added from code may start with no target sorting layers; make it light every layer.
        static void LightAllSortingLayers(Light2D light)
        {
            sortingLayersField ??= typeof(Light2D).GetField("m_ApplyToSortingLayers",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (sortingLayersField == null) return;
            var current = sortingLayersField.GetValue(light) as int[];
            if (current != null && current.Length > 0) return;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            sortingLayersField.SetValue(light, ids);
        }
    }
}
