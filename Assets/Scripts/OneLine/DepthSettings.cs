using UnityEngine;

namespace OneLine
{
    /// <summary>All 2.5D tunables in one asset: board tilt, shadows, node pop, parallax background.</summary>
    [CreateAssetMenu(fileName = "DepthSettings", menuName = "One Line/Depth Settings")]
    public class DepthSettings : ScriptableObject
    {
        [Header("Tilt (BoardView)")]
        [Range(0f, 15f)] public float maxTiltDegrees = 8f;
        [Tooltip("Seconds to catch up with the input; higher = smoother, lazier.")]
        [Range(0.01f, 1f)] public float tiltSmoothTime = 0.25f;
        [Tooltip("Use the accelerometer on phones / tablets.")]
        public bool useDeviceSensor = true;
        [Tooltip("How much device tilt (in g) gives a full board tilt. Lower = more sensitive.")]
        [Range(0.05f, 1f)] public float sensorFullTilt = 0.35f;
        [Tooltip("How fast the 'neutral' holding angle follows the device, so any comfortable angle works.")]
        [Range(0f, 2f)] public float sensorRecenterSpeed = 0.4f;
        [Tooltip("Tilt with the mouse position (Editor / desktop).")]
        public bool mouseTilt = true;
        [Tooltip("Tilt is scaled by this while the player is drawing, so the board stays calm under the finger.")]
        [Range(0f, 1f)] public float drawingTiltScale = 0.35f;

        [Header("Shadows (DepthFeedback)")]
        [Tooltip("Shadow offset on the 'table' behind the board (light from the top left).")]
        public Vector2 shadowOffset = new(0.07f, -0.10f);
        [Tooltip("How far behind the board the shadows lie. Bigger = more parallax when the board tilts.")]
        [Range(0f, 2f)] public float shadowDepth = 0.45f;
        [Range(0f, 1f)] public float nodeShadowAlpha = 0.35f;
        [Range(0f, 1f)] public float lineShadowAlpha = 0.22f;
        [Tooltip("Node shadow size relative to the node.")]
        [Range(0.5f, 2.5f)] public float nodeShadowScale = 1.35f;
        [Tooltip("Extra shadow offset per unit of node height — lifted nodes throw longer shadows.")]
        [Range(0f, 10f)] public float heightShadowFactor = 3f;

        [Header("Board plate (DepthFeedback)")]
        [Tooltip("A slightly lighter rounded plate under the board. Shadows land on it, so they stay visible " +
                 "on dark themes, and its edges make the tilt easy to see.")]
        public bool showPlate = true;
        [Tooltip("How much lighter than the background the plate is.")]
        [Range(0f, 0.3f)] public float plateLighten = 0.06f;
        [Tooltip("Plate margin around the outermost nodes (world units).")]
        [Range(0f, 2f)] public float platePadding = 0.8f;

        [Header("Node pop (DepthFeedback)")]
        [Tooltip("Height a visited node floats at, toward the camera (world units).")]
        [Range(0f, 0.5f)] public float visitedHeight = 0.08f;
        [Tooltip("Upward kick when a node is reached.")]
        [Range(0f, 10f)] public float popImpulse = 3.5f;
        [Range(10f, 800f)] public float popStiffness = 260f;
        [Range(0f, 50f)] public float popDamping = 14f;

        [Header("Glow (NodeGlow, URP Light2D)")]
        public bool glowEnabled = true;
        [Tooltip("Steady glow of a visited node.")]
        [Range(0f, 3f)] public float glowIntensity = 0.7f;
        [Tooltip("Brief flash the moment a node is reached.")]
        [Range(0f, 6f)] public float glowFlashIntensity = 2.2f;
        [Tooltip("Light radius as a multiple of the node size.")]
        [Range(0.5f, 5f)] public float glowRadius = 1.6f;
        [Range(0f, 1f)] public float glowFalloff = 0.6f;
        [Tooltip("How fast the flash settles / the glow fades after a reset.")]
        [Range(0.5f, 30f)] public float glowFadeSpeed = 7f;

        [Header("Parallax background")]
        [Tooltip("Far, middle, near layer: how far each moves at full tilt, as a share of the screen half-height.")]
        public Vector3 parallaxStrength = new(0.03f, 0.07f, 0.13f);
        [Tooltip("Blobs per layer (far, middle, near).")]
        public Vector3Int parallaxCounts = new(5, 9, 16);
        [Tooltip("Blob opacity per layer (far, middle, near).")]
        public Vector3 parallaxAlpha = new(0.06f, 0.07f, 0.08f);
    }
}
