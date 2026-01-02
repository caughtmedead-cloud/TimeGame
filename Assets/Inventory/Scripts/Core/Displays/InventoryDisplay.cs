using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Displays.Filler;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Displays
{
    [DefaultExecutionOrder(-1)]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryDisplay : MonoBehaviour
    {
        [Header("Display Configuration")] [SerializeField]
        private InventoryDisplayAnchorSo displayAnchorSo;

        [SerializeField]
        [Tooltip(
            "The prefab display filler which will be displayed inside the 'displayAnchorSo'. " +
            "If null, will use the one from InventorySettingsSo. " +
            "The priority is to use this property instead of the InventorySettingsSo")]
        private DisplayFiller customPrefabDisplayFiller;

        [Header("Display Sort (If multiple)")]
        [SerializeField]
        [Tooltip("This will be used to Sort the Containers in Player Inventory. Ps: This will be reverse in runtime.")]
        private ItemDataTypeSo[] itemDataTypeSos;

        private readonly List<DisplayFiller> _displayFillers = new();
        private RectTransform _containerParent;
        private bool _initialRefreshUi;

        private void Awake()
        {
            SetAnchor();

            _containerParent = GetComponent<RectTransform>();

            Array.Reverse(itemDataTypeSos);
        }

        private void SetAnchor()
        {
            if (displayAnchorSo == null)
            {
                Debug.LogError(
                    "[Display] ContainerDisplay not configured correctly... Missing the anchor to the Holder."
                        .Configuration());
                return;
            }

            displayAnchorSo.Value = this;
        }

        public void DisplayInteract(ItemTable itemTable, DisplayInteraction displayInteraction)
        {
            var displayData = FindDisplayDataByItem(itemTable);

            DisplayInteract(displayData, displayInteraction);
        }

        public void DisplayInteract(DisplayData displayData, DisplayInteraction displayInteraction)
        {
            if (displayInteraction == DisplayInteraction.Close)
            {
                CloseDisplay(displayData);
                displayData?.SetState(false);
                ResortContainers();
                return;
            }

            if (displayInteraction != DisplayInteraction.Open) return;

            OpenDisplay(displayData);
            displayData?.SetState(true);
            ResortContainers();
            RefreshUI();
        }

        private void OpenDisplay(DisplayData container)
        {
            if (IsContainerAlreadyOpened(container))
            {
                Debug.Log("The container is already opened... Not opening again.".Info());
                return;
            }

            var displayFiller = GetFreeDisplayFiller(container);

            if (displayFiller == null) return;

            displayFiller.Open(container);
        }

        private void CloseDisplay(DisplayData displayData)
        {
            var displayFiller = FindDisplayFillerBy(displayData, displayData?.InventorySo != null);

            if (displayFiller == null) return;

            displayFiller.Close(displayData);
        }

        public void CloseAll()
        {
            foreach (var displayedContainer in GetDisplayedContainers())
            {
                DisplayInteract(displayedContainer, DisplayInteraction.Close);
            }
        }

        private void RefreshUI()
        {
            if (_initialRefreshUi) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_containerParent);
            _initialRefreshUi = true;
        }

        private bool IsContainerAlreadyOpened(DisplayData displayData)
        {
            return FindDisplayFillerBy(displayData, displayData.InventorySo != null) != null;
        }

        private DisplayFiller GetFreeDisplayFiller(DisplayData displayData)
        {
            if (displayData == null) return null;

            var foundDisplayFiller = FindDisplayFillerBy(null, displayData.InventorySo != null);

            if (foundDisplayFiller != null)
            {
                foundDisplayFiller.Set(displayData);

                return foundDisplayFiller;
            }

            var newDisplayFiller = AddNewDisplayFiller(displayData.InventorySo != null);

            newDisplayFiller.Set(displayData);

            return newDisplayFiller;
        }

        private DisplayFiller FindDisplayFillerBy(DisplayData displayData, bool isCharacterInventory)
        {
            if (isCharacterInventory)
            {
                return _displayFillers?.FirstOrDefault(filler =>
                    filler != null && filler is CharacterDisplayFiller
                                   && (filler.DisplayData == displayData ||
                                       IsDisplayContainInventorySo(displayData?.InventorySo, filler.DisplayData))
                );
            }

            return _displayFillers?.FirstOrDefault(filler =>
                filler != null && filler is not CharacterDisplayFiller
                               && (filler.DisplayData == displayData ||
                                   IsDisplayContainItemContainer(displayData?.ItemContainer, filler.DisplayData))
            );
        }

        private DisplayFiller AddNewDisplayFiller(bool isCharacterInventory)
        {
            var displayFillerPrefab = GetDisplayFillerPrefab(isCharacterInventory);

            var newDisplayFiller = Instantiate(displayFillerPrefab, _containerParent);

            newDisplayFiller.gameObject.SetActive(false);

            _displayFillers.Add(newDisplayFiller);

            return newDisplayFiller;
        }

        private List<DisplayData> GetDisplayedContainers()
        {
            return _displayFillers.Where(filler => filler.DisplayData != null)
                .Select(filler => filler.DisplayData)
                .ToList();
        }

        private void ResortContainers()
        {
            var displayedContainers = GetDisplayedContainers();

            foreach (var itemDataTypeSo in itemDataTypeSos)
            {
                var displayData = displayedContainers
                    .Find(data =>
                        data is { ItemContainer: not null } &&
                        data.ItemContainer.ItemDataSo.ItemDataTypeSo == itemDataTypeSo);

                if (displayData == null) continue;

                var foundDisplayFiller = FindDisplayFillerBy(displayData, displayData.InventorySo != null);

                if (foundDisplayFiller != null)
                {
                    foundDisplayFiller.Sort();
                }
            }
        }

        private DisplayFiller GetDisplayFillerPrefab(bool isCharacterInventory)
        {
            var inventoryDisplayPrefab = GetInventoryDisplayPrefab(isCharacterInventory);

            if (inventoryDisplayPrefab != null) return inventoryDisplayPrefab;

            Debug.LogError("Display filler prefab not configured in InventorySettingsSo...".Settings());
            Debug.LogError("Inventory Display not configured correctly...".Configuration());
            return null;
        }

        private DisplayFiller GetInventoryDisplayPrefab(bool isCharacterInventory)
        {
            var inventorySettingsAnchorSo = StaticInventoryContext.InventorySettingsAnchorSo;

            if (inventorySettingsAnchorSo == null)
            {
                return null;
            }

            if (isCharacterInventory)
            {
                return inventorySettingsAnchorSo.InventorySettingsSo.CharacterInventoryFiller;
            }

            return customPrefabDisplayFiller != null
                ? customPrefabDisplayFiller
                : inventorySettingsAnchorSo.InventorySettingsSo.ItemContainerFiller;
        }

        public bool IsItemContainerOpened(ItemTable itemContainer)
        {
            return FindDisplayDataByItem(itemContainer) != null;
        }

        private DisplayData FindDisplayDataByItem(ItemTable itemTable)
        {
            var displayedContainers = GetDisplayedContainers();

            return displayedContainers.FirstOrDefault(display => IsDisplayContainItemContainer(itemTable, display));
        }

        public bool IsInventoryOpened(InventorySo inventorySo)
        {
            var displayedContainers = GetDisplayedContainers();

            return displayedContainers.FirstOrDefault(display => IsDisplayContainInventorySo(inventorySo, display)) !=
                   null;
        }

        private static bool IsDisplayContainItemContainer(ItemTable itemContainer, DisplayData currentDisplay)
        {
            return currentDisplay is { ItemContainer: not null } && currentDisplay.ItemContainer == itemContainer;
        }

        private static bool IsDisplayContainInventorySo(InventorySo inventorySo, DisplayData currentDisplay)
        {
            return currentDisplay is { InventorySo: not null } && currentDisplay.InventorySo == inventorySo;
        }

        public bool IsDisplayOpened(DisplayData displayData)
        {
            if (displayData == null)
            {
                return false;
            }

            return (displayData.InventorySo != null && IsInventoryOpened(displayData.InventorySo))
                   || (displayData.ItemContainer != null && IsItemContainerOpened(displayData.ItemContainer));
        }
    }
}