using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Controllers
{
    public enum WindowType
    {
        Inspect,
        Container,
    }

    [DefaultExecutionOrder(10)]
    public abstract class WindowController : MonoBehaviour
    {
        [Header("Inventory Settings Anchor Settings")] [SerializeField]
        protected InventorySettingsAnchorSo inventorySettingsAnchorSo;

        [SerializeField] private RectTransformPrefabAnchorSo parentRectTransform;

        private int _currentIndex;
        private readonly List<Window> _windows = new();

        private void Awake()
        {
            StaticInventoryContext.AddWindowManagerReference(this);
        }

        private void Start()
        {
            var windowTypes = Enum.GetValues(typeof(WindowType))
                .Cast<WindowType>();

            foreach (var windowType in windowTypes)
            {
                var maxWindowOpen = GetMaxWindowOpen(windowType);
                var prefabWindow = GetPrefabWindow(windowType);

                for (var i = 0; i < maxWindowOpen; i++)
                {
                    var windowInstantiated = Instantiate(prefabWindow, parentRectTransform.Value);

                    _windows.Add(windowInstantiated.GetComponent<Window>());

                    windowInstantiated.gameObject.SetActive(false);
                }
            }
        }

        protected virtual Window GetPrefabWindow(WindowType windowType)
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.BasicPrefabWindow;
        }

        protected virtual int GetMaxWindowOpen(WindowType windowType)
        {
            return 4;
        }

        private List<Window> GetAllWindows()
        {
            return _windows;
        }

        private Window GetNextWindowInThePool(WindowType windowType)
        {
            var specificWindows = _windows.Where(window => window.GetType() == GetPrefabWindow(windowType).GetType())
                .ToList();

            var indexToNewWindow = GetIndexToNewWindow(specificWindows, windowType);

            return specificWindows[indexToNewWindow];
        }

        private int GetIndexToNewWindow(List<Window> specificWindows, WindowType windowType)
        {
            int currentIndex;

            for (var i = 0; i < specificWindows.Count; i++)
            {
                var window = specificWindows[i];

                if (!window.gameObject.activeSelf) return i;
            }

            if (_currentIndex < GetMaxWindowOpen(windowType) - 1)
            {
                currentIndex = _currentIndex;

                _currentIndex++;

                return currentIndex;
            }

            currentIndex = _currentIndex;
            _currentIndex = 0;

            return currentIndex;
        }

        private static void SetWindowInMiddle(Window window)
        {
            if (window == null) return;

            var rectTransform = window.GetComponent<RectTransform>();

            var rectTransformAnchoredPosition = new Vector2(0.5f, 0.5f);

            rectTransform.anchoredPosition = rectTransformAnchoredPosition;
            rectTransform.anchorMax = rectTransformAnchoredPosition;
            rectTransform.anchorMin = rectTransformAnchoredPosition;
            rectTransform.pivot = rectTransformAnchoredPosition;

            rectTransform.SetAsLastSibling();
        }

        public virtual void OpenWindow(ItemTable itemTable, WindowType windowType)
        {
            var nextWindowInThePool = GetNextWindowInThePool(windowType);

            var allWindows = GetAllWindows();

            if (allWindows == null) return;

            if (allWindows.Any(window => window.gameObject.activeSelf &&
                                         window.IsWindowOpenedWith(itemTable)))
            {
                return;
            }

            nextWindowInThePool.CloseWindow(); // Close previous inventory
            nextWindowInThePool.Set(itemTable);
            SetWindowInMiddle(nextWindowInThePool);
            nextWindowInThePool.gameObject.SetActive(true);

            RefreshUI(nextWindowInThePool.RectTransform);
        }

        public virtual void CloseWindow(ItemTable itemTable)
        {
            if (itemTable == null) return;

            var allWindows = GetAllWindows();

            foreach (var window in allWindows.Where(window => window != null)
                         .Where(window => window.IsWindowOpenedWith(itemTable)))
            {
                window.CloseWindow();
                RefreshUI(window.RectTransform);
            }
        }

        public void CloseAll()
        {
            var allWindows = GetAllWindows();

            foreach (var window in allWindows)
            {
                window.CloseWindow();
            }
        }

        private void RefreshUI(RectTransform transformRef)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transformRef);
        }

        public void SetInventorySettingsAnchorSo(InventorySettingsAnchorSo inventorySettingsAnchor)
        {
            inventorySettingsAnchorSo = inventorySettingsAnchor;
        }

        public void SetWindowParent(RectTransformPrefabAnchorSo windowParent)
        {
            parentRectTransform = windowParent;
        }
    }
}