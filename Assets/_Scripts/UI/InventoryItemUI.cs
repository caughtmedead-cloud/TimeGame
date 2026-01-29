using UnityEngine;
using UnityEngine.UI;
using NewThelos.Inventory.Runtime;
using NewThelos.Inventory.Data;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.UI.Inventory
{
    /// <summary>
    /// Visual representation of an inventory item.
    /// Displays item sprite and highlights occupied cells.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class InventoryItemUI : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private float borderWidth = 3f;
        
        private Image _itemImage;
        private Outline _outline;
        private InventoryItem _itemData;
        private ItemDefinitionSO _definition;
        private InventoryGridUI _parentGrid;
        
        // Public properties for drag handler
        public InventoryItem Item => _itemData;
        public ItemDefinitionSO Definition => _definition;
        public Vector2Int GridPosition => new Vector2Int(_itemData.posX, _itemData.posY);
        public InventoryGridUI ParentGrid => _parentGrid;
        
        // Direct access properties
        public string InstanceId { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public bool IsRotated { get; private set; }
        
        private void Awake()
        {
            _itemImage = GetComponent<Image>();
            _itemImage.raycastTarget = true;
            
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = borderColor;
            _outline.effectDistance = new Vector2(borderWidth, borderWidth);
        }
        
        public void Initialize(InventoryItem item, InventoryGridUI parentGrid, Vector2 cellSize)
        {
            _itemData = item;
            _parentGrid = parentGrid;
            InstanceId = item.instanceId;
            GridX = item.posX;
            GridY = item.posY;
            IsRotated = item.isRotated;
            
            // Get definition from registry
            _definition = ItemDefinitionRegistry.GetItemDefinition(item.itemDefinitionId);
            if (_definition == null)
            {
                Debug.LogError($"[InventoryItemUI] Could not find definition for {item.itemDefinitionId}");
                return;
            }
            
            // Set item sprite
            if (_definition.icon != null)
            {
                _itemImage.sprite = _definition.icon;
                _itemImage.color = Color.white;
            }
            else
            {
                _itemImage.sprite = null;
                _itemImage.color = GetFallbackColor(_definition.itemId);
            }
            
            // Calculate size based on item dimensions
            int width = IsRotated ? _definition.height : _definition.width;
            int height = IsRotated ? _definition.width : _definition.height;
            
            RectTransform rect = GetComponent<RectTransform>();
            
            // CRITICAL: Explicitly set anchor/pivot to prevent positioning issues
            rect.anchorMin = new Vector2(0, 1);  // Top-left anchor
            rect.anchorMax = new Vector2(0, 1);  // Top-left anchor
            rect.pivot = new Vector2(0.5f, 0.5f); // Center pivot
            
            rect.sizeDelta = new Vector2(cellSize.x * width, cellSize.y * height);
            
            // Position in grid
            PositionInGrid(cellSize);
            
            // Highlight occupied cells
            HighlightOccupiedCells(true);
            
            gameObject.name = $"Item_{_definition.itemId}_{InstanceId}";
        }
        
        private void PositionInGrid(Vector2 cellSize)
        {
            RectTransform rect = GetComponent<RectTransform>();
            
            int width = IsRotated ? _definition.height : _definition.width;
            int height = IsRotated ? _definition.width : _definition.height;
            
            // Calculate position accounting for pivot (0.5, 0.5) and multi-cell items
            // X: Start at GridX * cellSize, then add half the item's total width
            float xPos = (GridX * cellSize.x) + (width * cellSize.x * 0.5f);
            
            // Y: Start at GridY * cellSize (going down), then subtract half the item's total height
            // Negative because Y increases downward in UI space
            float yPos = -(GridY * cellSize.y) - (height * cellSize.y * 0.5f);
            
            rect.anchoredPosition = new Vector2(xPos, yPos);
            
            Debug.Log($"[InventoryItemUI] Positioned {_definition.itemId} at grid ({GridX},{GridY})" +
                      $" → UI pos ({xPos:F1},{yPos:F1}) | CellSize: {cellSize} | Item size: {width}x{height}");
        }
        
        private void HighlightOccupiedCells(bool highlight)
        {
            int width = IsRotated ? _definition.height : _definition.width;
            int height = IsRotated ? _definition.width : _definition.height;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    _parentGrid.SetCellOccupied(GridX + x, GridY + y, highlight);
                }
            }
        }
        
        private void OnDestroy()
        {
            if (_parentGrid != null)
            {
                HighlightOccupiedCells(false);
            }
        }
        
        private Color GetFallbackColor(string itemId)
        {
            int hash = itemId.GetHashCode();
            Random.InitState(hash);
            return new Color(Random.value, Random.value, Random.value, 1f);
        }
    }
}