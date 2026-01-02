using Inventory.Scripts.Core.Controllers.Draggable;
using Inventory.Scripts.Core.Controllers.DragRepresentation;
using Inventory.Scripts.Core.Controllers.Highlights;
using Inventory.Scripts.Core.Controllers.Inputs;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events.Interact;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventory.Scripts.Core.Controllers
{
    public class DraggableController : MonoBehaviour
    {
        [Header("Provider Configuration")] [SerializeField]
        private DraggableProviderSo draggableProviderSo;

        [SerializeField] private HighlighterProviderSo highlighterProviderSo;

        [Header("Item Configuration")] [SerializeField] [Tooltip("Used to item being drag not appear behind the grid.")]
        private RectTransformPrefabAnchorSo defaultItemParentWhenDrag;

        [FormerlySerializedAs("onAbstractItemBeingDragEventChannelSo")] [Header("Highlight Reference")] [SerializeField]
        private AbstractItemEventChannelSo abstractItemEventChannelSo;

        [Header("Listening on...")] [SerializeField]
        private OnGridInteractEventChannelSo onGridInteractEventChannelSo;

        [SerializeField] private OnItemHolderInteractEventChannelSo onItemHolderInteractEventChannelSo;

        [Header("Inputs")] [SerializeField] private InputProviderSo inputProvider;

        private AbstractGrid _selectedAbstractGrid;
        private ItemHolder _selectedItemHolder;
        private AbstractItem _selectedInventoryItem;

        private AbstractItem _inventoryItemDragRepresentation;

        private PickupContext _pickupContext;
        private PickupState _pickupState;

        private bool _isPicking;

        private void OnEnable()
        {
            inputProvider.OnRotateItem += RotateItem;
            inputProvider.OnPickupItem += OnPickupItem;
            inputProvider.OnReleaseItem += OnReleaseItem;

            onGridInteractEventChannelSo.OnEventRaised += ChangeAbstractGrid;
            onItemHolderInteractEventChannelSo.OnEventRaised += ChangeItemHolderHolder;
        }

        private void OnDisable()
        {
            inputProvider.OnRotateItem -= RotateItem;
            inputProvider.OnPickupItem -= OnPickupItem;
            inputProvider.OnReleaseItem -= OnReleaseItem;

            onGridInteractEventChannelSo.OnEventRaised -= ChangeAbstractGrid;
            onItemHolderInteractEventChannelSo.OnEventRaised -= ChangeItemHolderHolder;
        }

        private void ChangeAbstractGrid(AbstractGrid abstractGrid)
        {
            _selectedAbstractGrid = abstractGrid;
        }

        private void ChangeItemHolderHolder(ItemHolder itemHolder)
        {
            _selectedItemHolder = itemHolder;
        }

        private void Update()
        {
            UpdateItemIconDragging();
        }

        private void UpdateItemIconDragging()
        {
            if (_selectedInventoryItem == null) return;

            var selectedItemTransform = _selectedInventoryItem.transform;

            var inputState = inputProvider.GetState();

            var cursorPosition = inputState.CursorPosition;

            selectedItemTransform.position = Vector3.Lerp(
                selectedItemTransform.position,
                cursorPosition,
                Time.deltaTime * StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo
                    .ItemDragMovementSpeed
            );
            selectedItemTransform.SetAsLastSibling();
        }

        private void RotateItem()
        {
            if (_selectedInventoryItem == null)
                return;

            _selectedInventoryItem.Rotate();
        }

        private void OnPickupItem()
        {
            if (_isPicking) return;

            _isPicking = true;

            _pickupContext = new PickupContext(
                _selectedInventoryItem,
                _selectedAbstractGrid,
                _selectedItemHolder,
                GetTileGridHelperSo()
            );

            _pickupState = draggableProviderSo.ProcessPickup(_pickupContext);

            _selectedInventoryItem = _pickupState.Item;

            if (_selectedInventoryItem == null) return;

            PlayAudioPickup();
            _selectedInventoryItem.SetDraggingStyle();
            _selectedInventoryItem.transform.SetParent(defaultItemParentWhenDrag.Value);
            _inventoryItemDragRepresentation = StartDragRepresentation(
                _pickupContext.AbstractGridFromStartDragging,
                _pickupContext.ItemHolderFromStartDragging,
                _selectedInventoryItem
            );
            abstractItemEventChannelSo.RaiseEvent(_selectedInventoryItem);
        }

        private void OnReleaseItem()
        {
            _isPicking = false;

            if (_pickupContext == null || _pickupState == null) return;

            if (_selectedInventoryItem == null) return;

            var releaseContext = new ReleaseContext(
                _pickupState,
                _selectedAbstractGrid,
                _selectedItemHolder,
                _pickupContext.TileGridHelperSo
            );

            StopDragRepresentation();

            draggableProviderSo.ProcessRelease(releaseContext);

            PlayAudioPlace();
            ResetDraggableState();
        }

        private AbstractItem StartDragRepresentation(AbstractGrid abstractGrid, ItemHolder itemHolderFromPickup,
            AbstractItem selectedInventoryItem)
        {
            if (selectedInventoryItem != null)
            {
                selectedInventoryItem.ResizeIcon(true);
            }

            if (ShowDragRepresentation() && abstractGrid != null)
            {
                return DragRepresentationBuilder.PlaceDragRepresentation(
                    selectedInventoryItem != null ? selectedInventoryItem.ItemTable : null,
                    abstractGrid.gameObject.transform
                );
            }

            if (itemHolderFromPickup != null)
            {
                var itemEquipped = itemHolderFromPickup.GetItemEquipped();

                return DragRepresentationBuilder.PlaceDragRepresentation(
                    itemEquipped,
                    itemHolderFromPickup.gameObject.transform
                );
            }

            return null;
        }

        private void StopDragRepresentation()
        {
            DragRepresentationBuilder.RemoveDragRepresentation(_inventoryItemDragRepresentation);
            _inventoryItemDragRepresentation = null;
        }

        private void ResetDraggableState()
        {
            _pickupState = null;
            _selectedInventoryItem = null;
            abstractItemEventChannelSo.RaiseEvent(null);
            highlighterProviderSo.NonHighlight();
        }

        private void PlayAudioPlace()
        {
            var audioCueSo = _selectedInventoryItem.ItemTable.ItemDataSo.AudioCueSo;
            if (audioCueSo)
            {
                StaticInventoryContext.AudioStateEventChannelSo.RaiseEvent(audioCueSo.OnBeingPlace);
            }
        }

        private void PlayAudioPickup()
        {
            var audioCueSo = _selectedInventoryItem.ItemTable.ItemDataSo.AudioCueSo;
            if (audioCueSo)
            {
                StaticInventoryContext.AudioStateEventChannelSo.RaiseEvent(audioCueSo.OnBeingPicked);
            }
        }

        private TileGridHelperSo GetTileGridHelperSo()
        {
            return StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.TileGridHelperSo;
        }

        private bool ShowDragRepresentation()
        {
            return StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.ShowDragRepresentation;
        }

        public void SetItemParent(RectTransformPrefabAnchorSo parentTransform)
        {
            defaultItemParentWhenDrag = parentTransform;
        }
    }
}