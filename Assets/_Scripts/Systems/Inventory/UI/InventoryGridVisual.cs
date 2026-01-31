using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Visual representation of an inventory grid.
    /// Handles drawing the grid background and spawning item visuals.
    /// 
    /// Uses MANUAL positioning - no Unity LayoutGroups.
    /// Full control over grid layout and scaling.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryGridVisual : MonoBehaviour, IInventoryDropTarget
    {
        [Header("References")]
        [Tooltip("Prefab for individual grid cells (background)")]
        [SerializeField] private GameObject gridCellPrefab;

        [Tooltip("Prefab for item visuals")]
        [SerializeField] private GameObject itemVisualPrefab;

        [Header("Appearance")]
        [Tooltip("Color for grid cell backgrounds")]
        [SerializeField] private Color gridCellColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        [Tooltip("Color for grid cell borders")]
        [SerializeField] private Color gridBorderColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        [Tooltip("Border thickness in pixels")]
        [SerializeField] private float borderThickness = 1f;

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLogging = false;

        // Internal references
        private RectTransform rectTransform;
        private InventorySystem inventorySystem;

        // Visual tracking
        private Dictionary<Guid, InventoryItemVisual> itemVisuals = new Dictionary<Guid, InventoryItemVisual>();
        private Transform gridCellContainer;
        private Transform itemContainer;

        // Grid dimensions (cached)
        private int gridWidth;
        private int gridHeight;
        private float cellSize;

        // Public accessors
        public InventorySystem InventorySystem => inventorySystem;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float CellSize => cellSize;

        #region Initialization

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            // Create containers for organization
            gridCellContainer = CreateContainer("GridCells");
            itemContainer = CreateContainer("Items");
        }

        /// <summary>
        /// Initialize this grid visual with an inventory system.
        /// Call this after creating the InventorySystem.
        /// </summary>
        public void Initialize(InventorySystem system)
        {
            if (system == null)
            {
                Debug.LogError("[InventoryGridVisual] Cannot initialize with null InventorySystem!", this);
                return;
            }

            // Unsubscribe from old system if any
            if (inventorySystem != null)
            {
                inventorySystem.OnItemAdded -= HandleItemAdded;
                inventorySystem.OnItemRemoved -= HandleItemRemoved;
            }

            inventorySystem = system;
            gridWidth = system.Width;
            gridHeight = system.Height;
            cellSize = system.CellSize;

            // Subscribe to events
            inventorySystem.OnItemAdded += HandleItemAdded;
            inventorySystem.OnItemRemoved += HandleItemRemoved;

            // Set our size to match grid
            rectTransform.sizeDelta = new Vector2(gridWidth * cellSize, gridHeight * cellSize);

            // Draw grid background
            DrawGridBackground();

            // Spawn visuals for any existing items
            RefreshAllItemVisuals();

            if (enableDebugLogging)
            {
                Debug.Log($"[InventoryGridVisual] Initialized {gridWidth}×{gridHeight} grid, cell size {cellSize}px");
            }
        }

        private Transform CreateContainer(string containerName)
        {
            GameObject container = new GameObject(containerName);
            RectTransform rt = container.AddComponent<RectTransform>();
            
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            return container.transform;
        }

        #endregion

        #region Grid Background Drawing

        /// <summary>
        /// Draw the grid background cells.
        /// Manual positioning - no LayoutGroups.
        /// </summary>
        private void DrawGridBackground()
        {
            // Clear existing cells
            foreach (Transform child in gridCellContainer)
            {
                Destroy(child.gameObject);
            }

            // Create cell prefab if not provided
            if (gridCellPrefab == null)
            {
                gridCellPrefab = CreateDefaultGridCellPrefab();
            }

            // Spawn cells manually at calculated positions
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    SpawnGridCell(x, y);
                }
            }

            if (enableDebugLogging)
            {
                Debug.Log($"[InventoryGridVisual] Drew {gridWidth * gridHeight} grid cells");
            }
        }

        private void SpawnGridCell(int x, int y)
        {
            GameObject cellObj = Instantiate(gridCellPrefab, gridCellContainer);
            RectTransform cellRT = cellObj.GetComponent<RectTransform>();

            // Manual positioning
            Vector2 position = GridPositionToLocalPosition(new Vector2Int(x, y));
            
            cellRT.anchorMin = new Vector2(0, 0);
            cellRT.anchorMax = new Vector2(0, 0);
            cellRT.pivot = new Vector2(0, 0);
            cellRT.sizeDelta = new Vector2(cellSize, cellSize);
            cellRT.anchoredPosition = position;

            // Style the cell
            Image cellImage = cellObj.GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.color = gridCellColor;
            }

            // Add border (Outline component)
            Outline outline = cellObj.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = gridBorderColor;
                outline.effectDistance = new Vector2(borderThickness, borderThickness);
            }

            cellObj.name = $"Cell_{x}_{y}";
        }

        /// <summary>
        /// Create a default grid cell prefab if none provided.
        /// </summary>
        private GameObject CreateDefaultGridCellPrefab()
        {
            GameObject prefab = new GameObject("GridCell");
            
            RectTransform rt = prefab.AddComponent<RectTransform>();
            Image image = prefab.AddComponent<Image>();
            Outline outline = prefab.AddComponent<Outline>();

            image.color = gridCellColor;
            outline.effectColor = gridBorderColor;
            outline.effectDistance = new Vector2(borderThickness, borderThickness);

            return prefab;
        }

        #endregion

        #region Item Visual Management

        /// <summary>
        /// Handle item added event from InventorySystem.
        /// </summary>
        private void HandleItemAdded(PlacedItem item)
        {
            SpawnItemVisual(item);
        }

        /// <summary>
        /// Handle item removed event from InventorySystem.
        /// </summary>
        private void HandleItemRemoved(PlacedItem item)
        {
            RemoveItemVisual(item.InstanceID);
        }

        /// <summary>
        /// Spawn a visual for a placed item.
        /// </summary>
        private void SpawnItemVisual(PlacedItem placedItem)
        {
            if (placedItem == null) return;

            // Check if already exists
            if (itemVisuals.ContainsKey(placedItem.InstanceID))
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"[InventoryGridVisual] Item visual already exists for {placedItem.InstanceID}");
                }
                return;
            }

            // Get item definition
            InventoryItemSO itemDef = placedItem.ItemDefinition as InventoryItemSO;
            if (itemDef == null)
            {
                Debug.LogError($"[InventoryGridVisual] PlacedItem is not an InventoryItemSO!", this);
                return;
            }

            // Create visual prefab if not provided
            if (itemVisualPrefab == null)
            {
                itemVisualPrefab = CreateDefaultItemVisualPrefab();
            }

            // Instantiate visual
            GameObject visualObj = Instantiate(itemVisualPrefab, itemContainer);
            InventoryItemVisual visual = visualObj.GetComponent<InventoryItemVisual>();

            if (visual == null)
            {
                Debug.LogError($"[InventoryGridVisual] Item visual prefab missing InventoryItemVisual component!", this);
                Destroy(visualObj);
                return;
            }

            // Initialize visual (pass gridVisual reference for drag-drop)
            visual.Initialize(placedItem, itemDef, cellSize, this);

            // Position at grid location
            Vector2 localPos = GridPositionToLocalPosition(placedItem.AnchorPosition);
            visual.SetPosition(localPos);

            // Track visual
            itemVisuals[placedItem.InstanceID] = visual;
            visualObj.name = $"Item_{itemDef.ItemName}_{placedItem.InstanceID}";

            if (enableDebugLogging)
            {
                Debug.Log($"[InventoryGridVisual] Spawned visual for {itemDef.ItemName} at {placedItem.AnchorPosition}");
            }
        }

        /// <summary>
        /// Remove a visual by item instance ID.
        /// </summary>
        private void RemoveItemVisual(Guid instanceID)
        {
            if (itemVisuals.TryGetValue(instanceID, out InventoryItemVisual visual))
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[InventoryGridVisual] Removing visual for {instanceID}");
                }

                Destroy(visual.gameObject);
                itemVisuals.Remove(instanceID);
            }
        }

        /// <summary>
        /// Refresh all item visuals (clear and respawn from InventorySystem).
        /// </summary>
        public void RefreshAllItemVisuals()
        {
            // Clear existing visuals
            foreach (var visual in itemVisuals.Values)
            {
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
            }
            itemVisuals.Clear();

            // Spawn visuals for all items in system
            if (inventorySystem != null)
            {
                foreach (PlacedItem item in inventorySystem.GetAllItems())
                {
                    SpawnItemVisual(item);
                }
            }

            if (enableDebugLogging)
            {
                Debug.Log($"[InventoryGridVisual] Refreshed {itemVisuals.Count} item visuals");
            }
        }

        /// <summary>
        /// Create default item visual prefab if none provided.
        /// </summary>
        private GameObject CreateDefaultItemVisualPrefab()
        {
            GameObject prefab = new GameObject("ItemVisual");
            
            RectTransform rt = prefab.AddComponent<RectTransform>();
            Image image = prefab.AddComponent<Image>();
            InventoryItemVisual visual = prefab.AddComponent<InventoryItemVisual>();

            return prefab;
        }

        #endregion

        #region Position Conversion

        /// <summary>
        /// Convert grid position to local UI position (bottom-left origin).
        /// Manual calculation - no layout components.
        /// </summary>
        public Vector2 GridPositionToLocalPosition(Vector2Int gridPos)
        {
            return new Vector2(gridPos.x * cellSize, gridPos.y * cellSize);
        }

        /// <summary>
        /// Convert local UI position to grid position.
        /// </summary>
        public Vector2Int LocalPositionToGridPosition(Vector2 localPos)
        {
            int x = Mathf.FloorToInt(localPos.x / cellSize);
            int y = Mathf.FloorToInt(localPos.y / cellSize);
            return new Vector2Int(x, y);
        }

        #endregion

        #region IInventoryDropTarget Implementation

        public bool CanAcceptItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition)
        {
            if (inventorySystem == null)
            {
                Debug.LogWarning("[InventoryGridVisual] Cannot accept item - inventory system not initialized");
                return false;
            }

            // Convert mouse position to grid position
            Vector2Int gridPos = LocalPositionToGridPosition(mouseLocalPosition);

            // Check if the inventory system can place the item here (FIX: CanAddItem not CanPlaceItem)
            return inventorySystem.CanAddItem(item, gridPos, rotation);
        }

        public bool TryPlaceItem(InventoryItemSO item, GridDirection rotation, Vector2 mouseLocalPosition, out PlacedItem placedItem)
        {
            placedItem = null;

            if (inventorySystem == null)
            {
                Debug.LogWarning("[InventoryGridVisual] Cannot place item - inventory system not initialized");
                return false;
            }

            // Convert mouse position to grid position
            Vector2Int gridPos = LocalPositionToGridPosition(mouseLocalPosition);

            // Try to add the item to the inventory system
            bool success = inventorySystem.TryAddItem(item, gridPos, rotation, out placedItem);

            if (enableDebugLogging)
            {
                if (success)
                {
                    Debug.Log($"[InventoryGridVisual] Placed {item.ItemName} at {gridPos}");
                }
                else
                {
                    Debug.Log($"[InventoryGridVisual] Failed to place {item.ItemName} at {gridPos}");
                }
            }

            return success;
        }

        public RectTransform GetRectTransform()
        {
            return rectTransform;
        }

        public string GetDisplayName()
        {
            return gameObject.name;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (inventorySystem != null)
            {
                inventorySystem.OnItemAdded -= HandleItemAdded;
                inventorySystem.OnItemRemoved -= HandleItemRemoved;
            }
        }

        #endregion
    }
}
