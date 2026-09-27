using UnityEngine;
using UnityEngine.InputSystem;

namespace OneLine
{
    /// <summary>
    /// Tilts the board (PathManager.BoardPivot) a few degrees for a 2.5D feel:
    /// from the accelerometer on phones, from the mouse position in the Editor / on desktop.
    /// Purely visual — node snapping uses screen positions, so input stays exact at any tilt.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public PathManager pathManager;
        public DepthSettings settings;

        /// <summary>Current smoothed tilt, each axis in -1..1. ParallaxBackground reads this.</summary>
        public Vector2 Tilt { get; private set; }

        /// <summary>Tests / cutscenes can force a tilt (-1..1); set to null to go back to live input.</summary>
        public Vector2? OverrideTilt { get; set; }

        Vector2 velocity;
        Vector3 neutral;
        bool calibrated;

        void Update()
        {
            if (!pathManager || !settings || !pathManager.BoardPivot) return;

            Vector2 target = OverrideTilt ?? ReadInput();
            if (pathManager.IsDrawing && OverrideTilt == null) target *= settings.drawingTiltScale;
            Tilt = Vector2.SmoothDamp(Tilt, target, ref velocity, settings.tiltSmoothTime);

            // The side the tilt points to dips away from the camera.
            float max = settings.maxTiltDegrees;
            pathManager.BoardPivot.localRotation = Quaternion.Euler(Tilt.y * max, -Tilt.x * max, 0f);
        }

        Vector2 ReadInput()
        {
            var accelerometer = Accelerometer.current;
            if (settings.useDeviceSensor && accelerometer != null && !Application.isEditor)
            {
                if (!accelerometer.enabled) InputSystem.EnableDevice(accelerometer);
                Vector3 g = accelerometer.acceleration.ReadValue();
                if (!calibrated) { neutral = g; calibrated = true; }
                // Slowly re-center, so whatever angle the player holds the phone at becomes "flat".
                neutral = Vector3.Lerp(neutral, g, settings.sensorRecenterSpeed * Time.deltaTime);
                Vector3 d = g - neutral;
                return Vector2.ClampMagnitude(new Vector2(d.x, d.y) / settings.sensorFullTilt, 1f);
            }

            if (settings.mouseTilt && Mouse.current != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                var n = new Vector2(m.x / Screen.width, m.y / Screen.height) * 2f - Vector2.one;
                return Vector2.ClampMagnitude(n, 1f);
            }
            return Vector2.zero;
        }
    }
}
