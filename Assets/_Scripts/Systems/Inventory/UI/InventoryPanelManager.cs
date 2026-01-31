using UnityEngine;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages multiple inventory panels (left/center/right).
    /// Automatically registers all child drop targets with the drag handler.
    /// Makes it easy to set up multi-panel inventory layouts.
    /// </summary>
    public class InventoryPanelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryDragHandler dragHandler;

        [Header("Auto-Registration")]
        [Tooltip("Automatically find and register all IInventoryDropTarget components in children")]
        [SerializeField] private bool autoRegisterOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        private void Start()
        {
            if (autoRegisterOnStart)
            {
                RegisterAllDropTargets();
            }
        }

        /// <summary>
        /// Find and register all IInventoryDropTarget components in children.
        /// Call this after dynamically spawning new panels/grids/slots.
        /// </summary>
        public void RegisterAllDropTargets()
        {
            if (dragHandler == null)
            {
                Debug.LogError("[InventoryPanelManager] Drag handler reference is null!", this);
                return;
            }

            int registeredCount = 0;

            // Find all InventoryGridVisual components
            InventoryGridVisual[] grids = GetComponentsInChildren<InventoryGridVisual>(true);
            foreach (InventoryGridVisual grid in grids)
            {
                dragHandler.RegisterDropTarget(grid);
                registeredCount++;
                Log($"Registered grid: {grid.gameObject.name}");
            }

            // Find all EquipmentSlot components
            EquipmentSlot[] slots = GetComponentsInChildren<EquipmentSlot>(true);
            foreach (EquipmentSlot slot in slots)
            {
                dragHandler.RegisterDropTarget(slot);
                registeredCount++;
                Log($"Registered slot: {slot.gameObject.name}");
            }

            Debug.Log($"[InventoryPanelManager] Registered {registeredCount} drop targets ({grids.Length} grids, {slots.Length} slots)");
        }

        /// <summary>
        /// Register a single drop target.
        /// </summary>
        public void RegisterDropTarget(IInventoryDropTarget target)
        {
            if (dragHandler == null)
            {
                Debug.LogError("[InventoryPanelManager] Drag handler reference is null!", this);
                return;
            }

            dragHandler.RegisterDropTarget(target);
            Log($"Registered target: {target.GetDisplayName()}");
        }

        /// <summary>
        /// Unregister a drop target.
        /// </summary>
        public void UnregisterDropTarget(IInventoryDropTarget target)
        {
            if (dragHandler == null)
            {
                Debug.LogError("[InventoryPanelManager] Drag handler reference is null!", this);
                return;
            }

            dragHandler.UnregisterDropTarget(target);
            Log($"Unregistered target: {target.GetDisplayName()}");
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[InventoryPanelManager] {message}");
            }
        }
    }
}
