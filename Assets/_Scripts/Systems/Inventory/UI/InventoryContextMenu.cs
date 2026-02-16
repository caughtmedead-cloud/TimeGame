using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Context menu for right-clicking inventory items.
    /// Displays available actions (Use, Drop, Inspect, etc.)
    /// </summary>
    public class InventoryContextMenu : MonoBehaviour
    {
        [Header("Visual Settings")]
        [Tooltip("Optional sprite for menu background (9-slice recommended)")]
        [SerializeField] private Sprite backgroundSprite;

        [Tooltip("Optional sprite for button backgrounds (9-slice recommended)")]
        [SerializeField] private Sprite buttonSprite;

        [Tooltip("Optional sprite for selection indicator (overlays selected button in world mode)")]
        [SerializeField] private Sprite selectionIndicatorSprite;

        [Tooltip("Optional font for button text (uses TextMeshPro default if not set)")]
        [SerializeField] private TMP_FontAsset buttonFont;

        [SerializeField] private Vector2 menuSize = new Vector2(150, 200);
        [SerializeField] private float selectionLerpSpeed = 12f;
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("World Menu Settings")]
        [Tooltip("Offset for world item menu (no cursor mode). X = right, Y = up")]
        [SerializeField] private Vector2 worldMenuOffset = Vector2.zero;


        private static InventoryContextMenu instance;
        private Canvas canvas;
        private PlacedItem currentItem;
        private InventoryGridVisual currentGrid;

        // Dynamically created UI elements
        private GameObject menuPanel;
        private Transform buttonContainer;
        private GameObject buttonPrefab;
        private GameObject selectionIndicator; // Visual indicator for selected button (world mode only)
        private RectTransform selectionIndicatorRT;

        // Scroll wheel navigation (for world item mode only)
        private List<Button> currentButtons = new List<Button>();
        private int selectedButtonIndex = 0;
        private Vector2 targetIndicatorPosition;

        // Context mode
        private bool isWorldItemMode = false; // True = world item (no cursor), False = inventory item (cursor enabled)

        // Menu actions
        public event Action<PlacedItem, InventoryGridVisual> OnUseItem;
        public event Action<PlacedItem, InventoryGridVisual> OnInspectItem;
        public event Action<PlacedItem, InventoryGridVisual> OnDropItem;
        public event Action<PlacedItem, InventoryGridVisual> OnDropOneItem;

        private void Awake()
        {
            instance = this;

            // Get the parent canvas (should be ContextMenuCanvas)
            canvas = GetComponentInParent<Canvas>();

            if (menuPanel == null)
            {
                CreateDefaultMenuUI();
            }

            Hide();
        }

        private void Start()
        {
            // Create selection indicator after UI is fully initialized
            if (selectionIndicatorSprite != null && buttonContainer != null)
            {
                CreateSelectionIndicator();
            }
        }

        /// <summary>
        /// Show context menu for inventory item (supports mouse cursor)
        /// </summary>
        public static void ShowMenuInInventory(Vector2 screenPosition, PlacedItem item, InventoryGridVisual grid)
        {
            if (instance == null) return;
            instance.ShowMenuInInventoryInternal(screenPosition, item, grid);
        }

        /// <summary>
        /// Show context menu for world item (no cursor, input-only navigation)
        /// </summary>
        public static void ShowMenuInWorld(Vector2 screenPosition, List<ContextMenuOption> options)
        {
            if (instance == null) return;
            instance.ShowMenuInWorldInternal(screenPosition, options);
        }

        public static void HideMenu()
        {
            if (instance == null) return;
            instance.Hide();
        }

        private void ShowMenuInInventoryInternal(Vector2 screenPosition, PlacedItem item, InventoryGridVisual grid)
        {
            isWorldItemMode = false;
            currentItem = item;
            currentGrid = grid;

            // Clear existing buttons
            ClearButtons();

            // Build menu options based on item type
            InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
            if (itemDef != null)
            {
                // Use button - only for consumables with uses
                if (item.IsInstanceTracked &&
                    item.ItemInstances != null &&
                    item.ItemInstances.Count > 0 &&
                    item.ItemInstances[0].UsesRemaining >= 0)
                {
                    AddMenuButton("Use", OnUseButtonClicked);
                }

                // Inspect button - always available
                AddMenuButton("Inspect", OnInspectButtonClicked);

                // Drop buttons - dynamic based on stack count
                if (item.StackCount > 1)
                {
                    // Stacked items: show both Drop One and Drop All
                    AddMenuButton("Drop One", OnDropOneButtonClicked);
                    AddMenuButton("Drop All", OnDropButtonClicked);
                }
                else
                {
                    // Single item: just show Drop
                    AddMenuButton("Drop", OnDropButtonClicked);
                }
            }

            // Position menu at mouse
            PositionMenu(screenPosition);

            // Show (no selection indicator in inventory mode)
            selectedButtonIndex = 0;
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
            }

            menuPanel.SetActive(true);
        }

        private void ShowMenuInWorldInternal(Vector2 screenPosition, List<ContextMenuOption> options)
        {
            isWorldItemMode = true;
            currentItem = null;
            currentGrid = null;

            // Clear existing buttons
            ClearButtons();

            // Add custom options
            foreach (var option in options)
            {
                if (option.IsEnabled)
                {
                    AddMenuButton(option.Label, option.Callback);
                }
            }

            // Position menu at screen center (no cursor in FPS mode)
            PositionMenuAtCenter();

            // Show with selection indicator
            selectedButtonIndex = 0;
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(true);
                // Force indicator to render on top of newly created buttons
                selectionIndicator.transform.SetAsLastSibling();
            }

            menuPanel.SetActive(true);

            // Update indicator position after layout completes
            StartCoroutine(UpdateIndicatorAfterLayout());
        }

        private void ClearButtons()
        {
            foreach (Transform child in buttonContainer)
            {
                // Don't destroy the selection indicator
                if (child.gameObject != selectionIndicator)
                {
                    Destroy(child.gameObject);
                }
            }
            currentButtons.Clear();
            selectedButtonIndex = 0;
        }

        private void Hide()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }
            currentItem = null;
            currentGrid = null;
        }

        private void AddMenuButton(string label, Action callback)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);
            buttonObj.name = $"Button_{label}"; // Name for debugging

            // Manual layout: position buttons from top with padding
            RectTransform buttonRT = buttonObj.GetComponent<RectTransform>();
            int buttonIndex = currentButtons.Count;
            float yPos = -5 - (buttonIndex * 32); // 5px top padding + (index * (30px height + 2px spacing))

            buttonRT.anchorMin = new Vector2(0, 1); // Top-left
            buttonRT.anchorMax = new Vector2(1, 1); // Top-right (stretch width)
            buttonRT.pivot = new Vector2(0.5f, 1); // Top-center
            buttonRT.sizeDelta = new Vector2(-10, 30); // -10 for 5px padding each side, 30 height
            buttonRT.anchoredPosition = new Vector2(0, yPos);

            TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
            }

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[ContextMenu] Button clicked: {label}, Mode: {(isWorldItemMode ? "World" : "Inventory")}");
                    callback?.Invoke();
                    Hide();
                });
                currentButtons.Add(button);
            }
        }

        private void UpdateSelectionIndicator(bool snapImmediately = false)
        {
            // Only update in world item mode
            if (!isWorldItemMode || selectionIndicator == null || currentButtons.Count == 0) return;

            // Get target button position
            if (selectedButtonIndex >= 0 && selectedButtonIndex < currentButtons.Count)
            {
                RectTransform buttonRT = currentButtons[selectedButtonIndex].GetComponent<RectTransform>();

                // Buttons positioned manually, indicator matches exactly
                targetIndicatorPosition = buttonRT.anchoredPosition;

                // Snap immediately if requested (for initial positioning)
                if (snapImmediately && selectionIndicatorRT != null)
                {
                    selectionIndicatorRT.anchoredPosition = targetIndicatorPosition;
                }
            }
        }

        private void PositionMenu(Vector2 screenPosition)
        {
            RectTransform menuRT = menuPanel.GetComponent<RectTransform>();

            // For Screen Space Overlay, screen position IS the position we want
            // Just need to convert from screen space to canvas local space
            menuRT.position = screenPosition;

            // TODO: Clamp to canvas bounds (implementation similar to Tooltip pattern)
        }

        private void PositionMenuAtCenter()
        {
            RectTransform menuRT = menuPanel.GetComponent<RectTransform>();
            // Center the menu on screen (for world item mode with no cursor)
            // Uses worldMenuOffset which can be adjusted in the inspector
            menuRT.anchoredPosition = worldMenuOffset;
        }

        private void OnUseButtonClicked()
        {
            Debug.Log("[InventoryContextMenu] Use button clicked!");
            Debug.Log($"[InventoryContextMenu] currentItem: {currentItem}, currentGrid: {currentGrid}");
            Debug.Log($"[InventoryContextMenu] OnUseItem subscribers: {OnUseItem?.GetInvocationList().Length ?? 0}");
            OnUseItem?.Invoke(currentItem, currentGrid);
        }

        private void OnInspectButtonClicked()
        {
            OnInspectItem?.Invoke(currentItem, currentGrid);
        }

        private void OnDropButtonClicked()
        {
            OnDropItem?.Invoke(currentItem, currentGrid);
        }

        private void OnDropOneButtonClicked()
        {
            OnDropOneItem?.Invoke(currentItem, currentGrid);
        }

        private void CreateDefaultMenuUI()
        {
            // Create menu panel GameObject
            GameObject panelObj = new GameObject("ContextMenuPanel");
            panelObj.transform.SetParent(transform, false);

            RectTransform panelRT = panelObj.AddComponent<RectTransform>();
            panelRT.sizeDelta = menuSize;
            panelRT.pivot = new Vector2(0, 1); // Top-left pivot

            Image panelBg = panelObj.AddComponent<Image>();

            // Use sprite if provided, otherwise use solid color fallback
            if (backgroundSprite != null)
            {
                panelBg.sprite = backgroundSprite;
                panelBg.type = Image.Type.Sliced; // Use sliced for 9-slice support
                panelBg.color = Color.white; // Keep white to show sprite as-is
            }
            else
            {
                panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // Fallback color
            }

            // Create button container
            GameObject containerObj = new GameObject("ButtonContainer");
            containerObj.transform.SetParent(panelObj.transform, false);

            RectTransform containerRT = containerObj.AddComponent<RectTransform>();
            containerRT.anchorMin = Vector2.zero;
            containerRT.anchorMax = Vector2.one;
            containerRT.sizeDelta = Vector2.zero;
            containerRT.anchoredPosition = Vector2.zero;

            // No VerticalLayoutGroup - we'll position buttons manually for full control

            menuPanel = panelObj;
            buttonContainer = containerObj.transform;

            // Create default button prefab
            CreateDefaultButtonPrefab();
        }

        private void CreateSelectionIndicator()
        {
            if (selectionIndicatorSprite == null || buttonContainer == null) return;

            // Create selection indicator as child of BUTTON CONTAINER (same parent as buttons)
            // This ensures it renders with buttons, then we use LayoutElement to ignore layout
            selectionIndicator = new GameObject("SelectionIndicator");
            selectionIndicator.transform.SetParent(buttonContainer, false);

            selectionIndicatorRT = selectionIndicator.AddComponent<RectTransform>();
            // Match button setup exactly
            selectionIndicatorRT.anchorMin = new Vector2(0, 1); // Top-left
            selectionIndicatorRT.anchorMax = new Vector2(1, 1); // Top-right (stretch width)
            selectionIndicatorRT.pivot = new Vector2(0.5f, 1); // Top-center
            selectionIndicatorRT.sizeDelta = new Vector2(-10, 30); // -10 for padding, 30 height
            selectionIndicatorRT.anchoredPosition = Vector2.zero; // Will be updated

            Image indicatorImage = selectionIndicator.AddComponent<Image>();
            indicatorImage.sprite = selectionIndicatorSprite;
            indicatorImage.type = Image.Type.Sliced;
            indicatorImage.color = Color.white;
            indicatorImage.raycastTarget = false; // Don't block button clicks

            // Set as last sibling so it renders on top of buttons
            selectionIndicator.transform.SetAsLastSibling();
            selectionIndicator.SetActive(false); // Hidden by default
        }

        private void CreateDefaultButtonPrefab()
        {
            GameObject btnObj = new GameObject("ContextMenuButton");

            RectTransform btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(0, 30);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.raycastTarget = true; // Ensure button can receive clicks

            // Use sprite if provided, otherwise use solid color
            if (buttonSprite != null)
            {
                btnImage.sprite = buttonSprite;
                btnImage.type = Image.Type.Sliced; // Use sliced for 9-slice support
                btnImage.color = Color.white; // Keep white to show sprite as-is
            }
            else
            {
                btnImage.color = buttonColor;
            }

            Button button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.interactable = true; // Ensure button is interactable

            // Button colors
            ColorBlock colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHoverColor;
            colors.pressedColor = buttonHoverColor * 0.8f;
            button.colors = colors;

            // Text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            textRT.anchoredPosition = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14;
            text.color = Color.white;

            // Use custom font if provided
            if (buttonFont != null)
            {
                text.font = buttonFont;
            }

            buttonPrefab = btnObj;
        }

        private void Update()
        {
            if (menuPanel == null || !menuPanel.activeSelf) return;

            if (isWorldItemMode)
            {
                // World item mode: input-driven navigation (no cursor)
                // Scroll wheel navigation
                float scroll = Input.mouseScrollDelta.y;
                if (scroll != 0 && currentButtons.Count > 0)
                {
                    selectedButtonIndex -= (int)Mathf.Sign(scroll);
                    selectedButtonIndex = (selectedButtonIndex + currentButtons.Count) % currentButtons.Count;
                    UpdateSelectionIndicator();
                }

                // Lerp selection indicator to target position
                if (selectionIndicator != null && selectionIndicatorRT != null)
                {
                    selectionIndicatorRT.anchoredPosition = Vector2.Lerp(
                        selectionIndicatorRT.anchoredPosition,
                        targetIndicatorPosition,
                        Time.deltaTime * selectionLerpSpeed
                    );
                }

                // Left click to execute selected button
                if (Input.GetMouseButtonDown(0))
                {
                    if (selectedButtonIndex >= 0 && selectedButtonIndex < currentButtons.Count)
                    {
                        currentButtons[selectedButtonIndex].onClick.Invoke();
                    }
                }

                // Right click to close
                if (Input.GetMouseButtonDown(1))
                {
                    Hide();
                }
            }
            else
            {
                // Inventory mode: close menu if clicking outside
                if (Input.GetMouseButtonDown(0))
                {
                    if (!IsPointerOverMenu())
                    {
                        Hide();
                    }
                }

                // Right click also closes
                if (Input.GetMouseButtonDown(1))
                {
                    Hide();
                }
            }
        }

        private bool IsPointerOverMenu()
        {
            if (menuPanel == null) return false;

            // Check if mouse is over the menu using RectTransform bounds
            RectTransform menuRT = menuPanel.GetComponent<RectTransform>();
            return RectTransformUtility.RectangleContainsScreenPoint(menuRT, Input.mousePosition, canvas.worldCamera);
        }

        private System.Collections.IEnumerator UpdateIndicatorAfterLayout()
        {
            // Wait for layout to complete
            yield return null;
            UpdateSelectionIndicator(true); // Snap immediately after layout
        }
    }
}
