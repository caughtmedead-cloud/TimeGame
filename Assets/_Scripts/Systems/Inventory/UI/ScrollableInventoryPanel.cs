using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages a scrollable panel that can dynamically spawn and remove inventory grids.
    /// Handles scroll rect setup, vertical layout, and grid lifecycle.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ScrollableInventoryPanel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Grid factory for creating grids")]
        [SerializeField] private InventoryGridFactory gridFactory;

        [Tooltip("Drag handler to register grids with")]
        [SerializeField] private InventoryDragHandler dragHandler;

        [Header("Scroll Settings")]
        [SerializeField] private bool vertical = true;
        [SerializeField] private bool horizontal = false;
        [SerializeField] private float scrollSensitivity = 20f;

        [Header("Layout Settings")]
        [SerializeField] private float spacing = 20f;
        [SerializeField] private RectOffset padding = new RectOffset(10, 10, 10, 10);

        [Header("Auto-Setup")]
        [SerializeField] private bool autoSetupOnAwake = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // Internal references
        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private VerticalLayoutGroup layoutGroup;

        // Spawned grids tracking
        private Dictionary<string, InventoryGridVisual> spawnedGrids = new Dictionary<string, InventoryGridVisual>();

        /// <summary>
        /// Get the content RectTransform where grids are spawned
        /// </summary>
        public RectTransform Content => content;

        /// <summary>
        /// Get all currently spawned grids
        /// </summary>
        public IReadOnlyDictionary<string, InventoryGridVisual> SpawnedGrids => spawnedGrids;

        private void Awake()
        {
            if (autoSetupOnAwake)
            {
                SetupScrollRect();
            }
        }

        /// <summary>
        /// Set up the scroll rect structure if not already configured.
        /// Creates: ScrollRect → Viewport → Content hierarchy
        /// </summary>
        public void SetupScrollRect()
        {
            // Check if already set up
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect != null && content != null)
            {
                Log("Scroll rect already set up");
                return;
            }

            // Create or get ScrollRect component
            if (scrollRect == null)
            {
                scrollRect = gameObject.AddComponent<ScrollRect>();
            }

            // Configure ScrollRect
            scrollRect.vertical = vertical;
            scrollRect.horizontal = horizontal;
            scrollRect.scrollSensitivity = scrollSensitivity;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;

            // Create Viewport
            viewport = CreateViewport();
            scrollRect.viewport = viewport;

            // Create Content
            content = CreateContent();
            scrollRect.content = content;

            // Add Mask to viewport
            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage == null)
            {
                viewportImage = viewport.gameObject.AddComponent<Image>();
                viewportImage.color = new Color(0, 0, 0, 0.01f); // Nearly transparent, required for Mask
            }
            
            Mask mask = viewport.GetComponent<Mask>();
            if (mask == null)
            {
                mask = viewport.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }

            // Setup LayoutGroup on content
            layoutGroup = content.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            ConfigureLayoutGroup();

            // Add ContentSizeFitter to content
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            Log("Scroll rect setup complete");
        }

        private RectTransform CreateViewport()
        {
            // Check if viewport already exists
            Transform existingViewport = transform.Find("Viewport");
            if (existingViewport != null)
            {
                return existingViewport.GetComponent<RectTransform>();
            }

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            
            // Stretch to fill parent
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;

            return viewportRect;
        }

        private RectTransform CreateContent()
        {
            // Check if content already exists
            Transform existingContent = viewport.Find("Content");
            if (existingContent != null)
            {
                return existingContent.GetComponent<RectTransform>();
            }

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewport, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            
            // Anchor to top
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0); // Height will be controlled by ContentSizeFitter

            return contentRect;
        }

        private void ConfigureLayoutGroup()
        {
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.spacing = spacing;
            layoutGroup.padding = padding;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
        }

        /// <summary>
        /// Spawn a new inventory grid in this panel
        /// </summary>
        public InventoryGridVisual SpawnGrid(
            string gridName,
            int width,
            int height,
            float maxWeight = 0f,
            float? cellSize = null,
            bool withLabel = false,
            string labelText = null)
        {
            if (gridFactory == null)
            {
                Debug.LogError("[ScrollableInventoryPanel] No GridFactory assigned! Cannot spawn grid.");
                return null;
            }

            if (content == null)
            {
                Debug.LogError("[ScrollableInventoryPanel] Content not initialized! Call SetupScrollRect first.");
                return null;
            }

            // Check if grid with this name already exists
            if (spawnedGrids.ContainsKey(gridName))
            {
                Debug.LogWarning($"[ScrollableInventoryPanel] Grid '{gridName}' already exists!");
                return spawnedGrids[gridName];
            }

            InventoryGridVisual grid;

            if (withLabel)
            {
                // Create labeled grid container
                GameObject container = gridFactory.CreateLabeledGrid(
                    gridName,
                    labelText ?? gridName,
                    width,
                    height,
                    maxWeight,
                    content,
                    cellSize
                );

                // Get the grid component from the container's children
                grid = container.GetComponentInChildren<InventoryGridVisual>();
            }
            else
            {
                // Create standalone grid
                grid = gridFactory.CreateGrid(
                    gridName,
                    width,
                    height,
                    maxWeight,
                    content,
                    cellSize
                );
            }

            if (grid == null)
            {
                Debug.LogError($"[ScrollableInventoryPanel] Failed to create grid '{gridName}'");
                return null;
            }

            // Track spawned grid
            spawnedGrids[gridName] = grid;

            // Register with drag handler
            if (dragHandler != null)
            {
                dragHandler.RegisterDropTarget(grid);
                Log($"Registered grid '{gridName}' with drag handler");
            }

            Log($"Spawned grid '{gridName}' ({width}x{height})");

            return grid;
        }

        /// <summary>
        /// Remove a spawned grid by name
        /// </summary>
        public bool RemoveGrid(string gridName)
        {
            if (!spawnedGrids.ContainsKey(gridName))
            {
                Debug.LogWarning($"[ScrollableInventoryPanel] Grid '{gridName}' not found");
                return false;
            }

            InventoryGridVisual grid = spawnedGrids[gridName];

            // Unregister from drag handler
            if (dragHandler != null)
            {
                dragHandler.UnregisterDropTarget(grid);
            }

            // Destroy GameObject (either the grid itself or its parent container)
            GameObject toDestroy = grid.transform.parent.name.EndsWith("_Container") 
                ? grid.transform.parent.gameObject 
                : grid.gameObject;

            Destroy(toDestroy);
            spawnedGrids.Remove(gridName);

            Log($"Removed grid '{gridName}'");
            return true;
        }

        /// <summary>
        /// Remove all spawned grids
        /// </summary>
        public void ClearAllGrids()
        {
            List<string> gridNames = new List<string>(spawnedGrids.Keys);
            
            foreach (string gridName in gridNames)
            {
                RemoveGrid(gridName);
            }

            Log("Cleared all grids");
        }

        /// <summary>
        /// Get a spawned grid by name
        /// </summary>
        public InventoryGridVisual GetGrid(string gridName)
        {
            spawnedGrids.TryGetValue(gridName, out InventoryGridVisual grid);
            return grid;
        }

        /// <summary>
        /// Check if a grid with the given name exists
        /// </summary>
        public bool HasGrid(string gridName)
        {
            return spawnedGrids.ContainsKey(gridName);
        }

        /// <summary>
        /// Scroll to top of the panel
        /// </summary>
        public void ScrollToTop()
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>
        /// Scroll to bottom of the panel
        /// </summary>
        public void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[ScrollableInventoryPanel:{gameObject.name}] {message}");
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Update layout settings if already set up
            if (layoutGroup != null)
            {
                ConfigureLayoutGroup();
            }
        }
        #endif
    }
}
