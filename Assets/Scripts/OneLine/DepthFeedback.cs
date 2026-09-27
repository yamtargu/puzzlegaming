using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// 2.5D feedback on the board: soft drop shadows for nodes, edges and the drawn path, cast onto a
    /// "table" plane just behind the board, and a springy pop toward the camera when a node is reached.
    /// Visual only.
    /// </summary>
    public class DepthFeedback : MonoBehaviour
    {
        public PathManager pathManager;
        public DepthSettings settings;

        class NodeDepth
        {
            public Node node;
            public Transform shadow;
            public SpriteRenderer shadowRenderer;
            public float height, velocity;
            public bool wasVisited;
        }

        readonly List<NodeDepth> nodes = new();
        SpriteRenderer plate;
        Camera cam;

        void OnEnable() { if (pathManager) pathManager.BoardBuilt += Build; }
        void OnDisable() { if (pathManager) pathManager.BoardBuilt -= Build; }

        Vector3 ShadowShift(float height) =>
            new Vector3(settings.shadowOffset.x, settings.shadowOffset.y, 0f) * (1f + height * settings.heightShadowFactor)
            + Vector3.forward * settings.shadowDepth; // +z = away from the camera

        void Build()
        {
            if (!settings) return;
            nodes.Clear();
            if (pathManager.Nodes.Count == 0) return;
            var root = pathManager.Nodes[0].transform.parent; // destroyed with the level, so shadows go with it

            if (settings.showPlate)
            {
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (var node in pathManager.Nodes) { min = Vector3.Min(min, node.BoardPoint); max = Vector3.Max(max, node.BoardPoint); }
                var go = new GameObject("Board Plate", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                // Just behind the shadows, so they land on it.
                go.transform.localPosition = (min + max) * 0.5f + Vector3.forward * (settings.shadowDepth + 0.01f);
                plate = go.GetComponent<SpriteRenderer>();
                plate.sprite = Art.RoundedRect;
                plate.drawMode = SpriteDrawMode.Sliced;
                plate.size = (Vector2)(max - min) + Vector2.one * (2f * settings.platePadding);
                plate.sharedMaterial = Art.SpriteMaterial;
                plate.sortingOrder = -8;
                cam = Camera.main;
            }

            foreach (var node in pathManager.Nodes)
            {
                var go = new GameObject($"Shadow {node.Index}", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                go.transform.localScale = node.transform.localScale * settings.nodeShadowScale;
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = Art.SoftCircle;
                sr.sharedMaterial = Art.SpriteMaterial;
                sr.color = new Color(0, 0, 0, settings.nodeShadowAlpha);
                sr.sortingOrder = -5;
                nodes.Add(new NodeDepth { node = node, shadow = go.transform, shadowRenderer = sr });
            }

            // Idle connections are faint dashes now; only give them shadows if asked to.
            if (settings.lineShadowAlpha > 0f)
            {
                var lineShadow = new Color(0, 0, 0, settings.lineShadowAlpha);
                foreach (var edge in pathManager.EdgeLines)
                {
                    var s = PathManager.CreateLine(edge.name + " Shadow", root, edge.widthMultiplier * 1.8f, lineShadow, -6);
                    s.positionCount = 2;
                    s.SetPosition(0, edge.GetPosition(0) + ShadowShift(0));
                    s.SetPosition(1, edge.GetPosition(1) + ShadowShift(0));
                }
            }
            // The drawn path has its own colored glow (WandTrail), so no path shadow here.
        }

        void LateUpdate()
        {
            if (!settings || !pathManager) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // Plate follows the theme / equipped background.
            if (plate && cam) plate.color = Color.Lerp(cam.backgroundColor, Color.white, settings.plateLighten);

            foreach (var d in nodes)
            {
                if (!d.node) continue;
                // Springy height: kicked up when reached, settles at visitedHeight; drops back to 0 on reset.
                if (d.node.Visited && !d.wasVisited) d.velocity += settings.popImpulse;
                d.wasVisited = d.node.Visited;
                float target = d.node.Visited ? settings.visitedHeight : 0f;
                d.velocity += (target - d.height) * settings.popStiffness * dt;
                d.velocity *= Mathf.Max(0f, 1f - settings.popDamping * dt);
                d.height += d.velocity * dt;

                var p = d.node.BoardPoint;
                d.node.transform.localPosition = new Vector3(p.x, p.y, -d.height); // -z = toward the camera
                d.shadow.localPosition = p + ShadowShift(Mathf.Max(0f, d.height));
                // Higher node -> wider, fainter shadow.
                float lift = Mathf.Clamp01(d.height * 4f);
                d.shadow.localScale = d.node.transform.localScale * settings.nodeShadowScale * (1f + lift * 0.25f);
                d.shadowRenderer.color = new Color(0, 0, 0, settings.nodeShadowAlpha * (1f - lift * 0.35f));
            }
        }
    }
}
