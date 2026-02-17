using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// A draggable floating window that displays a container's inventory grid.
    /// Used for opening containers within your inventory (backpacks, pouches, etc.)
    /// </summary>
    public class FloatingContainerWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform gridContainer;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private bool verboseLogging = true;

        // The container item this window displays
        private GridPlacement.PlacedItem containerItem;

        // The spawned grid visual
        private InventoryGridVisual gridVisual;

        // Drag state
        private Vector2 dragOffset;
        private bool isDragging = false;

        public GridPlacement.PlacedItem ContainerItem => containerItem;
        public InventoryGridVisual GridVisual => gridVisual;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Initialize the floating window with a container item
        /// </summary>
        public void Initialize(GridPlacement.PlacedItem container, InventoryGridVisual grid, string displayName)
        {
            if (container == null || grid == null)
            {
                Debug.LogError("[FloatingContainerWindow] Cannot initialize with null container or grid!");
                Destroy(gameObject);
                return;
            }

            containerItem = container;
            gridVisual = grid;

            // Set title
            if (titleText != null)
            {
                titleText.text = displayName;
            }

            // Parent the grid to our container
            if (gridContainer != null)
            {
                grid.transform.SetParent(gridContainer, false);

                // DYNAMIC SIZING: Get grid's actual size and resize window to fit
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                if (gridRect != null && windowRect != null)
                {
                    // Grid keeps its size (determined by factory based on cell count)
                    // Window resizes to fit the grid plus padding for top bar

                    Vector2 gridSize = gridRect.sizeDelta;
                    float topBarHeight = topBar != null ? topBar.rect.height : 30f;
                    float padding = 10f; // Padding around grid

                    // Resize window to fit grid + top bar + padding
                    windowRect.sizeDelta = new Vector2(
                        gridSize.x + (padding * 2),
                        gridSize.y + topBarHeight + (padding * 2)
                    );

                    // CRITICAL: DO NOT change pivot! Grid MUST have pivot at (0,0) for coordinate system
                    // The factory already set up the correct pivot (bottom-left)

                    // Position grid: anchored to top-left so it aligns properly
                    // With bottom-left pivot, the grid's bottom-left corner will be at anchor point
                    gridRect.anchorMin = new Vector2(0f, 1f); // Top-left anchor
                    gridRect.anchorMax = new Vector2(0f, 1f);
                    // DON'T CHANGE PIVOT - keep factory's (0, 0) pivot!

                    // Offset: padding from left, and down by grid height
                    // This places the grid in the container with proper padding
                    gridRect.anchoredPosition = new Vector2(padding, -gridSize.y);

                    Log($"Window sized to fit grid: {gridSize.x}x{gridSize.y}px (maintaining bottom-left pivot for coordinates)");
                }
            }

            // CRITICAL: Ensure window has GraphicRaycaster for item clicks
            GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Debug.LogWarning($"[FloatingContainerWindow] No GraphicRaycaster found in parent hierarchy! Item clicks may not work. Make sure the Canvas has a GraphicRaycaster component.");
            }

            // Fade in
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(1f, 0.2f);
            }

            Log($"Initialized floating window for {displayName}");
        }

        /// <summary>
        /// Update the container item reference (called when item moves between grids)
        /// </summary>
        public void UpdateContainerItem(GridPlacement.PlacedItem newReference)
        {
            containerItem = newReference;
            // Also update the grid's owner reference for self-insertion prevention
            if (gridVisual != null)
            {
                gridVisual.SetOwnerContainerItem(newReference);
            }
        }

        /// <summary>
        /// Close this window
        /// </summary>
        public void Close()
        {
            // If an item is currently being dragged FROM this window's grid, cancel the drag
            // before destroying the grid. Otherwise the drag handler holds a dead reference
            // and the item gets stuck to the cursor.
            InventoryDragHandler dragHandler = gridVisual != null ? gridVisual.GetDragHandler() : null;
            if (dragHandler != null && dragHandler.IsDragging)
            {
                dragHandler.CancelDrag();
            }

            if (canvasGroup != null)
            {
                // Fade out then destroy
                canvasGroup.DOFade(0f, 0.15f).OnComplete(() =>
                {
                    // Notify manager
                    FloatingContainerWindowManager manager = FindObjectOfType<FloatingContainerWindowManager>();
                    if (manager != null)
                    {
                        manager.UnregisterWindow(this);
                    }

                    Destroy(gameObject);
                });
            }
            else
            {
                // Notify manager
                FloatingContainerWindowManager manager = FindObjectOfType<FloatingContainerWindowManager>();
                if (manager != null)
                {
                    manager.UnregisterWindow(this);
                }

                Destroy(gameObject);
            }

            Log($"Closed floating window");
        }

        #region Drag Handling

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Only drag from top bar
            if (!RectTransformUtility.RectangleContainsScreenPoint(topBar, eventData.position, eventData.pressEventCamera))
            {
                return;
            }

            isDragging = true;

            // Calculate offset from window position to mouse position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                windowRect.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localMousePos
            );

            dragOffset = windowRect.anchoredPosition - localMousePos;

            // Bring to front
            transform.SetAsLastSibling();

            Log("Started dragging window");
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            // Convert screen position to local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                windowRect.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localMousePos
            );

            // Apply offset
            windowRect.anchoredPosition = localMousePos + dragOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            Log("Stopped dragging window");
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up grid if it exists
            if (gridVisual != null && gridVisual.gameObject != null)
            {
                Destroy(gridVisual.gameObject);
            }
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[FloatingContainerWindow:{titleText?.text}] {message}");
            }
        }
    }
}
