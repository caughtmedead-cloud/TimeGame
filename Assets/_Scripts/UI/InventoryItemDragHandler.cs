using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NewThelos.Inventory.Runtime;
using NewThelos.Inventory.Networking;
using NewThelos.Inventory.Data;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.UI.Inventory
{
    /// <summary>
    /// Handles drag & drop for inventory items with real-time collision detection and visual feedback.
    /// Supports item swapping when dropping on occupied slots and rotation during drag.
    /// </summary>
    public class InventoryItemDragHandler : MonoBehaviour, 
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Visual Feedback")]
        [SerializeField] private float dragAlpha = 0.6f;
        [SerializeField] private Color validPlacementColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("References")]
        private InventoryItemUI itemUI;
        private InventoryGridUI gridUI;
        private RectTransform rectTransform;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private Image itemImage;
        
        // Drag state
        private bool isDragging;
        private Vector2 originalPosition;
        private Vector2Int originalGridPosition;
        private Transform originalParent;
        private int originalSiblingIndex;
        private bool originalRotation; // Track original rotation state
        private bool currentRotation;  // Track current rotation during drag
        
        // Preview state
        private GameObject previewObject;
        private Image previewImage;
        private bool isValidPlacement;
        private Vector2Int previewGridPosition = new Vector2Int(-1, -1);
        private InventoryItem collidingItem; // Item we're hovering over for potential swap

        #region Initialization

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            itemImage = GetComponent<Image>();
            itemUI = GetComponent<InventoryItemUI>();
        }

        /// <summary>
        /// Initialize with required references
        /// </summary>
        public void Initialize(InventoryGridUI grid, Canvas parentCanvas)
        {
            gridUI = grid;
            canvas = parentCanvas;
        }

        #endregion

        #region Drag Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Only owner can drag items
            if (itemUI == null || itemUI.Item == null)
            {
                Debug.LogWarning("[InventoryItemDragHandler] Cannot drag - no item data");
                return;
            }

            // TODO: Check if player owns this inventory (IsOwner check on NetworkedPlayerInventory)
            
            isDragging = true;
            
            // Store original state
            originalPosition = rectTransform.anchoredPosition;
            originalGridPosition = itemUI.GridPosition;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();
            originalRotation = itemUI.Item.isRotated;
            currentRotation = originalRotation; // Start with original rotation
            
            // Visual feedback - make semi-transparent
            canvasGroup.alpha = dragAlpha;
            canvasGroup.blocksRaycasts = false;
            
            // Move to canvas root for proper rendering above everything
            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();
            
            // Create preview object
            CreatePreview();
            
            ItemDefinitionSO def = itemUI.Definition;
            Debug.Log($"[InventoryItemDragHandler] Started dragging {def.displayName} from ({originalGridPosition.x},{originalGridPosition.y})");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            
            // Follow cursor
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
            
            // Check for rotation input (R key)
            if (Input.GetKeyDown(KeyCode.R))
            {
                ToggleRotation();
            }
            
            // Update preview position and validity
            UpdatePreviewPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            
            isDragging = false;
            
            // Restore visual state
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            
            // Destroy preview
            if (previewObject != null)
            {
                Destroy(previewObject);
                previewObject = null;
            }
            
            // Calculate drop position
            Vector2Int dropGridPosition = GetGridPositionFromMouse(eventData);
            
            // Validate and execute drop
            // Allow drop if: valid placement AND (position changed OR rotation changed)
            bool positionChanged = dropGridPosition != originalGridPosition;
            bool rotationChanged = currentRotation != originalRotation;
            
            if (isValidPlacement && (positionChanged || rotationChanged))
            {
                ItemDefinitionSO def = itemUI.Definition;
                string rotInfo = rotationChanged ? $" (rotated: {currentRotation})" : "";
                Debug.Log($"[InventoryItemDragHandler] Dropping {def.displayName} at ({dropGridPosition.x},{dropGridPosition.y}){rotInfo}");
                ExecuteDrop(dropGridPosition, currentRotation);
            }
            else
            {
                Debug.Log($"[InventoryItemDragHandler] Invalid drop or no changes, returning to ({originalGridPosition.x},{originalGridPosition.y})");
                ReturnToOriginalPosition();
            }
        }

        #endregion

        #region Rotation

        /// <summary>
        /// Toggle rotation state during drag
        /// </summary>
        private void ToggleRotation()
        {
            currentRotation = !currentRotation;
            
            ItemDefinitionSO def = itemUI.Definition;
            
            // Update the dragged item's visual rotation
            // Swap width/height visually
            int newWidth = currentRotation ? def.height : def.width;
            int newHeight = currentRotation ? def.width : def.height;
            
            Vector2 cellSize = gridUI.CellSize;
            rectTransform.sizeDelta = new Vector2(newWidth * cellSize.x, newHeight * cellSize.y);
            
            // Update preview to match new rotation
            UpdatePreviewRotation();
            
            Debug.Log($"[InventoryItemDragHandler] Rotated {def.displayName} to {currentRotation}");
        }

        /// <summary>
        /// Update preview object to match current rotation
        /// </summary>
        private void UpdatePreviewRotation()
        {
            if (previewObject == null || itemUI?.Definition == null) return;
            
            ItemDefinitionSO def = itemUI.Definition;
            int newWidth = currentRotation ? def.height : def.width;
            int newHeight = currentRotation ? def.width : def.height;
            
            Vector2 cellSize = gridUI.CellSize;
            RectTransform previewRect = previewObject.GetComponent<RectTransform>();
            previewRect.sizeDelta = new Vector2(newWidth * cellSize.x, newHeight * cellSize.y);
        }

        #endregion

        #region Preview System

        private void CreatePreview()
        {
            if (itemUI == null || itemUI.Item == null || itemUI.Definition == null) return;
            
            // Create preview GameObject
            previewObject = new GameObject("ItemPreview");
            previewObject.transform.SetParent(gridUI.transform, false);
            
            // Setup RectTransform with same anchor/pivot as grid items
            RectTransform previewRect = previewObject.AddComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0, 1);  // Top-left
            previewRect.anchorMax = new Vector2(0, 1);  // Top-left
            previewRect.pivot = new Vector2(0.5f, 0.5f); // Center
            previewRect.sizeDelta = rectTransform.sizeDelta;
            
            // Setup image
            previewImage = previewObject.AddComponent<Image>();
            previewImage.sprite = itemUI.Definition.icon;
            previewImage.color = validPlacementColor;
            
            // Behind other items but above grid background
            previewObject.transform.SetAsFirstSibling();
        }

        private void UpdatePreviewPosition(PointerEventData eventData)
        {
            if (previewObject == null || gridUI == null) return;

            // CRITICAL: Get grid's RectTransform (not items container)
            RectTransform gridRect = gridUI.GetComponent<RectTransform>();

            // Convert screen space to grid local space
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,                    // Convert relative to GRID RectTransform
                eventData.position,          // Mouse position in screen space
                eventData.pressEventCamera,  // Camera (null for Screen Space - Overlay)
                out Vector2 localPoint       // Output: position relative to grid
            );

            if (!success)
            {
                Debug.LogWarning("[InventoryItemDragHandler] Failed to convert screen to local point");
                return;
            }

            // Get item definition for size calculations
            ItemDefinitionSO definition = itemUI.Definition;
            if (definition == null) return;

            // Convert local point to grid coordinates using CURRENT rotation state
            Vector2Int targetGridPos = gridUI.WorldToGridPosition(
                localPoint,           // Position relative to grid RectTransform
                definition,
                currentRotation       // Use current rotation, not original!
            );

            // Check if placement is valid at this position with current rotation
            CheckPlacementValidity(targetGridPos);

            // Update preview color based on validity
            previewImage.color = isValidPlacement ? validPlacementColor : invalidPlacementColor;

            // Convert grid coordinates to world position for preview
            Vector2 previewWorldPos = gridUI.GridToWorldPosition(
                targetGridPos,
                definition,
                currentRotation       // Use current rotation
            );

            // Position the preview
            RectTransform previewRect = previewObject.GetComponent<RectTransform>();
            previewRect.anchoredPosition = previewWorldPos;
        }

        private void CheckPlacementValidity(Vector2Int gridPos)
        {
            collidingItem = null;
            isValidPlacement = false;
            
            if (itemUI?.Item == null || itemUI.Definition == null)
            {
                return;
            }
            
            InventoryGrid grid = gridUI.Grid;
            ItemDefinitionSO definition = itemUI.Definition;
            
            // Use CURRENT rotation state for validation
            int width = currentRotation ? definition.height : definition.width;
            int height = currentRotation ? definition.width : definition.height;
            
            // Check bounds
            if (gridPos.x < 0 || gridPos.y < 0 ||
                gridPos.x + width > grid.Width ||
                gridPos.y + height > grid.Height)
            {
                return; // Out of bounds
            }
            
            // Check collision with other items (excluding self)
            foreach (InventoryItem otherItem in grid.GetAllItems())
            {
                // Skip self
                if (otherItem.instanceId == itemUI.Item.instanceId)
                    continue;
                
                // Get other item's definition
                ItemDefinitionSO otherDef = ItemDefinitionRegistry.GetItemDefinition(otherItem.itemDefinitionId);
                if (otherDef == null) continue;
                
                int otherWidth = otherItem.isRotated ? otherDef.height : otherDef.width;
                int otherHeight = otherItem.isRotated ? otherDef.width : otherDef.height;
                
                // Check AABB collision
                if (gridPos.x < otherItem.posX + otherWidth &&
                    gridPos.x + width > otherItem.posX &&
                    gridPos.y < otherItem.posY + otherHeight &&
                    gridPos.y + height > otherItem.posY)
                {
                    collidingItem = otherItem;
                    
                    // Check if we can swap with this item
                    // For swap to be valid, the colliding item must fit at our original position
                    isValidPlacement = CheckCanSwap(otherItem, otherDef);
                    return;
                }
            }
            
            // No collision - placement is valid
            isValidPlacement = true;
        }
        
        /// <summary>
        /// Check if we can swap by validating the other item can fit at our original position
        /// </summary>
        private bool CheckCanSwap(InventoryItem otherItem, ItemDefinitionSO otherDef)
        {
            InventoryGrid grid = gridUI.Grid;
            
            int otherWidth = otherItem.isRotated ? otherDef.height : otherDef.width;
            int otherHeight = otherItem.isRotated ? otherDef.width : otherDef.height;
            
            // Check bounds at original position
            if (originalGridPosition.x < 0 || originalGridPosition.y < 0 ||
                originalGridPosition.x + otherWidth > grid.Width ||
                originalGridPosition.y + otherHeight > grid.Height)
            {
                return false;
            }
            
            // Check collision with other items at original position (excluding both items involved in swap)
            foreach (InventoryItem gridItem in grid.GetAllItems())
            {
                // Skip the item we're dragging and the item we're swapping with
                if (gridItem.instanceId == itemUI.Item.instanceId ||
                    gridItem.instanceId == otherItem.instanceId)
                    continue;
                
                ItemDefinitionSO gridItemDef = ItemDefinitionRegistry.GetItemDefinition(gridItem.itemDefinitionId);
                if (gridItemDef == null) continue;
                
                int gridItemWidth = gridItem.isRotated ? gridItemDef.height : gridItemDef.width;
                int gridItemHeight = gridItem.isRotated ? gridItemDef.width : gridItemDef.height;
                
                // Check AABB collision with other item at original position
                if (originalGridPosition.x < gridItem.posX + gridItemWidth &&
                    originalGridPosition.x + otherWidth > gridItem.posX &&
                    originalGridPosition.y < gridItem.posY + gridItemHeight &&
                    originalGridPosition.y + otherHeight > gridItem.posY)
                {
                    return false; // Collision - can't swap
                }
            }
            
            return true; // No collision - swap is valid
        }

        #endregion

        #region Position Calculations

        private Vector2Int GetGridPositionFromMouse(PointerEventData eventData)
        {
            // Convert screen position to grid local position
            RectTransform gridRect = gridUI.GetComponent<RectTransform>();
            
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );
            
            // Convert to grid coordinates using CURRENT rotation
            return gridUI.WorldToGridPosition(localPoint, itemUI.Definition, currentRotation);
        }

        #endregion

        #region Drop Execution

        private void ExecuteDrop(Vector2Int targetPosition, bool targetRotation)
        {
            if (itemUI?.Item == null)
            {
                ReturnToOriginalPosition();
                return;
            }
            
            // Get the NetworkedPlayerInventory component
            NetworkedPlayerInventory inventory = gridUI.Inventory;
            if (inventory == null)
            {
                Debug.LogError("[InventoryItemDragHandler] No NetworkedPlayerInventory found!");
                ReturnToOriginalPosition();
                return;
            }
            
            // Send move request to server with new rotation
            string gridId = gridUI.GridId;
            string itemId = itemUI.Item.instanceId;
            
            inventory.MoveItemWithSwap_ServerRpc(itemId, gridId, targetPosition.x, targetPosition.y, targetRotation);
            
            Debug.Log($"[InventoryItemDragHandler] Sent move request: {itemId} to ({targetPosition.x},{targetPosition.y}) rot:{targetRotation}");
            
            // Return to original position temporarily
            // The server will send back the update which will reposition correctly
            ReturnToOriginalPosition();
        }

        private void ReturnToOriginalPosition()
        {
            // Restore parent
            transform.SetParent(originalParent, true);
            transform.SetSiblingIndex(originalSiblingIndex);
            
            // Restore position
            rectTransform.anchoredPosition = originalPosition;
            
            // Restore original size (in case rotation changed it)
            if (itemUI?.Definition != null)
            {
                ItemDefinitionSO def = itemUI.Definition;
                int width = originalRotation ? def.height : def.width;
                int height = originalRotation ? def.width : def.height;
                Vector2 cellSize = gridUI.CellSize;
                rectTransform.sizeDelta = new Vector2(width * cellSize.x, height * cellSize.y);
            }
            
            // Restore visual state
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        #endregion

        #region Hover Handlers (for future tooltip system)

        public void OnPointerEnter(PointerEventData eventData)
        {
            // TODO: Show tooltip with item info
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // TODO: Hide tooltip
        }

        #endregion
    }
}