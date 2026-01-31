using UnityEngine;
using UnityEngine.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Factory for creating InventoryGridVisual GameObjects programmatically.
    /// Handles all component setup, configuration, and initialization.
    /// </summary>
    public class InventoryGridFactory : MonoBehaviour
    {
        [Header("Grid Prefab (Optional)")]
        [Tooltip("If provided, will instantiate this prefab. Otherwise creates from scratch.")]
        [SerializeField] private GameObject gridPrefab;

        [Header("Default Visual Settings")]
        [SerializeField] private float defaultCellSize = 64f;
        [SerializeField] private Color gridCellColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private Color gridBorderColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        [SerializeField] private float borderThickness = 1f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        /// <summary>
        /// Create a complete InventoryGridVisual GameObject with all components configured.
        /// </summary>
        /// <param name="gridName">Name for the grid GameObject</param>
        /// <param name="width">Grid width in cells</param>
        /// <param name="height">Grid height in cells</param>
        /// <param name="maxWeight">Maximum weight capacity (0 = unlimited)</param>
        /// <param name="parent">Parent transform (typically scroll content)</param>
        /// <param name="cellSize">Size of each cell in pixels (default: 64)</param>
        /// <returns>Initialized InventoryGridVisual component</returns>
        public InventoryGridVisual CreateGrid(
            string gridName,
            int width,
            int height,
            float maxWeight = 0f,
            Transform parent = null,
            float? cellSize = null)
        {
            float actualCellSize = cellSize ?? defaultCellSize;

            // Create or instantiate grid GameObject
            GameObject gridObj;
            if (gridPrefab != null)
            {
                gridObj = Instantiate(gridPrefab, parent);
                Log($"Instantiated grid from prefab: {gridName}");
            }
            else
            {
                gridObj = CreateGridFromScratch(parent);
                Log($"Created grid from scratch: {gridName}");
            }

            gridObj.name = gridName;

            // Get or add RectTransform
            RectTransform rectTransform = gridObj.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = gridObj.AddComponent<RectTransform>();
            }

            // Configure RectTransform - CRITICAL: Bottom-left pivot
            ConfigureGridRectTransform(rectTransform, width, height, actualCellSize);

            // Get or add InventoryGridVisual component
            InventoryGridVisual gridVisual = gridObj.GetComponent<InventoryGridVisual>();
            if (gridVisual == null)
            {
                gridVisual = gridObj.AddComponent<InventoryGridVisual>();
            }

            // Create InventorySystem
            Vector3 gridOrigin = Vector3.zero; // Not used for UI
            InventorySystem inventorySystem = new InventorySystem(
                width,
                height,
                actualCellSize,
                gridOrigin,
                maxWeight
            );

            // Initialize grid visual
            gridVisual.Initialize(inventorySystem);

            Log($"Created grid '{gridName}': {width}x{height} cells, {actualCellSize}px/cell, {maxWeight}kg max weight");

            return gridVisual;
        }

        /// <summary>
        /// Create a grid GameObject from scratch with all required components.
        /// </summary>
        private GameObject CreateGridFromScratch(Transform parent)
        {
            GameObject gridObj = new GameObject("InventoryGrid");
            
            if (parent != null)
            {
                gridObj.transform.SetParent(parent, false);
            }

            // Add RectTransform
            RectTransform rectTransform = gridObj.AddComponent<RectTransform>();

            // Optional: Add background image
            Image background = gridObj.AddComponent<Image>();
            background.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            background.raycastTarget = false; // Don't block clicks

            return gridObj;
        }

        /// <summary>
        /// Configure RectTransform for grid with proper anchoring and sizing.
        /// CRITICAL: Pivot must be (0, 0) for bottom-left origin.
        /// </summary>
        private void ConfigureGridRectTransform(RectTransform rectTransform, int width, int height, float cellSize)
        {
            // Anchors: Top-center (so grids stack downward)
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);

            // Pivot: Bottom-left (required for grid coordinate system)
            rectTransform.pivot = new Vector2(0f, 0f);

            // Size based on grid dimensions
            float pixelWidth = width * cellSize;
            float pixelHeight = height * cellSize;
            rectTransform.sizeDelta = new Vector2(pixelWidth, pixelHeight);

            // Position will be set by parent's LayoutGroup
            rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Create a labeled grid with a title above it.
        /// </summary>
        public GameObject CreateLabeledGrid(
            string gridName,
            string labelText,
            int width,
            int height,
            float maxWeight = 0f,
            Transform parent = null,
            float? cellSize = null)
        {
            // Create container
            GameObject container = new GameObject($"{gridName}_Container");
            
            if (parent != null)
            {
                container.transform.SetParent(parent, false);
            }

            RectTransform containerRect = container.AddComponent<RectTransform>();
            
            // Configure container
            containerRect.anchorMin = new Vector2(0.5f, 1f);
            containerRect.anchorMax = new Vector2(0.5f, 1f);
            containerRect.pivot = new Vector2(0.5f, 1f);

            // Add VerticalLayoutGroup to stack label + grid
            VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 5f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Add ContentSizeFitter
            ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);
            
            TMPro.TextMeshProUGUI label = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 18;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(200, 30);

            // Create grid as child of container
            InventoryGridVisual grid = CreateGrid(
                gridName,
                width,
                height,
                maxWeight,
                container.transform,
                cellSize
            );

            Log($"Created labeled grid container: {gridName} with label '{labelText}'");

            return container;
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[InventoryGridFactory] {message}");
            }
        }
    }
}
