using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages floating container windows for nested inventory containers.
    /// Handles spawning, tracking, and cleanup of container windows.
    /// </summary>
    public class FloatingContainerWindowManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject floatingWindowPrefab;
        [SerializeField] private Transform windowParent; // Canvas where windows spawn
        [SerializeField] private InventoryGridFactory gridFactory; // Factory for creating dynamic grids

        [Header("Settings")]
        [SerializeField] private Vector2 initialWindowPosition = new Vector2(0, 0);
        [SerializeField] private Vector2 windowOffset = new Vector2(30, -30); // Stack offset for multiple windows
        [SerializeField] private float cellSize = 64f; // Cell size for container grids
        [SerializeField] private bool verboseLogging = true;

        // Singleton — O(1) access, avoids FindObjectOfType at runtime
        public static FloatingContainerWindowManager Instance { get; private set; }

        // Track all open windows
        private List<FloatingContainerWindow> openWindows = new List<FloatingContainerWindow>();

        // Cached config — loaded once from Resources on first window open
        private ItemInspectPanelConfig uiConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[FloatingContainerWindowManager] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Open a container in a floating window
        /// </summary>
        public FloatingContainerWindow OpenContainer(GridPlacement.PlacedItem containerItem)
        {
            if (containerItem == null)
            {
                Debug.LogError("[FloatingContainerWindowManager] Cannot open null container!");
                return null;
            }

            InventoryItemSO itemDef = containerItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null || !itemDef.ProvidesStorage)
            {
                Debug.LogWarning($"[FloatingContainerWindowManager] Item {containerItem.ItemDefinition?.name} doesn't provide storage!");
                return null;
            }

            // Check if this container is already open - compare by InstanceID not reference!
            // Reference changes when item moves between grids (new PlacedItem created)
            FloatingContainerWindow existingWindow = openWindows.FirstOrDefault(w =>
                w.ContainerItem != null && w.ContainerItem.InstanceID == containerItem.InstanceID);
            if (existingWindow != null)
            {
                // Update the stored reference to the latest PlacedItem (may have moved grids)
                existingWindow.UpdateContainerItem(containerItem);
                Log($"Container {itemDef.ItemName} is already open - bringing to front");
                existingWindow.transform.SetAsLastSibling();
                return existingWindow;
            }

            // Ensure container has inventory system
            if (containerItem.ContainerInventory == null)
            {
                Log($"Container {itemDef.ItemName} has no ContainerInventory - initializing empty");
                ContainerHelper.InitializeContainerInventory(containerItem);
            }

            // Spawn window prefab
            GameObject windowObj = Instantiate(floatingWindowPrefab, windowParent);
            FloatingContainerWindow window = windowObj.GetComponent<FloatingContainerWindow>();

            if (window == null)
            {
                Debug.LogError("[FloatingContainerWindowManager] Window prefab doesn't have FloatingContainerWindow component!");
                Destroy(windowObj);
                return null;
            }

            // Calculate position (offset based on number of open windows)
            Vector2 spawnPosition = initialWindowPosition + (windowOffset * openWindows.Count);
            RectTransform windowRect = window.GetComponent<RectTransform>();
            if (windowRect != null)
            {
                windowRect.anchoredPosition = spawnPosition;
            }

            // Create grid visual for the container
            InventoryGridVisual gridVisual = CreateGridVisual(containerItem, itemDef);

            if (gridVisual == null)
            {
                Debug.LogError("[FloatingContainerWindowManager] Failed to create grid visual!");
                Destroy(windowObj);
                return null;
            }

            // Load shared UI config on first use (Resources.Load is cached after the first call)
            if (uiConfig == null)
                uiConfig = Resources.Load<ItemInspectPanelConfig>("ItemInspectPanelConfig");

            // Push shared sprites into the window before Initialize so they're ready on first show.
            // Weight icon: config takes priority over the factory field; fall back to factory if not set.
            Sprite weightIcon = (uiConfig != null && uiConfig.weightIconSprite != null)
                ? uiConfig.weightIconSprite
                : (gridFactory != null ? gridFactory.WeightIconSprite : null);
            if (weightIcon != null)
                window.SetWeightIconSprite(weightIcon);

            // Close button: from config only (no factory equivalent)
            if (uiConfig != null && uiConfig.closeButtonSprite != null)
                window.SetCloseButtonSprite(uiConfig.closeButtonSprite);

            // Initialize window
            window.Initialize(containerItem, gridVisual, itemDef.ItemName);

            // Track window
            openWindows.Add(window);

            Log($"Opened floating window for {itemDef.ItemName} ({itemDef.StorageGridSize.x}x{itemDef.StorageGridSize.y})");

            return window;
        }

        /// <summary>
        /// Create an InventoryGridVisual for a container using the factory
        /// </summary>
        private InventoryGridVisual CreateGridVisual(GridPlacement.PlacedItem containerItem, InventoryItemSO itemDef)
        {
            if (gridFactory == null)
            {
                Debug.LogError("[FloatingContainerWindowManager] No gridFactory assigned!");
                return null;
            }

            // Get grid dimensions from item definition
            Vector2Int gridSize = itemDef.StorageGridSize;
            float maxWeight = itemDef.StorageMaxWeight;

            // Use factory to create grid dynamically (NO prefab needed!)
            // This creates a properly sized grid with all components
            InventoryGridVisual gridVisual = gridFactory.CreateGrid(
                $"{itemDef.ItemName}_FloatingGrid",
                gridSize.x,
                gridSize.y,
                maxWeight,
                parent: null, // We'll parent it in Initialize
                cellSize: cellSize
            );

            if (gridVisual == null)
            {
                Debug.LogError("[FloatingContainerWindowManager] Factory failed to create grid!");
                return null;
            }

            // CRITICAL: Swap in the container's existing InventorySystem WITHOUT touching factory layout.
            // This preserves all items that were already in the container, without clobbering
            // the cell size and RectTransform sizing that the factory already set up correctly.
            gridVisual.SwapInventorySystem(containerItem.ContainerInventory);

            // CRITICAL: Register grid with drag handler so it can be dragged to/from!
            InventoryDragHandler dragHandler = InventoryDragHandler.Instance;
            if (dragHandler != null)
            {
                dragHandler.RegisterDropTarget(gridVisual);

                // CRITICAL: Inject drag handler reference into grid for item clicks
                // Floating windows aren't children of drag handler, so items need this reference
                gridVisual.SetDragHandler(dragHandler);

                // PARADOX PREVENTION: Tell the grid which container item owns it
                // This prevents the container from being dropped into itself
                gridVisual.SetOwnerContainerItem(containerItem);

                // Debug: Verify registration worked
                var items = containerItem.ContainerInventory.GetAllItems();
                Debug.Log($"[FloatingContainerWindowManager] Registered floating grid '{gridVisual.name}' with {items.Count} items. Grid InstanceID: {gridVisual.GetInstanceID()}");
                foreach (var item in items)
                {
                    Debug.Log($"  → Item {item.ItemDefinition.name} at {item.AnchorPosition}, InstanceID: {item.InstanceID}");
                }
            }
            else
            {
                Debug.LogWarning("[FloatingContainerWindowManager] No InventoryDragHandler found - dragging may not work!");
            }

            Log($"Created dynamic floating grid: {gridSize.x}x{gridSize.y} cells");

            return gridVisual;
        }

        /// <summary>
        /// Close all open windows
        /// </summary>
        public void CloseAllWindows()
        {
            // Create copy of list since Close() will modify it
            List<FloatingContainerWindow> windowsCopy = new List<FloatingContainerWindow>(openWindows);

            foreach (var window in windowsCopy)
            {
                if (window != null)
                {
                    window.Close();
                }
            }

            openWindows.Clear();
            Log("Closed all floating windows");
        }

        /// <summary>
        /// Unregister a window (called when window closes itself)
        /// </summary>
        public void UnregisterWindow(FloatingContainerWindow window)
        {
            if (window != null && openWindows.Contains(window))
            {
                openWindows.Remove(window);

                // Unregister grid from drag handler
                if (window.GridVisual != null)
                {
                    InventoryDragHandler dragHandler = InventoryDragHandler.Instance;
                    if (dragHandler != null)
                    {
                        dragHandler.UnregisterDropTarget(window.GridVisual);
                        Log($"Unregistered floating grid from drag handler");
                    }
                }

                Log($"Unregistered floating window");
            }
        }

        /// <summary>
        /// Sync an open window's container reference after a cross-grid move.
        /// Called by InventoryDragHandler when a container item moves grids.
        /// InstanceID is preserved so we can match the window.
        /// </summary>
        public void SyncContainerReference(GridPlacement.PlacedItem newPlacedItem)
        {
            if (newPlacedItem == null) return;

            FloatingContainerWindow window = openWindows.FirstOrDefault(w =>
                w.ContainerItem != null && w.ContainerItem.InstanceID == newPlacedItem.InstanceID);

            if (window != null)
            {
                window.UpdateContainerItem(newPlacedItem);
                Log($"Synced container reference for open window after cross-grid move");
            }
        }

        /// <summary>
        /// Close the floating window for a specific container item (matched by InstanceID).
        /// Called when a container item is equipped, so its window closes gracefully.
        /// Deferred one frame to avoid closing mid-drag (equip fires OnItemEquipped synchronously
        /// inside the drag handler's execution, so we must wait until the frame is clean).
        /// </summary>
        public void CloseWindowForItem(GridPlacement.PlacedItem containerItem)
        {
            if (containerItem == null) return;

            // If the panel is inactive (e.g. equipping from the world while inventory is closed)
            // there is no drag in progress, so we can close immediately without deferring.
            if (!gameObject.activeInHierarchy)
            {
                CloseWindowForItemImmediate(containerItem.InstanceID);
                return;
            }

            // Defer one frame so we don't close mid-drag (equip from inventory fires inside drag handler)
            StartCoroutine(CloseWindowForItemNextFrame(containerItem.InstanceID));
        }

        private void CloseWindowForItemImmediate(System.Guid instanceID)
        {
            FloatingContainerWindow window = openWindows.FirstOrDefault(w =>
                w.ContainerItem != null && w.ContainerItem.InstanceID == instanceID);

            if (window != null)
            {
                Log($"Closing window for equipped item (immediate)");
                window.Close();
            }
        }

        private IEnumerator CloseWindowForItemNextFrame(System.Guid instanceID)
        {
            yield return null; // Wait one frame for drag cleanup to complete

            FloatingContainerWindow window = openWindows.FirstOrDefault(w =>
                w.ContainerItem != null && w.ContainerItem.InstanceID == instanceID);

            if (window != null)
            {
                Log($"Closing window for equipped item (deferred)");
                window.Close();
            }
        }

        /// <summary>
        /// Check if a container is currently open (compared by InstanceID)
        /// </summary>
        public bool IsContainerOpen(GridPlacement.PlacedItem containerItem)
        {
            if (containerItem == null) return false;
            return openWindows.Any(w => w.ContainerItem != null && w.ContainerItem.InstanceID == containerItem.InstanceID);
        }

        /// <summary>
        /// Get window for a container item (compared by InstanceID)
        /// </summary>
        public FloatingContainerWindow GetWindow(GridPlacement.PlacedItem containerItem)
        {
            if (containerItem == null) return null;
            return openWindows.FirstOrDefault(w => w.ContainerItem != null && w.ContainerItem.InstanceID == containerItem.InstanceID);
        }

        private void Update()
        {
            // Close all windows on ESC
            if (Input.GetKeyDown(KeyCode.Escape) && openWindows.Count > 0)
            {
                // Close most recent window
                FloatingContainerWindow lastWindow = openWindows.LastOrDefault();
                if (lastWindow != null)
                {
                    lastWindow.Close();
                }
            }
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[FloatingContainerWindowManager] {message}");
            }
        }
    }
}
