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
        [Header("UI Settings")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject buttonPrefab;

        [Header("Visual Settings")]
        [SerializeField] private Vector2 menuSize = new Vector2(150, 200);
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color buttonHoverColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private static InventoryContextMenu instance;
        private Canvas canvas;
        private PlacedItem currentItem;
        private InventoryGridVisual currentGrid;

        // Menu actions
        public event Action<PlacedItem, InventoryGridVisual> OnUseItem;
        public event Action<PlacedItem, InventoryGridVisual> OnInspectItem;
        public event Action<PlacedItem, InventoryGridVisual> OnDropItem;

        private void Awake()
        {
            instance = this;
            canvas = GetComponentInParent<Canvas>();

            if (menuPanel == null)
            {
                CreateDefaultMenuUI();
            }

            Hide();
        }

        public static void ShowMenu(Vector2 screenPosition, PlacedItem item, InventoryGridVisual grid)
        {
            if (instance == null) return;
            instance.ShowMenuInternal(screenPosition, item, grid);
        }

        public static void HideMenu()
        {
            if (instance == null) return;
            instance.Hide();
        }

        private void ShowMenuInternal(Vector2 screenPosition, PlacedItem item, InventoryGridVisual grid)
        {
            currentItem = item;
            currentGrid = grid;

            // Clear existing buttons
            foreach (Transform child in buttonContainer)
            {
                Destroy(child.gameObject);
            }

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

                // Drop button - always available
                AddMenuButton("Drop", OnDropButtonClicked);
            }

            // Position menu at mouse
            PositionMenu(screenPosition);

            // Show
            menuPanel.SetActive(true);
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
            Debug.Log($"[InventoryContextMenu] Creating button: {label}");
            GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);
            Debug.Log($"[InventoryContextMenu] Button object created: {buttonObj.name}");

            TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                Debug.Log($"[InventoryContextMenu] Set button text to: {label}");
            }
            else
            {
                Debug.LogWarning($"[InventoryContextMenu] No TextMeshProUGUI found in button!");
            }

            Button button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                Debug.Log($"[InventoryContextMenu] Found Button component, adding listener");
                button.onClick.AddListener(() =>
                {
                    Debug.Log($"[InventoryContextMenu] Button '{label}' clicked!");
                    callback?.Invoke();
                    Hide();
                });
            }
            else
            {
                Debug.LogWarning($"[InventoryContextMenu] No Button component found on button object!");
            }
        }

        private void PositionMenu(Vector2 screenPosition)
        {
            RectTransform menuRT = menuPanel.GetComponent<RectTransform>();

            // Convert screen position to canvas position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                screenPosition,
                null,
                out Vector2 localPos
            );

            menuRT.anchoredPosition = localPos;

            // TODO: Clamp to canvas bounds (implementation similar to Tooltip pattern)
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

        private void CreateDefaultMenuUI()
        {
            // Create menu panel GameObject
            GameObject panelObj = new GameObject("ContextMenuPanel");
            panelObj.transform.SetParent(transform, false);

            RectTransform panelRT = panelObj.AddComponent<RectTransform>();
            panelRT.sizeDelta = menuSize;
            panelRT.pivot = new Vector2(0, 1); // Top-left pivot

            Image panelBg = panelObj.AddComponent<Image>();
            panelBg.color = backgroundColor;

            // Create button container
            GameObject containerObj = new GameObject("ButtonContainer");
            containerObj.transform.SetParent(panelObj.transform, false);

            RectTransform containerRT = containerObj.AddComponent<RectTransform>();
            containerRT.anchorMin = Vector2.zero;
            containerRT.anchorMax = Vector2.one;
            containerRT.sizeDelta = Vector2.zero;
            containerRT.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 2;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            menuPanel = panelObj;
            buttonContainer = containerObj.transform;

            // Create default button prefab
            CreateDefaultButtonPrefab();
        }

        private void CreateDefaultButtonPrefab()
        {
            GameObject btnObj = new GameObject("ContextMenuButton");

            RectTransform btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(0, 30);

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = buttonColor;

            Button button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnImage;

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

            buttonPrefab = btnObj;
        }

        // Close menu when clicking outside
        // Disabled for now - buttons handle hiding themselves via onClick callbacks
        // This prevents race condition where Update() hides menu before button onClick fires
        /*
        private void Update()
        {
            if (menuPanel != null && menuPanel.activeSelf)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    Hide();
                }
            }
        }
        */
    }
}
