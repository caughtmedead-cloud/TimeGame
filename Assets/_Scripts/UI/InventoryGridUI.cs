using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NewThelos.Inventory.Runtime;
using NewThelos.Inventory.Networking;
using NewThelos.Inventory.Data;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.UI.Inventory
{
    public class InventoryGridUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject itemPrefab;
        
        [Header("Grid Settings")]
        [SerializeField] private float aspectRatio = 1f;
        [SerializeField] private float padding = 5f;
        
        private string _gridId;
        private int _width;
        private int _height;
        private Vector2 _cellSize;
        
        private GridCell[,] _cells;
        private RectTransform _gridContainer;
        private RectTransform _itemsContainer;
        
        private Dictionary<string, InventoryItemUI> _itemUIs = new Dictionary<string, InventoryItemUI>();
        private NetworkedPlayerInventory _inventory;
        private Canvas _canvas;
        
        // Public properties for drag handler
        public string GridId => _gridId;
        public NetworkedPlayerInventory Inventory => _inventory;
        public InventoryGrid Grid => _inventory?.GetClientGrid(_gridId);
        public Vector2 CellSize => _cellSize;
        
        public void Initialize(string gridId, int width, int height, NetworkedPlayerInventory inventory, float availableWidth, Canvas canvas)
        {
            _gridId = gridId;
            _width = width;
            _height = height;
            _inventory = inventory;
            _canvas = canvas;
            
            gameObject.name = $"Grid_{gridId}";
            
            CalculateCellSize(availableWidth);
            CreateGridStructure();
            SubscribeToInventoryEvents();
            LoadExistingItems();
        }
        
        private void CalculateCellSize(float availableWidth)
        {
            float totalPadding = padding * (_width + 1);
            float cellWidth = (availableWidth - totalPadding) / _width;
            float cellHeight = cellWidth / aspectRatio;
            
            _cellSize = new Vector2(cellWidth, cellHeight);
            
            RectTransform rect = GetComponent<RectTransform>();
            float totalWidth = (_cellSize.x * _width) + totalPadding;
            float totalHeight = (_cellSize.y * _height) + (padding * (_height + 1));
            rect.sizeDelta = new Vector2(totalWidth, totalHeight);
        }
        
        private void CreateGridStructure()
        {
            GameObject gridContainerObj = new GameObject("GridContainer");
            gridContainerObj.transform.SetParent(transform, false);
            _gridContainer = gridContainerObj.AddComponent<RectTransform>();
            _gridContainer.anchorMin = Vector2.zero;
            _gridContainer.anchorMax = Vector2.one;
            _gridContainer.sizeDelta = Vector2.zero;
            _gridContainer.anchoredPosition = Vector2.zero;
            
            GameObject itemsContainerObj = new GameObject("ItemsContainer");
            itemsContainerObj.transform.SetParent(transform, false);
            _itemsContainer = itemsContainerObj.AddComponent<RectTransform>();
            _itemsContainer.anchorMin = new Vector2(0, 1);
            _itemsContainer.anchorMax = new Vector2(0, 1);
            _itemsContainer.pivot = new Vector2(0, 1);
            _itemsContainer.anchoredPosition = new Vector2(padding, -padding);
            _itemsContainer.sizeDelta = new Vector2(_cellSize.x * _width, _cellSize.y * _height);
            
            _cells = new GridCell[_width, _height];
            
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    CreateCell(x, y);
                }
            }
        }
        
        private void CreateCell(int x, int y)
        {
            GameObject cellObj;
            
            if (cellPrefab != null)
            {
                cellObj = Instantiate(cellPrefab, _gridContainer);
            }
            else
            {
                cellObj = new GameObject($"Cell_{x}_{y}");
                cellObj.transform.SetParent(_gridContainer, false);
                cellObj.AddComponent<Image>();
                cellObj.AddComponent<GridCell>();
            }
            
            GridCell cell = cellObj.GetComponent<GridCell>();
            if (cell == null)
            {
                cell = cellObj.AddComponent<GridCell>();
            }
            
            cell.Initialize(x, y);
            
            RectTransform cellRect = cellObj.GetComponent<RectTransform>();
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);
            cellRect.sizeDelta = _cellSize;
            
            float xPos = padding + (x * (_cellSize.x + padding));
            float yPos = -padding - (y * (_cellSize.y + padding));
            cellRect.anchoredPosition = new Vector2(xPos, yPos);
            
            _cells[x, y] = cell;
        }
        
        public void SetCellOccupied(int x, int y, bool occupied)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _cells[x, y].SetOccupied(occupied);
            }
        }
        
        #region Coordinate Conversion (for Drag & Drop)
        
        /// <summary>
        /// Convert grid coordinates to world position in the items container.
        /// Accounts for item size to center multi-cell items correctly.
        /// </summary>
        public Vector2 GridToWorldPosition(Vector2Int gridPos, ItemDefinitionSO definition, bool isRotated = false)
        {
            int width = isRotated ? definition.height : definition.width;
            int height = isRotated ? definition.width : definition.height;
            
            // Same calculation as InventoryItemUI.PositionInGrid()
            float xPos = (gridPos.x * _cellSize.x) + (width * _cellSize.x * 0.5f);
            float yPos = -(gridPos.y * _cellSize.y) - (height * _cellSize.y * 0.5f);
            
            return new Vector2(xPos, yPos);
        }
        
        /// <summary>
        /// Convert world position to grid coordinates.
        /// localPoint comes from ScreenPointToLocalPointInRectangle on the grid's RectTransform.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector2 localPoint, ItemDefinitionSO definition, bool isRotated = false)
        {
            // CRITICAL FIX: Get the position relative to the items container directly
            // Instead of doing coordinate math, ask Unity to convert to container's local space
            
            RectTransform gridRect = GetComponent<RectTransform>();
            
            // Convert the grid-local point to world position first
            Vector3 worldPoint = gridRect.TransformPoint(localPoint);
            
            // Then convert from world back to items container local space
            Vector2 containerLocalPoint = _itemsContainer.InverseTransformPoint(worldPoint);
            
            int width = isRotated ? definition.height : definition.width;
            int height = isRotated ? definition.width : definition.height;
            
            // containerLocalPoint is now in items container space
            // Items container has anchor/pivot at (0,1) = top-left
            // So containerLocalPoint.x is positive going right, containerLocalPoint.y is negative going down
            
            // Account for item's center pivot
            // Items are rendered with their center at the calculated position
            float itemCenterX = containerLocalPoint.x - (width * _cellSize.x * 0.5f);
            float itemCenterY = -containerLocalPoint.y - (height * _cellSize.y * 0.5f);
            
            // Convert to grid coordinates
            int gridX = Mathf.RoundToInt(itemCenterX / _cellSize.x);
            int gridY = Mathf.RoundToInt(itemCenterY / _cellSize.y);
            
            // Clamp to valid grid range
            gridX = Mathf.Clamp(gridX, 0, _width - width);
            gridY = Mathf.Clamp(gridY, 0, _height - height);
            
            return new Vector2Int(gridX, gridY);
        }
        
        #endregion
        
        private void SubscribeToInventoryEvents()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded += HandleItemAdded;
                _inventory.OnItemRemoved += HandleItemRemoved;
                _inventory.OnItemMoved += HandleItemMoved;
            }
        }
        
        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= HandleItemAdded;
                _inventory.OnItemRemoved -= HandleItemRemoved;
                _inventory.OnItemMoved -= HandleItemMoved;
            }
        }
        
        private void LoadExistingItems()
        {
            if (_inventory == null) return;
            
            var clientGrid = _inventory.GetClientGrid(_gridId);
            if (clientGrid == null) return;
            
            foreach (var item in clientGrid.GetAllItems())
            {
                SpawnItemUI(item);
            }
        }
        
        private void HandleItemAdded(NetworkedItemData itemData)
        {
            if (itemData.gridId != _gridId) return;
            
            var clientGrid = _inventory.GetClientGrid(_gridId);
            if (clientGrid == null) return;
            
            var item = clientGrid.GetItemById(itemData.instanceId);
            if (item != null)
            {
                SpawnItemUI(item);
            }
        }
        
        private void HandleItemRemoved(string instanceId)
        {
            if (_itemUIs.ContainsKey(instanceId))
            {
                DestroyItemUI(instanceId);
            }
        }
        
        /// <summary>
        /// FIXED: Reposition existing UI instead of destroying and recreating.
        /// This prevents icon duplication when items move rapidly.
        /// </summary>
        private void HandleItemMoved(NetworkedItemData itemData)
        {
            // Check if this item is currently in our grid
            bool wasInGrid = _itemUIs.ContainsKey(itemData.instanceId);
            bool nowInGrid = (itemData.gridId == _gridId);
            
            if (wasInGrid && !nowInGrid)
            {
                // Item moved OUT of this grid → destroy UI
                DestroyItemUI(itemData.instanceId);
            }
            else if (!wasInGrid && nowInGrid)
            {
                // Item moved INTO this grid → create new UI
                var clientGrid = _inventory.GetClientGrid(_gridId);
                if (clientGrid != null)
                {
                    var item = clientGrid.GetItemById(itemData.instanceId);
                    if (item != null)
                    {
                        SpawnItemUI(item);
                    }
                }
            }
            else if (wasInGrid && nowInGrid)
            {
                // Item moved WITHIN this grid → reposition existing UI
                RepositionItemUI(itemData.instanceId);
            }
        }
        
        /// <summary>
        /// Reposition an existing item UI to match its current grid data.
        /// This is more efficient than destroying and recreating UI elements.
        /// </summary>
        private void RepositionItemUI(string instanceId)
        {
            if (!_itemUIs.TryGetValue(instanceId, out InventoryItemUI itemUI))
                return;
            
            var clientGrid = _inventory.GetClientGrid(_gridId);
            if (clientGrid == null) return;
            
            var item = clientGrid.GetItemById(instanceId);
            if (item == null) return;
            
            // Update the ItemUI with new position data
            itemUI.UpdatePosition(item.posX, item.posY, item.isRotated, _cellSize);
        }
        
        private void SpawnItemUI(InventoryItem item)
        {
            if (_itemUIs.ContainsKey(item.instanceId))
            {
                Debug.LogWarning($"[InventoryGridUI] Item {item.instanceId} already has UI in grid {_gridId}");
                return;
            }
            
            GameObject itemObj;
            
            if (itemPrefab != null)
            {
                itemObj = Instantiate(itemPrefab, _itemsContainer);
            }
            else
            {
                itemObj = new GameObject($"Item_{item.instanceId}");
                itemObj.transform.SetParent(_itemsContainer, false);
                Image img = itemObj.AddComponent<Image>();
                img.raycastTarget = true;
                itemObj.AddComponent<InventoryItemUI>();
            }
            
            InventoryItemUI itemUI = itemObj.GetComponent<InventoryItemUI>();
            if (itemUI == null)
            {
                itemUI = itemObj.AddComponent<InventoryItemUI>();
            }
            
            itemUI.Initialize(item, this, _cellSize);
            
            // Add drag handler
            InventoryItemDragHandler dragHandler = itemObj.GetComponent<InventoryItemDragHandler>();
            if (dragHandler == null)
            {
                dragHandler = itemObj.AddComponent<InventoryItemDragHandler>();
            }
            dragHandler.Initialize(this, _canvas);
            
            _itemUIs[item.instanceId] = itemUI;
        }
        
        private void DestroyItemUI(string instanceId)
        {
            if (_itemUIs.TryGetValue(instanceId, out InventoryItemUI itemUI))
            {
                _itemUIs.Remove(instanceId);
                Destroy(itemUI.gameObject);
            }
        }
    }
}