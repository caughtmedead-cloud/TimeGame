using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events;
using UnityEngine;

namespace Inventory.Scripts.Options
{
    public abstract class OptionExecutionManager : MonoBehaviour
    {
        [Header("Listening execution on...")] [Tooltip("The option event that was executed")] [SerializeField]
        private OnItemExecuteOptionEventChannelSo onItemExecuteEquipOptionEventChannelSo;

        protected virtual void OnEnable()
        {
            onItemExecuteEquipOptionEventChannelSo.OnEventRaised += HandleOption;
        }

        protected virtual void OnDisable()
        {
            onItemExecuteEquipOptionEventChannelSo.OnEventRaised -= HandleOption;
        }

        private void HandleOption(AbstractItem itemUi)
        {
            if (itemUi == null)
            {
                Debug.LogWarning($"Avoiding handling option execution with item null on {name} script.".Broadcasting());
                return;
            }

            HandleOptionExecution(itemUi);
        }

        protected abstract void HandleOptionExecution(AbstractItem itemUi);
    }
}