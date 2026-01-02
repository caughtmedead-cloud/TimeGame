using System.Linq;
using Inventory.Scripts.Core.Controllers.Draggable;
using Inventory.Scripts.Core.Controllers.Inputs;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events.Interact;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers
{
    public class InventoryOptionsController : MonoBehaviour
    {
        [Header("Options Settings")] [SerializeField]
        private RectTransformPrefabAnchorSo defaultOptionsParent;

        [SerializeField] private InputProviderSo inputProviderSo;

        [SerializeField] private DraggableProviderSo draggableProviderSo;

        [Header("Listening on...")] [SerializeField]
        private OnOptionInteractEventChannelSo onOptionInteractEventChannelSo;

        private AbstractItem _selectedInventoryItem;

        private OptionsController _optionsController;
        private RectTransform _optionsControllerRectTransform;

        private OptionsController _selectedOptionsController;

        private void Start()
        {
            var optionsControllerGameObject =
                Instantiate(GetOptionsController(), defaultOptionsParent.Value);

            _optionsController = optionsControllerGameObject.GetComponent<OptionsController>();
            _optionsControllerRectTransform = optionsControllerGameObject.GetComponent<RectTransform>();
            _optionsController.CloseOptions();
            StaticInventoryContext.OptionsController = _optionsController;
        }

        private void OnEnable()
        {
            inputProviderSo.OnReleaseItem += OnLeftClick;
            inputProviderSo.OnToggleOptions += OnRightClick;

            onOptionInteractEventChannelSo.OnEventRaised += ChangeOptionController;
        }

        private void OnDisable()
        {
            inputProviderSo.OnReleaseItem -= OnLeftClick;
            inputProviderSo.OnToggleOptions -= OnRightClick;

            onOptionInteractEventChannelSo.OnEventRaised -= ChangeOptionController;
        }

        private void Update()
        {
            if (draggableProviderSo.IsDragging())
            {
                ResetOpenState();
            }
        }

        private void OnLeftClick()
        {
            if (!_optionsController.gameObject.activeSelf || _selectedOptionsController != null) return;

            _optionsController.CloseOptions();
        }

        private void OnRightClick()
        {
            if (_optionsController.gameObject.activeSelf)
            {
                _optionsController.CloseOptions();
                return;
            }

            _selectedInventoryItem = GetInventoryItem();

            if (_selectedInventoryItem == null)
            {
                OnLeftClick();
                return;
            }

            var itemOptions = _selectedInventoryItem.ItemTable.ItemOptions;

            if (itemOptions == null) return;

            var orderedEnumerable = itemOptions.Options.OrderBy(so => so.Order).ToList();

            _optionsController.SetOptions(_selectedInventoryItem, orderedEnumerable);

            OpenOptionsMenu();
        }

        private void OpenOptionsMenu()
        {
            var inputState = inputProviderSo.GetState();
            var cursorPosition = inputState.CursorPosition;

            _optionsControllerRectTransform.position = new Vector3(cursorPosition.x + 8, cursorPosition.y + -8, 0f);
            _optionsController.OpenOptions();
        }

        private AbstractItem GetInventoryItem()
        {
            var itemHolderSelectedAnchorSo = StaticInventoryContext.GetItemHolderSelectedAnchorSo();

            if (itemHolderSelectedAnchorSo.isSet)
            {
                var inventoryItem = itemHolderSelectedAnchorSo.Value.GetAbstractItem();

                if (inventoryItem != null)
                    return inventoryItem;
            }

            var abstractGridSelectedAnchorSo = StaticInventoryContext.GetAbstractGridSelectedAnchorSo();

            if (!abstractGridSelectedAnchorSo.isSet) return null;

            var selectedAbstractGrid = abstractGridSelectedAnchorSo.Value;

            var tileGridHelperSo = GetTileGridHelperSo();

            var tileGridPosition =
                tileGridHelperSo.GetTileGridPositionByGridTable(selectedAbstractGrid.transform);

            return selectedAbstractGrid.Grid.GetItem(tileGridPosition.x, tileGridPosition.y)?.GetAbstractItem();
        }

        private void ChangeOptionController(OptionsController optionsController)
        {
            _selectedOptionsController = optionsController;
        }

        private void ResetOpenState()
        {
            _optionsController.CloseOptions();
        }

        private OptionsController GetOptionsController()
        {
            return StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.OptionsController;
        }

        private TileGridHelperSo GetTileGridHelperSo()
        {
            return StaticInventoryContext.InventorySettingsAnchorSo.InventorySettingsSo.TileGridHelperSo;
        }

        public void SetOptionsParent(RectTransformPrefabAnchorSo rectTransformPrefabAnchorSo)
        {
            defaultOptionsParent = rectTransformPrefabAnchorSo;
        }
    }
}