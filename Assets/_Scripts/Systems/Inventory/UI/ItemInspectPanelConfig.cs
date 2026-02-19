using UnityEngine;
using TMPro;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// ScriptableObject that holds all designer-facing settings for the inventory UI.
    /// Used by both ItemInspectPanel and FloatingContainerWindow so shared assets
    /// (font, close button icon, weight icon) only need to be set in one place.
    ///
    /// Create one via: Assets ▶ Create ▶ TimeGame ▶ Inventory ▶ Inventory UI Config
    /// Save it to Assets/Resources/ so any system can load it at runtime with no
    /// scene references — keep the filename exactly "ItemInspectPanelConfig".
    ///
    /// All fields are optional: each system falls back to its own code defaults when
    /// a field is left null/zero.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ItemInspectPanelConfig",
        menuName  = "TimeGame/Inventory/Inventory UI Config",
        order     = 210)]
    public class ItemInspectPanelConfig : ScriptableObject
    {
        // ── Shared font ───────────────────────────────────────────────────────
        [Header("Shared Font")]
        [Tooltip("UI font used by all inventory windows. Leave empty for the TMP default.")]
        public TMP_FontAsset uiFont;

        // ── Shared icons ──────────────────────────────────────────────────────
        [Header("Shared Icons")]
        [Tooltip("Close button icon used by both the inspect panel and floating container windows. " +
                 "Leave empty for the built-in ✕ text fallback (inspect panel) or the prefab default (container windows).")]
        public Sprite closeButtonSprite;

        [Tooltip("Weight icon used in floating container window weight rows. " +
                 "Leave empty to keep the icon invisible.")]
        public Sprite weightIconSprite;

        // ── Inspect panel icons ───────────────────────────────────────────────
        [Header("Inspect Panel Icons")]
        [Tooltip("Resize handle icon for the inspect panel. Leave empty for the built-in ⇲ text fallback.")]
        public Sprite resizeHandleSprite;

        // ── Window defaults ───────────────────────────────────────────────────
        [Header("Window Size")]
        [Tooltip("Initial window width as a fraction of screen width (0.3 – 1.0).")]
        [Range(0.3f, 1f)]
        public float windowWidthFraction = 0.72f;

        [Tooltip("Initial window height as a fraction of screen height (0.3 – 1.0).")]
        [Range(0.3f, 1f)]
        public float windowHeightFraction = 0.78f;

        [Tooltip("Minimum allowed window size in canvas pixels.")]
        public Vector2 minWindowSize = new Vector2(500f, 380f);

        [Tooltip("Maximum allowed window size in canvas pixels.")]
        public Vector2 maxWindowSize = new Vector2(1600f, 1000f);

        // ── Preview camera ────────────────────────────────────────────────────
        [Header("Preview Camera")]
        [Tooltip("Distance from the camera to the model pivot.")]
        public float cameraDistance = 2.5f;

        [Tooltip("Vertical field-of-view of the preview camera.")]
        public float cameraFOV = 40f;

        [Tooltip("Camera tilt in degrees above the item pivot (positive = looking down slightly).")]
        public float cameraElevation = 15f;

        [Tooltip("Resolution of the render texture used for the 3-D preview.")]
        public int previewResolution = 512;

        // ── Model rotation ────────────────────────────────────────────────────
        [Header("Rotation")]
        [Tooltip("How sensitive horizontal drag is to yaw rotation.")]
        public float dragSensitivity = 0.4f;

        [Tooltip("How quickly the drag inertia decays after releasing the mouse.")]
        public float inertiaDamping = 8f;

        [Tooltip("Automatic slow-spin speed (deg/s) while the user is not dragging.")]
        public float autoSpinSpeed = 20f;
    }
}
