using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using TimeGame.Systems.Inventory;

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

        [Header("Weight UI")]
        [Tooltip("Sprite shown as the weight icon in the weight row. Assigned at runtime by FloatingContainerWindowManager.")]
        [SerializeField] private Sprite weightIconSprite;

        [Header("Settings")]
        [SerializeField] private bool verboseLogging = true;

        // Staged sprites — set by the manager before Initialize() runs
        private Sprite stagedCloseButtonSprite;

        /// <summary>Called by FloatingContainerWindowManager before Initialize to pass through the weight icon sprite.</summary>
        public void SetWeightIconSprite(Sprite sprite) => weightIconSprite = sprite;

        /// <summary>
        /// Called by FloatingContainerWindowManager before Initialize to apply a custom close button icon.
        /// Replaces the Image.sprite on the close Button so all windows share the same icon from the config.
        /// </summary>
        public void SetCloseButtonSprite(Sprite sprite) => stagedCloseButtonSprite = sprite;

        /// <summary>
        // The container item this window displays
        private GridPlacement.PlacedItem containerItem;

        // The spawned grid visual
        private InventoryGridVisual gridVisual;

        // Drag state
        private Vector2 dragOffset;
        private bool isDragging = false;

        // ── Parent container context for real-time networking sync ───────────
        // Stores which world container owns this floating window and the chain of
        // container InstanceIDs that must be walked to reach THIS window's inventory.
        // ContainerPath contains ancestors only (NOT this window's own container ID).
        // The networking layer appends ContainerItem.InstanceID when building RPCs.
        private int _parentWorldContainerNetId = -1;
        private int _parentCompartmentIndex    = -1;
        private System.Guid[] _containerPath;

        public GridPlacement.PlacedItem ContainerItem => containerItem;
        public InventoryGridVisual GridVisual => gridVisual;

        // ── Parent container context for real-time networking sync ───────────

        [System.Serializable]
        public struct ParentContainerContext
        {
            public int           WorldContainerNetId; // NetworkObject.ObjectId of the root world container
            public int           CompartmentIndex;    // Which compartment in that container
            public System.Guid[] ContainerPath;       // Ancestor IDs from root to this container's parent

            public bool IsValid => WorldContainerNetId >= 0;
        }

        /// <summary>
        /// Set the full parent context including the container path for real-time nested sync.
        /// Called by FloatingContainerWindowManager when opening a nested container.
        /// </summary>
        public void SetParentContext(int containerNetId, int compartmentIndex, System.Guid[] containerPath)
        {
            _parentWorldContainerNetId = containerNetId;
            _parentCompartmentIndex    = compartmentIndex;
            _containerPath             = containerPath ?? System.Array.Empty<System.Guid>();
        }

        /// <summary>
        /// Get the parent world container context for networking.
        /// ContainerPath contains ancestor IDs only — callers must append
        /// ContainerItem.InstanceID to get the full path for server RPCs.
        /// </summary>
        public ParentContainerContext GetParentContext() => new ParentContainerContext
        {
            WorldContainerNetId = _parentWorldContainerNetId,
            CompartmentIndex    = _parentCompartmentIndex,
            ContainerPath       = _containerPath ?? System.Array.Empty<System.Guid>()
        };

        /// <summary>
        /// Refresh the grid visual to display updated container contents.
        /// Called by networking RPCs when the server broadcasts changes from other players.
        /// </summary>
        public void RefreshGrid()
        {
            if (gridVisual != null && containerItem?.ContainerInventory != null)
            {
                gridVisual.SwapInventorySystem(containerItem.ContainerInventory);
                gridVisual.RefreshAllItemVisuals();
                Log($"Refreshed grid for '{containerItem.ItemDefinition?.ItemName}'.");
            }
        }

        /// <summary>
        /// Rebuilds this window's grid in-place from a <see cref="ContainerItemData"/> snapshot
        /// pushed by the server after any mutation.
        ///
        /// Works directly on <c>gridVisual.InventorySystem</c> (which is the same object as
        /// <c>containerItem.ContainerInventory</c> after the window was opened) so the
        /// object reference is never replaced — the item-lineage law is honoured at the UI layer.
        /// </summary>
        public void RebuildFromData(ContainerItemData data)
        {
            InventorySystem inv = gridVisual?.InventorySystem;
            if (inv == null || data == null) return;

            // Clear in-place — same object, preserved reference.
            foreach (var p in inv.GetAllItems().ToList())
                inv.RemoveItem(p.InstanceID);

            data.LoadIntoInventorySystem(inv, compartmentIndex: 0);

            // SwapInventorySystem is a no-op here (same object) but ensures the visual
            // re-wires internal event subscriptions if the implementation requires it.
            gridVisual.SwapInventorySystem(inv);
            gridVisual.RefreshAllItemVisuals();

            Log($"RebuildFromData for '{containerItem?.ItemDefinition?.ItemName}'.");
        }

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

            // Apply custom close button sprite if one was staged by the manager.
            // We swap the Image.sprite on the Button's target graphic so the prefab's
            // layout, size, and ColorBlock are all preserved — only the icon changes.
            if (stagedCloseButtonSprite != null && closeButton != null)
            {
                Image closeBtnImage = closeButton.targetGraphic as Image;
                if (closeBtnImage != null)
                {
                    closeBtnImage.sprite        = stagedCloseButtonSprite;
                    closeBtnImage.preserveAspect = true;
                }
            }

            // Parent the grid to our container
            if (gridContainer != null)
            {
                grid.transform.SetParent(gridContainer, false);

                // DYNAMIC SIZING: Get grid's actual size and resize window to fit
                RectTransform gridRect = grid.GetComponent<RectTransform>();
                if (gridRect != null && windowRect != null)
                {
                    Vector2 gridSize   = gridRect.sizeDelta;
                    float topBarHeight = topBar != null ? topBar.rect.height : 30f;
                    float padding      = 10f;

                    // Reserve space for weight row when a limit exists
                    bool hasWeightLimit = grid.InventorySystem != null && grid.InventorySystem.UseWeightLimit;
                    float weightRowHeight = hasWeightLimit ? 24f : 0f;

                    // Resize window to fit grid + top bar + optional weight row + padding
                    windowRect.sizeDelta = new Vector2(
                        gridSize.x + (padding * 2),
                        gridSize.y + topBarHeight + weightRowHeight + (padding * 2)
                    );

                    // Position grid: anchored to top-left, pivot (0,0) at bottom-left
                    gridRect.anchorMin = new Vector2(0f, 1f);
                    gridRect.anchorMax = new Vector2(0f, 1f);
                    gridRect.anchoredPosition = new Vector2(padding, -gridSize.y);

                    // Add weight row below the grid (inside gridContainer)
                    if (hasWeightLimit)
                    {
                        AddWeightRow(gridContainer, grid.InventorySystem, gridSize, padding);
                    }

                    Log($"Window sized to fit grid: {gridSize.x}x{gridSize.y}px" +
                        (hasWeightLimit ? " + weight row" : ""));
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
        /// Build a small weight row below the grid inside gridContainer.
        /// Positioned just below the grid's bottom edge using manual anchoring.
        /// </summary>
        private void AddWeightRow(RectTransform container, InventorySystem system, Vector2 gridSize, float padding)
        {
            // Row sits just below the grid (grid bottom-left is at anchoredPos (padding, -gridSize.y)
            // so the row bottom is at y = -(gridSize.y + rowHeight) relative to container top)
            const float rowHeight = 22f;

            GameObject rowObj = new GameObject("WeightRow");
            rowObj.transform.SetParent(container, false);

            RectTransform rowRT = rowObj.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(0f, 1f);
            rowRT.pivot     = new Vector2(0f, 1f);
            rowRT.sizeDelta = new Vector2(gridSize.x, rowHeight);
            // Place row just below the grid
            rowRT.anchoredPosition = new Vector2(padding, -(gridSize.y + 4f));

            HorizontalLayoutGroup hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment       = TextAnchor.MiddleLeft;
            hLayout.spacing              = 5f;
            hLayout.childControlWidth    = false;
            hLayout.childControlHeight   = false;
            hLayout.childForceExpandWidth  = false;
            hLayout.childForceExpandHeight = false;

            // Icon (small square)
            GameObject iconObj = new GameObject("WeightIcon");
            iconObj.transform.SetParent(rowObj.transform, false);
            RectTransform iconRT  = iconObj.AddComponent<RectTransform>();
            iconRT.sizeDelta      = new Vector2(14f, 14f);
            Image iconImg         = iconObj.AddComponent<Image>();
            if (weightIconSprite != null)
                iconImg.sprite = weightIconSprite;
            iconImg.color         = Color.white;
            iconImg.raycastTarget = false;

            // Text
            GameObject textObj = new GameObject("WeightText");
            textObj.transform.SetParent(rowObj.transform, false);
            RectTransform textRT  = textObj.AddComponent<RectTransform>();
            textRT.sizeDelta      = new Vector2(gridSize.x - 24f, rowHeight);
            TextMeshProUGUI tmp   = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize          = 13;
            tmp.alignment         = TextAlignmentOptions.MidlineLeft;
            tmp.color             = Color.white;
            tmp.raycastTarget     = false;

            // Wire up live component
            InventoryWeightBar bar = rowObj.AddComponent<InventoryWeightBar>();
            bar.SetReferences(iconImg, tmp);
            bar.Initialize(system);
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
        /// Close this window.
        /// Any in-progress drag from this window's grid is cancelled so the item returns
        /// to the grid rather than getting stuck to the cursor.
        /// </summary>
        public void Close()
        {
            // Cancel any active drag so the item snaps back instead of floating on the
            // cursor after the window disappears.
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
                    FloatingContainerWindowManager manager = FloatingContainerWindowManager.Instance;
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
                FloatingContainerWindowManager manager = FloatingContainerWindowManager.Instance;
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

        /// <summary>
        /// Return a new Guid[] equal to <paramref name="existing"/> with <paramref name="id"/> appended.
        /// Used when building the full container path for server sync RPCs.
        /// </summary>
        private static System.Guid[] AppendGuid(System.Guid[] existing, System.Guid id)
        {
            if (existing == null || existing.Length == 0)
                return new System.Guid[] { id };

            System.Guid[] result = new System.Guid[existing.Length + 1];
            System.Array.Copy(existing, result, existing.Length);
            result[existing.Length] = id;
            return result;
        }
    }
}
