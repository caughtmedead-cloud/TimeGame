using UnityEngine;
using UnityEngine.EventSystems;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Handles drag-drop operations for inventory items.
    /// Manages input, ghost preview, and placement validation.
    /// </summary>
    public class InventoryDragHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryGridVisual gridVisual;
        [SerializeField] private InventoryItemGhost ghost;
        [SerializeField] private Canvas parentCanvas;

        [Header("Input Settings")]
        [SerializeField] private KeyCode rotateKey = KeyCode.R;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Drag state
        private bool isDragging = false;
        private PlacedItem draggedItem;
        private Vector2Int originalPosition;
        private GridDirection originalRotation;
        private InventorySystem inventorySystem;

        private void Awake()
        {
            if (ghost == null)
            {
                Debug.LogError("[InventoryDragHandler] Ghost reference is missing!");
            }

            if (gridVisual == null)
            {
                Debug.LogError("[InventoryDragHandler] GridVisual reference is missing!");
            }

            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
            }
        }

        /// <summary>
        /// Initialize with inventory system reference.
        /// </summary>
        public void Initialize(InventorySystem inventory)
        {
            inventorySystem = inventory;
            Log("Drag handler initialized");
        }

        private void Update()
        {
            if (inventorySystem == null || gridVisual == null) return;

            // Handle drag input
            if (isDragging)
            {
                UpdateDrag();
            }
            else
            {
                CheckStartDrag();
            }
        }

        /// <summary>
        /// Check if user clicked on an item to start dragging.
        /// </summary>
        private void CheckStartDrag()
        {
            // Left mouse button pressed
            if (Input.GetMouseButtonDown(0))
            {
                // Convert mouse position to grid position
                Vector2Int gridPos = GetMouseGridPosition();
                
                // Check if there's an item at this position
                PlacedItem item = inventorySystem.GetItemAt(gridPos);
                
                if (item != null)
                {
                    StartDrag(item);
                }
            }
        }

        /// <summary>
        /// Start dragging an item.
        /// </summary>
        private void StartDrag(PlacedItem item)
        {
            draggedItem = item;
            originalPosition = item.AnchorPosition;
            originalRotation = item.Rotation;

            Log($"Started dragging {item.ItemDefinition.name} from {originalPosition}");

            // Remove item from grid (but keep reference)
            inventorySystem.RemoveItem(item.InstanceID);

            // Show ghost
            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            ghost.Initialize(itemDef, item.Rotation, gridVisual.CellSize);
            ghost.Show();

            isDragging = true;
        }

        /// <summary>
        /// Update drag state (mouse movement, rotation).
        /// </summary>
        private void UpdateDrag()
        {
            // Update ghost position
            UpdateGhostPosition();

            // Handle rotation input
            if (Input.GetKeyDown(rotateKey))
            {
                ghost.Rotate();
                Log($"Rotated ghost to {ghost.CurrentRotation}");
                
                // Re-validate after rotation
                UpdateGhostPosition();
            }

            // Handle drop
            if (Input.GetMouseButtonUp(0))
            {
                TryDrop();
            }

            // Handle cancel (right-click or Escape)
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelDrag();
            }
        }

        /// <summary>
        /// Update ghost position to follow mouse and validate placement.
        /// </summary>
        private void UpdateGhostPosition()
        {
            Vector2Int gridPos = GetMouseGridPosition();
            
            // Snap to grid
            Vector2 localPos = gridVisual.GridPositionToLocalPosition(gridPos);
            ghost.SetPosition(localPos);

            // Validate placement
            bool canPlace = inventorySystem.CanAddItem(
                ghost.CurrentItem,
                gridPos,
                ghost.CurrentRotation
            );

            ghost.SetValid(canPlace);
        }

        /// <summary>
        /// Try to drop the item at current mouse position.
        /// </summary>
        private void TryDrop()
        {
            Vector2Int gridPos = GetMouseGridPosition();

            // Try to place item
            bool success = inventorySystem.TryAddItem(
                ghost.CurrentItem,
                gridPos,
                ghost.CurrentRotation,
                out PlacedItem placed
            );

            if (success)
            {
                Log($"Dropped {ghost.CurrentItem.name} at {gridPos} facing {ghost.CurrentRotation}");
            }
            else
            {
                // Failed - return to original position
                Log($"Drop failed, returning to {originalPosition}");
                inventorySystem.TryAddItem(
                    ghost.CurrentItem,
                    originalPosition,
                    originalRotation,
                    out _
                );
            }

            EndDrag();
        }

        /// <summary>
        /// Cancel drag and return item to original position.
        /// </summary>
        private void CancelDrag()
        {
            Log($"Cancelled drag, returning to {originalPosition}");

            // Return to original position
            inventorySystem.TryAddItem(
                ghost.CurrentItem,
                originalPosition,
                originalRotation,
                out _
            );

            EndDrag();
        }

        /// <summary>
        /// End drag operation and cleanup.
        /// </summary>
        private void EndDrag()
        {
            ghost.Hide();
            isDragging = false;
            draggedItem = null;
        }

        /// <summary>
        /// Convert mouse position to grid coordinates.
        /// </summary>
        private Vector2Int GetMouseGridPosition()
        {
            // Get mouse position in screen space
            Vector2 mousePos = Input.mousePosition;

            // Convert to local position within grid visual
            RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                mousePos,
                parentCanvas.worldCamera,
                out Vector2 localPoint
            );

            // Convert local point to grid position
            int gridX = Mathf.FloorToInt(localPoint.x / gridVisual.CellSize);
            int gridY = Mathf.FloorToInt(localPoint.y / gridVisual.CellSize);

            // Clamp to grid bounds
            gridX = Mathf.Clamp(gridX, 0, gridVisual.GridWidth - 1);
            gridY = Mathf.Clamp(gridY, 0, gridVisual.GridHeight - 1);

            return new Vector2Int(gridX, gridY);
        }

        /// <summary>
        /// Debug logging.
        /// </summary>
        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[InventoryDragHandler] {message}");
            }
        }
    }
}
