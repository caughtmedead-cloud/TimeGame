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
        
        [Header("Font")]
        [Tooltip("Font used for all procedurally generated labels. Leave empty to use TMP default.")]
        [SerializeField] private TMPro.TMP_FontAsset uiFont;

        [Header("Tile Sprites")]
        [Tooltip("Optional: Tile sprites for grid visualization. If assigned, all created grids will use these sprites.")]
        [SerializeField] private InventoryTileSprites tileSprites;

        [Header("Weight UI")]
        [Tooltip("Sprite used as the weight icon next to each grid's weight readout. Any small icon works — scale, feather, etc. Leave empty for a plain colored square.")]
        [SerializeField] private Sprite weightIconSprite;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        /// <summary>Public accessor for tile sprites (used by drag handlers).</summary>
        public InventoryTileSprites TileSprites => tileSprites;

        /// <summary>Public accessor for the weight icon sprite (used by FloatingContainerWindowManager).</summary>
        public Sprite WeightIconSprite => weightIconSprite;

        /// <summary>Public accessor for the shared UI font (used by ItemInspectPanel).</summary>
        public TMPro.TMP_FontAsset UIFont => uiFont;

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
            
            // CRITICAL: Inject private fields via reflection — these are not serialized on
            // InventoryGridVisual because the factory owns them, not the grid itself.
            var bindingFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            if (tileSprites != null)
            {
                var field = typeof(InventoryGridVisual).GetField("tileSprites", bindingFlags);
                if (field != null)
                {
                    field.SetValue(gridVisual, tileSprites);
                    Log($"Assigned tile sprites to grid '{gridName}'");
                }
            }

            if (uiFont != null)
            {
                var field = typeof(InventoryGridVisual).GetField("uiFont", bindingFlags);
                if (field != null)
                {
                    field.SetValue(gridVisual, uiFont);
                    Log($"Assigned font to grid '{gridName}'");
                }
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
        /// Create a labeled grid with a title above it and a weight row below it.
        /// Layout (top → bottom):
        ///   [Label]
        ///   [Grid]
        ///   [⚖ icon]  [current / max kg]   ← only when maxWeight > 0
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

            // Add VerticalLayoutGroup to stack label + grid + weight row
            VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 6f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Add ContentSizeFitter
            ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Label ────────────────────────────────────────────────────
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(container.transform, false);

            TMPro.TextMeshProUGUI label = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 18;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = Color.white;
            if (uiFont != null) label.font = uiFont;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(200, 28);

            // ── Grid ─────────────────────────────────────────────────────
            InventoryGridVisual grid = CreateGrid(
                gridName,
                width,
                height,
                maxWeight,
                container.transform,
                cellSize
            );

            // ── Weight row (below grid, only when a limit exists) ─────────
            if (maxWeight > 0f && grid != null)
            {
                CreateWeightRow(container.transform, grid.InventorySystem);
            }

            Log($"Created labeled grid container: {gridName} with label '{labelText}'" +
                (maxWeight > 0f ? $" (weight bar: 0/{maxWeight} kg)" : " (no weight limit)"));

            return container;
        }

        /// <summary>
        /// Build the weight row: horizontal group containing a scale icon and weight text.
        /// Attaches an InventoryWeightBar that keeps the text live via events.
        /// </summary>
        private void CreateWeightRow(Transform parent, InventorySystem system)
        {
            // Root object carries the InventoryWeightBar component
            GameObject rowObj = new GameObject("WeightRow");
            rowObj.transform.SetParent(parent, false);

            RectTransform rowRT = rowObj.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(200, 22);

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.spacing = 5f;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = false;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = false;
            hLayout.padding = new RectOffset(0, 0, 0, 0);

            // ── Scale / weight icon ───────────────────────────────────
            GameObject iconObj = new GameObject("WeightIcon");
            iconObj.transform.SetParent(rowObj.transform, false);

            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(16, 16);

            Image iconImage = iconObj.AddComponent<Image>();
            if (weightIconSprite != null)
                iconImage.sprite = weightIconSprite;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            // ── Weight text ───────────────────────────────────────────
            GameObject textObj = new GameObject("WeightText");
            textObj.transform.SetParent(rowObj.transform, false);

            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.sizeDelta = new Vector2(130, 22);

            TMPro.TextMeshProUGUI text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.fontSize = 14;
            text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            if (uiFont != null) text.font = uiFont;

            // ── Wire up the live component ────────────────────────────
            InventoryWeightBar bar = rowObj.AddComponent<InventoryWeightBar>();
            bar.SetReferences(iconImage, text);
            bar.Initialize(system);
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
