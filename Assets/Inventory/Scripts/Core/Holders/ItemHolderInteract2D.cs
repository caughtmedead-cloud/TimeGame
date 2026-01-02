using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events.Interact;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventory.Scripts.Core.Holders
{
    [DefaultExecutionOrder(-1)]
    [RequireComponent(typeof(ItemHolder))]
    public class ItemHolderInteract2D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private OnItemHolderInteractEventChannelSo onItemHolderInteractEventChannelSo;
        [SerializeField] private ItemHolderSelectedAnchorSo itemHolderSelectedAnchorSo;

        private ItemHolder _itemHolder;

        private void Awake()
        {
            _itemHolder = GetComponent<ItemHolder>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            itemHolderSelectedAnchorSo.Value = _itemHolder;
            onItemHolderInteractEventChannelSo.RaiseEvent(_itemHolder);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            itemHolderSelectedAnchorSo.Value = null;
            onItemHolderInteractEventChannelSo.RaiseEvent(null);
        }

        private void OnDisable()
        {
            itemHolderSelectedAnchorSo.Value = null;
            onItemHolderInteractEventChannelSo.RaiseEvent(null);
        }
    }
}