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
        /// Convert world position (local to items container) to grid coordinates.
        /// Uses top-left cell as the grid position for consistency.
        /// </summary>
        public Vector2Int WorldToGridPosition(Vector2 localPoint, ItemDefinitionSO definition, bool isRotated = false)
        {
            // localPoint is already relative to the grid RectTransform, but we need it relative to items container
            // Account for the items container offset
            Vector2 adjustedPoint = localPoint - _itemsContainer.anchoredPosition;
            
            int width = isRotated ? definition.height : definition.width;
            int height = isRotated ? definition.width : definition.height;
            
            // Remove the centering offset to get top-left position
            float topLeftX = adjustedPoint.x - (width * _cellSize.x * 0.5f);
            float topLeftY = adjustedPoint.y + (height * _cellSize.y * 0.5f);
            
            // Convert to grid coordinates
            int gridX = Mathf.RoundToInt(topLeftX / _cellSize.x);
            int gridY = Mathf.RoundToInt(-topLeftY / _cellSize.y);
            
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
        
        private void HandleItemMoved(NetworkedItemData itemData)
        {
            UnityEngine.Debug.Log($"[InventoryGridUI] HandleItemMoved START: {itemData.itemDefinitionId} | GridId: {itemData.gridId} | Pos: ({itemData.posX},{itemData.posY})");
            
            // First check if item was in this grid (remove it)
            if (_itemUIs.ContainsKey(itemData.instanceId))
            {
                UnityEngine.Debug.Log($"[InventoryGridUI] Destroying old UI for {itemData.instanceId.Substring(0,8)}...");
                DestroyItemUI(itemData.instanceId);
            }
            
            // Then check if item moved TO this grid (add it)
            if (itemData.gridId == _gridId)
            {
                var clientGrid = _inventory.GetClientGrid(_gridId);
                if (clientGrid == null)
                {
                    UnityEngine.Debug.LogError($"[InventoryGridUI] Could not get client grid '{_gridId}'!");
                    return;
                }
                
                UnityEngine.Debug.Log($"[InventoryGridUI] Calling GetItemById for {itemData.instanceId.Substring(0,8)}...");
                var item = clientGrid.GetItemById(itemData.instanceId);
                
                if (item != null)
                {
                    UnityEngine.Debug.Log($"[InventoryGridUI] GetItemById returned: Pos=({item.posX},{item.posY}) | Expected: ({itemData.posX},{itemData.posY})");
                    
                    if (item.posX != itemData.posX || item.posY != itemData.posY)
                    {
                        UnityEngine.Debug.LogError($"[InventoryGridUI] ❌ POSITION MISMATCH!");
                        UnityEngine.Debug.LogError($"   Expected from NetworkedItemData: ({itemData.posX},{itemData.posY})");
                        UnityEngine.Debug.LogError($"   Got from GetItemById: ({item.posX},{item.posY})");
                        UnityEngine.Debug.LogError($"   This means the grid has stale data!");
                    }
                    
                    SpawnItemUI(item);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[InventoryGridUI] ❌ GetItemById returned null for {itemData.instanceId.Substring(0,8)}...");
                }
            }
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