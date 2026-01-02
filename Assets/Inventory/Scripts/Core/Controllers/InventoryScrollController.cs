using Inventory.Scripts.Core.Controllers.Draggable;
using Inventory.Scripts.Core.Controllers.Inputs;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events.Interact;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core.Controllers
{
    public class InventoryScrollController : MonoBehaviour
    {
        [Header("Scroll Configuration")] [SerializeField]
        private InventorySettingsAnchorSo inventorySettingsAnchorSo;

        [SerializeField] private DraggableProviderSo draggableProviderSo;

        [Header("Listening on...")] [SerializeField]
        private InputProviderSo inputProviderSo;

        [SerializeField] private OnScrollRectInteractEventChannelSo onScrollRectInteractEventChannelSo;

        private ScrollRect _currentScrollRectSelected;

        private void OnEnable()
        {
            onScrollRectInteractEventChannelSo.OnEventRaised += ChangeScrollRect;
        }

        private void OnDisable()
        {
            onScrollRectInteractEventChannelSo.OnEventRaised -= ChangeScrollRect;
        }

        private void ChangeScrollRect(ScrollRect scrollRect)
        {
            _currentScrollRectSelected = scrollRect;
        }

        private void Update()
        {
            if (!draggableProviderSo.IsDragging()) return;

            if (_currentScrollRectSelected == null) return;

            var tileGridHelperSo = GetTileGridHelperSo();

            var cursorPosition = inputProviderSo.GetState().CursorPosition;

            var pointerViewportPosition =
                tileGridHelperSo.GetLocalPosition(_currentScrollRectSelected.viewport, cursorPosition);

            if (pointerViewportPosition.y < _currentScrollRectSelected.viewport.rect.min.y + GetHoldScrollPadding())
            {
                var rect = _currentScrollRectSelected.viewport.rect;
                var scrollValue = _currentScrollRectSelected.verticalNormalizedPosition * rect.height;
                scrollValue -= GetHoldScrollRate() * Time.deltaTime;
                _currentScrollRectSelected.verticalNormalizedPosition = Mathf.Clamp01(scrollValue / rect.height);
            }

            if (pointerViewportPosition.y > _currentScrollRectSelected.viewport.rect.max.y - GetHoldScrollPadding())
            {
                var rect = _currentScrollRectSelected.viewport.rect;
                var scrollValue = _currentScrollRectSelected.verticalNormalizedPosition * rect.height;
                scrollValue += GetHoldScrollRate() * Time.deltaTime;
                _currentScrollRectSelected.verticalNormalizedPosition = Mathf.Clamp01(scrollValue / rect.height);
            }
        }

        private float GetHoldScrollRate()
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.HoldScrollRate;
        }

        private float GetHoldScrollPadding()
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.HoldScrollPadding;
        }

        private TileGridHelperSo GetTileGridHelperSo()
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.TileGridHelperSo;
        }

        public void SetInventorySettingsAnchorSo(InventorySettingsAnchorSo inventorySettingsAnchor)
        {
            inventorySettingsAnchorSo = inventorySettingsAnchor;
        }
    }
}