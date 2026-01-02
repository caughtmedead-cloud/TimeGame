using Inventory.Scripts.Core.Controllers.Highlights;
using Inventory.Scripts.Core.Controllers.Highlights.Global;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events.Interact;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Tile;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventory.Scripts.Core.Controllers
{
    public class InventoryHighlightController : MonoBehaviour
    {
        [Header("Highlight Settings")] [SerializeField]
        private InventorySettingsAnchorSo inventorySettingsAnchorSo;

        [SerializeField] private HighlighterProviderSo highlighterProviderSo;

        [Header("Listening on...")] [SerializeField]
        private OnGridInteractEventChannelSo onGridInteractEventChannelSo;

        [FormerlySerializedAs("onAbstractItemBeingDragEventChannelSo")] [SerializeField]
        private AbstractItemEventChannelSo abstractItemEventChannelSo;

        private RectTransform _highlightParent;

        private AbstractGrid _selectedAbstractGrid;
        private AbstractItem _selectedInventoryItem;

        private HighlighterContext _highlighterContext;

        private void OnEnable()
        {
            abstractItemEventChannelSo.OnEventRaised += HandleGlobalHighlights;

            onGridInteractEventChannelSo.OnEventRaised += HandleChangeGrid;
            abstractItemEventChannelSo.OnEventRaised += HandleItem;
        }

        private void OnDisable()
        {
            abstractItemEventChannelSo.OnEventRaised -= HandleGlobalHighlights;

            onGridInteractEventChannelSo.OnEventRaised -= HandleChangeGrid;
            abstractItemEventChannelSo.OnEventRaised -= HandleItem;
        }

        private void HandleGlobalHighlights(AbstractItem abstractItem)
        {
            var highlightGlobalContext = new HighlightGlobalContext(
                inventorySettingsAnchorSo.InventorySettingsSo,
                abstractItem,
                GetTileGridHelperSo()
            );

            highlighterProviderSo.HighlightGlobal(highlightGlobalContext);
        }

        private void Update()
        {
            if (_selectedAbstractGrid == null)
            {
                highlighterProviderSo.NonHighlight();
                return;
            }

            var tileGridHelperSo = GetTileGridHelperSo();

            var positionOnGrid =
                tileGridHelperSo.GetTileGridPosition(_selectedAbstractGrid.transform, _selectedInventoryItem);

            _highlighterContext = new HighlighterContext(
                inventorySettingsAnchorSo.InventorySettingsSo,
                positionOnGrid,
                _selectedAbstractGrid,
                _selectedInventoryItem,
                tileGridHelperSo
            );

            highlighterProviderSo.Highlight(_highlighterContext);
        }

        private void HandleChangeGrid(AbstractGrid abstractGrid)
        {
            _selectedAbstractGrid = abstractGrid;

            if (_selectedAbstractGrid == null) return;

            SetParent(_selectedAbstractGrid);
        }

        private void SetParent(AbstractGrid targetGrid)
        {
            if (targetGrid == null)
                return;

            _highlightParent = (RectTransform)targetGrid.GetOrInstantiateHighlightArea();

            highlighterProviderSo.SetParent(_highlightParent);
        }

        private void HandleItem(AbstractItem inventoryItem)
        {
            _selectedInventoryItem = inventoryItem;
        }

        private TileGridHelperSo GetTileGridHelperSo()
        {
            return inventorySettingsAnchorSo.InventorySettingsSo.TileGridHelperSo;
        }

        public void SetHighlighterAnchorSo(HighlighterPrefabAnchorSo highlighterPrefabAnchor)
        {
            highlighterProviderSo.HighlighterPrefabAnchorSo = highlighterPrefabAnchor;
        }

        public void SetInventorySettingsAnchorSo(InventorySettingsAnchorSo inventorySettingsAnchor)
        {
            inventorySettingsAnchorSo = inventorySettingsAnchor;
        }
    }
}