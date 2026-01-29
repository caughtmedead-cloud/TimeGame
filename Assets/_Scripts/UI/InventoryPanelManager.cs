using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using NewThelos.Inventory.Networking;
using FishNet.Object;

namespace NewThelos.UI.Inventory
{
    [RequireComponent(typeof(RectTransform))]
    public class InventoryPanelManager : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkedPlayerInventory inventory;
        [SerializeField] private GameObject inventoryGridPrefab;
        
        [Header("Layout Settings")]
        [SerializeField] private float gridSpacing = 20f;
        [SerializeField] private float panelPadding = 10f;
        [SerializeField] private float gridWidthPercentage = 0.9f;
        
        [Header("Scroll Settings")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentContainer;
        
        private List<InventoryGridUI> _spawnedGrids = new List<InventoryGridUI>();
        private RectTransform _rectTransform;
        
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            
            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>();
            }
            
            if (contentContainer == null && scrollRect != null)
            {
                contentContainer = scrollRect.content;
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }
            
            if (inventory == null)
            {
                inventory = GetComponentInParent<NetworkedPlayerInventory>();
            }
            
            if (inventory == null)
            {
                Debug.LogError("[InventoryPanelManager] NetworkedPlayerInventory not found!");
                return;
            }
            
            StartCoroutine(InitializeAfterInventory());
        }
        
        private System.Collections.IEnumerator InitializeAfterInventory()
        {
            yield return null;
            InitializeGrids();
        }
        
        private void InitializeGrids()
        {
            if (inventory == null || inventory.GridConfigs == null)
            {
                Debug.LogError("[InventoryPanelManager] Inventory or GridConfigs is null");
                return;
            }
            
            ClearGrids();
            
            float panelWidth = _rectTransform.rect.width;
            float availableWidth = panelWidth * gridWidthPercentage;
            float totalHeight = panelPadding;
            
            // FIXED: Use GridConfig from NetworkedPlayerInventory directly
            foreach (var gridConfig in inventory.GridConfigs)
            {
                InventoryGridUI gridUI = SpawnGrid(gridConfig, availableWidth, totalHeight);
                
                if (gridUI != null)
                {
                    _spawnedGrids.Add(gridUI);
                    totalHeight += gridUI.GetComponent<RectTransform>().sizeDelta.y + gridSpacing;
                }
            }
            
            if (contentContainer != null)
            {
                totalHeight += panelPadding;
                contentContainer.sizeDelta = new Vector2(contentContainer.sizeDelta.x, totalHeight);
            }
            
            Debug.Log($"[InventoryPanelManager] Initialized {_spawnedGrids.Count} grids");
        }
        
        // FIXED: Accept GridConfig directly from NetworkedPlayerInventory
        private InventoryGridUI SpawnGrid(GridConfig config, float availableWidth, float yOffset)
        {
            GameObject gridObj;
            
            if (inventoryGridPrefab != null)
            {
                gridObj = Instantiate(inventoryGridPrefab, contentContainer);
            }
            else
            {
                gridObj = new GameObject($"Grid_{config.gridId}");
                gridObj.transform.SetParent(contentContainer, false);
                gridObj.AddComponent<RectTransform>();
                gridObj.AddComponent<InventoryGridUI>();
            }
            
            InventoryGridUI gridUI = gridObj.GetComponent<InventoryGridUI>();
            if (gridUI == null)
            {
                gridUI = gridObj.AddComponent<InventoryGridUI>();
            }
            
            RectTransform gridRect = gridObj.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 1);
            gridRect.anchorMax = new Vector2(0.5f, 1);
            gridRect.pivot = new Vector2(0.5f, 1);
            gridRect.anchoredPosition = new Vector2(0, -yOffset);
            
            // Get the canvas reference for drag handler
            Canvas canvas = GetComponentInParent<Canvas>();
            
            gridUI.Initialize(config.gridId, config.width, config.height, inventory, availableWidth, canvas);
            
            return gridUI;
        }
        
        private void ClearGrids()
        {
            foreach (var grid in _spawnedGrids)
            {
                if (grid != null)
                {
                    Destroy(grid.gameObject);
                }
            }
            
            _spawnedGrids.Clear();
        }
        
        public InventoryGridUI GetGrid(string gridId)
        {
            return _spawnedGrids.Find(g => g.name.Contains(gridId));
        }
        
        public void RefreshGrids()
        {
            InitializeGrids();
        }
        
        private void OnDestroy()
        {
            ClearGrids();
        }
    }
}