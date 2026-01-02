using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Inventory.Scripts.Core.Environment;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors.Display;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers
{
    public abstract class StaticInventoryContext
    {
        // Loaded Context
        private static List<ItemDataSo> _loadedItemsData = new();
        private static readonly List<Holder> InitializedHolders = new();
        public static readonly List<InventoryDisplayAnchorSo> InitializedDisplays = new();
        public static WindowController WindowManager;
        public static OptionsController OptionsController;

        // References
        public static InventorySettingsAnchorSo InventorySettingsAnchorSo;
        public static InventorySupplierSo InventorySupplierSo;
        public static OnAudioStateEventChannelSo AudioStateEventChannelSo;
        public static InventorySo GroundItemsInventorySo;

        private static AbstractGridSelectedAnchorSo _abstractGridSelectedAnchorSo;
        private static ItemHolderSelectedAnchorSo _itemHolderSelectedAnchorSo;
        private static AbstractItem _dragRepresentationInventoryItem;

        // UI Opening Handles
        public static bool IsInventoryUIOpened;
        public static event Action OnOpenInventoryUI;
        public static Action CloseInventoryUICallBack { get; private set; }

        private static GameObject _uiInventoryObject;

        public static void SetSettingsProperties(
            InventorySettingsAnchorSo inventorySettingsAnchorSo,
            InventorySupplierSo inventorySupplierSo,
            InventorySo groundItemsInventorySo
        )
        {
            InventorySettingsAnchorSo = inventorySettingsAnchorSo;
            InventorySupplierSo = inventorySupplierSo;
            GroundItemsInventorySo = groundItemsInventorySo;
        }

        public static void AddItem(ItemDataSo itemDataSo)
        {
            if (_loadedItemsData.Contains(itemDataSo)) return;

            _loadedItemsData.Add(itemDataSo);
        }

        public static void AddHolder(Holder holder)
        {
            if (InitializedHolders.Contains(holder)) return;

            InitializedHolders.Add(holder);
        }

        public static void RemoveHolder(Holder holder)
        {
            InitializedHolders.Remove(holder);
        }

        public static void AddDisplays(InventoryDisplayAnchorSo inventoryDisplayAnchorSo)
        {
            if (InitializedDisplays.Contains(inventoryDisplayAnchorSo)) return;

            InitializedDisplays.Add(inventoryDisplayAnchorSo);
        }

        public static void AddWindowManagerReference(WindowController windowController)
        {
            WindowManager = windowController;
        }

        public static ItemDataSo GetItemDataFromId(string itemId)
        {
            var allItems = FindAllItems();

            var itemDataFromId = allItems.FirstOrDefault(item => item.uniqueUid.Uid.Equals(itemId));

            if (itemDataFromId == null)
            {
                Debug.LogWarning($"Could not find any item data so with this id... {itemId}".SaveSystem());
            }

            return itemDataFromId;
        }

        private static List<ItemDataSo> FindAllItems()
        {
            if (_loadedItemsData is { Count: > 0 })
            {
                return _loadedItemsData;
            }

            _loadedItemsData ??= Resources.FindObjectsOfTypeAll<ItemDataSo>().ToList();

            return _loadedItemsData;
        }

        public static Holder GetHolderFromId(string holderId, ItemTable itemEquipped)
        {
            if (itemEquipped == null)
            {
                return GetInitializedHolders(true)
                    .Find(holder => holder.uniqueUid.Uid.Equals(holderId));
            }

            return GetInitializedHolders(true)
                .Find(holder => holder.uniqueUid.Uid.Equals(holderId) && itemEquipped == holder.GetItemEquipped());
        }

        public static List<Holder> GetInitializedHolders(bool withNonStorableHolders = false)
        {
            if (withNonStorableHolders)
            {
                return InitializedHolders;
            }

            return InitializedHolders
                .Where(holder => holder.GetType() != typeof(ReplicatorItemHolder))
                .ToList();
        }

        public static AbstractGridSelectedAnchorSo GetAbstractGridSelectedAnchorSo()
        {
            if (_abstractGridSelectedAnchorSo == null)
            {
                _abstractGridSelectedAnchorSo =
                    Resources.FindObjectsOfTypeAll<AbstractGridSelectedAnchorSo>().FirstOrDefault();
            }

            return _abstractGridSelectedAnchorSo;
        }

        public static ItemHolderSelectedAnchorSo GetItemHolderSelectedAnchorSo()
        {
            if (_itemHolderSelectedAnchorSo == null)
            {
                _itemHolderSelectedAnchorSo =
                    Resources.FindObjectsOfTypeAll<ItemHolderSelectedAnchorSo>().FirstOrDefault();
            }

            return _itemHolderSelectedAnchorSo;
        }

        public static ItemTable CreateItemTable(ItemDataSo itemDataSo, bool isDragRepresentation = false)
        {
            return new ItemTable(itemDataSo, isDragRepresentation);
        }

        public static AbstractItem GetDragRepresentationInstance(Transform dragParentTransform)
        {
            if (_dragRepresentationInventoryItem != null)
            {
                _dragRepresentationInventoryItem.transform.SetParent(dragParentTransform);
                return _dragRepresentationInventoryItem;
            }

            _dragRepresentationInventoryItem = CreateSimpleAbstractItem(dragParentTransform);
            var dragRepresentationGameObject = _dragRepresentationInventoryItem.gameObject;
            dragRepresentationGameObject.SetActive(false);
            dragRepresentationGameObject.name += "_BeforeDragItemShowing";

            return _dragRepresentationInventoryItem;
        }

        public static AbstractItem CreateSimpleAbstractItem(Transform transform)
        {
            var itemPrefab = InventorySettingsAnchorSo.InventorySettingsSo.ItemPrefabSo.ItemPrefab;

            var instantiate = GameObject.Instantiate(itemPrefab);
            var abstractItem = instantiate.GetComponent<AbstractItem>();

            var transformFromItem = abstractItem.GetComponent<Transform>();
            transformFromItem.SetParent(transform);

            return abstractItem;
        }


        // TODO: Add validation to filter only managers with window opened. There no need to execute the for if
        // a manager does not have any window opened.
        public static void CloseDisplayContainerIfNeeded(ItemTable itemEquipped)
        {
            if (itemEquipped == null) return;

            if (!itemEquipped.IsContainer()) return;

            foreach (var containerDisplayAnchorSo in InitializedDisplays)
            {
                containerDisplayAnchorSo.CloseContainer(itemEquipped);
            }

            var metadata = itemEquipped.GetMetadata<ContainerMetadata>();

            var itemTables = metadata.GetAllItems(true);

            WindowManager?.CloseWindow(itemEquipped);

            foreach (var itemTable in itemTables)
            {
                WindowManager?.CloseWindow(itemTable);
            }
        }

        public static EnvironmentItemHolder InstantiateItemInTheWorld(ItemDataSo itemDataSo,
            Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var prefabItem = itemDataSo.Prefab;

            if (prefabItem == null)
            {
                Debug.LogWarning(
                    $"Cannot instantiate component if no prefab was set on the item data so. Please set the prefab on the {itemDataSo.DisplayName} item.");
                return null;
            }

            return CreateEnvironmentItemHolder(
                position,
                rotation,
                scale,
                prefabItem,
                itemDataSo
            );
        }

        /// <summary>
        /// Will place the item in to a EnvironmentItemHolder that will represent the item in the game world.
        /// </summary>
        /// <param name="itemTable">item will be dropped</param>
        /// <param name="dropTransform">transform where will be located.</param>
        /// <returns></returns>
        public static EnvironmentItemHolder DropItemToEnvironmentItemHolder(ItemTable itemTable,
            Transform dropTransform)
        {
            var itemDataSo = itemTable.ItemDataSo;
            var prefabItem = itemDataSo.Prefab;

            var environmentItem = CreateEnvironmentItemHolder(dropTransform, prefabItem, itemDataSo);

            if (environmentItem == null)
            {
                return null;
            }

            environmentItem.Equip(itemTable);

            return environmentItem;
        }

        private static EnvironmentItemHolder CreateEnvironmentItemHolder(
            Transform dropTransform,
            EnvironmentItemHolder prefabItem,
            ItemDataSo itemDataSo
        )
        {
            if (dropTransform == null)
            {
                Debug.LogWarning(
                    $"Cannot drop item because no dropTransform was provided on DropItemToEnvironmentItemHolder...");
                return null;
            }

            if (prefabItem == null)
            {
                Debug.LogWarning(
                    $"Cannot instantiate component if no prefab was set on the item data so. Please set the data on the {itemDataSo.DisplayName}");
                return null;
            }

            return CreateEnvironmentItemHolder(
                dropTransform.position,
                dropTransform.rotation,
                prefabItem.transform.localScale,
                prefabItem,
                itemDataSo
            );
        }

        private static EnvironmentItemHolder CreateEnvironmentItemHolder(Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            EnvironmentItemHolder prefabItem,
            ItemDataSo itemDataSo)
        {
            var dropInstantiated = GameObject.Instantiate(prefabItem, position, rotation);
            dropInstantiated.transform.localScale = scale;

            var environmentItemHolder = dropInstantiated.GetComponent<EnvironmentItemHolder>();

            if (environmentItemHolder == null)
            {
                throw new DataException(
                    $"The prefab {prefabItem.name} has no {nameof(EnvironmentItemHolder)} component attached. Please attach the component to this prefab object.");
            }

            dropInstantiated.name = $"{itemDataSo.DisplayName}_{dropInstantiated.name}";

            return environmentItemHolder;
        }

        public static ItemTable RetrieveItemByEnvironmentHolderId(string uid)
        {
            var itemDataSo = RetrieveItemDataBasedOnEnvironmentItemHolder(uid);

            if (itemDataSo == null)
            {
                throw new DataException(
                    $"Cannot find an item that the prefab contains the {nameof(EnvironmentItemHolder)} uid equals to this: {uid}. " +
                    $"Please make this {nameof(GameObject)} a prefab and attach to your {nameof(ItemDataSo)} then.");
            }

            return CreateItemTable(itemDataSo);
        }

        private static ItemDataSo RetrieveItemDataBasedOnEnvironmentItemHolder(string uid)
        {
            return FindAllItems()
                .Find(itemDataSo => itemDataSo != null
                                    && itemDataSo.Prefab != null
                                    && itemDataSo.Prefab.uniqueUid?.Uid != null
                                    && itemDataSo.Prefab.uniqueUid.Uid.Equals(uid));
        }

        public static GridResponse AddItemToPlayerInventory(ItemTable itemTable)
        {
            return InventorySupplierSo.PlaceItemInPlayerInventory(itemTable);
        }

        public static InventorySo GetPlayerInventory()
        {
            return InventorySettingsAnchorSo?.InventorySettingsSo?.PlayerInventorySo;
        }

        public static void SetCanvasUIGameObject(GameObject uiInventoryGameObject)
        {
            _uiInventoryObject = uiInventoryGameObject;
        }

        public static void OpenInventoryUI(Action onCloseCallBack)
        {
            if (_uiInventoryObject == null)
            {
                Debug.LogError(
                    $"Please set the 'uiInventoryObject' variable in the '{nameof(InventoryConfiguration)}' component on object 'InventoryManager'."
                        .Error());
                return;
            }

            CloseInventoryUICallBack = onCloseCallBack;
            _uiInventoryObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            IsInventoryUIOpened = true;
            OnOpenInventoryUI?.Invoke();
        }

        public static void CloseInventoryUI()
        {
            if (_uiInventoryObject == null)
            {
                Debug.LogError(
                    $"Please set the 'uiInventoryObject' variable in the '{nameof(InventoryConfiguration)}' component on object 'InventoryManager'."
                        .Error());
                return;
            }

            _uiInventoryObject.SetActive(false);
            IsInventoryUIOpened = false;
        }

        /// <summary>
        /// Method called once the Inventory UI is disabled.
        /// </summary>
        public static void DisableUI()
        {
            CloseAllWindows();
            IsInventoryUIOpened = false;
            CloseInventoryUICallBack?.Invoke();
        }

        public static void ToggleInventoryUI(Action onOpenCallback, Action onCloseCallBack)
        {
            if (IsInventoryUIOpened)
            {
                CloseInventoryUI();
            }
            else
            {
                OpenInventoryUI(onCloseCallBack);
                onOpenCallback?.Invoke();
            }
        }

        public static void CloseAllWindows()
        {
            WindowManager?.CloseAll();
            OptionsController?.CloseOptions();
        }
    }
}