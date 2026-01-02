using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;
using Inventory.Scripts.Core.ScriptableObjects.Audio;
using Inventory.Scripts.Core.ScriptableObjects.Configuration.Events;
using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Options
{
    [CreateAssetMenu(menuName = "Inventory/Options/New Option")]
    public class OptionSo : ScriptableObject
    {
        [Header("Option Event")] [SerializeField]
        private OnItemExecuteOptionEventChannelSo onItemExecuteOptionEventChannelSo;

        [Header("Option Metadata")] [SerializeField]
        private string displayName;

        [Header("Option Settings")] [SerializeField]
        private OptionsType optionsType;

        [SerializeField] private int order;

        [Header("Audio Settings")] [SerializeField]
        private AudioStateSo audioStateSo;

        private AbstractItem _currentInventoryItem;

        public string DisplayName
        {
            get => displayName;
            protected set => displayName = value;
        }

        public OptionsType OptionsType => optionsType;

        public AbstractItem CurrentInventoryItem => _currentInventoryItem;

        public int Order => order;

        public OnItemExecuteOptionEventChannelSo OnItemExecuteOptionEventChannelSo
        {
            get => onItemExecuteOptionEventChannelSo;
            protected set => onItemExecuteOptionEventChannelSo = value;
        }

        private void OnEnable()
        {
            if (onItemExecuteOptionEventChannelSo != null)
                onItemExecuteOptionEventChannelSo.OnEventRaised += Execute;

            OnEnableOption();
        }

        private void OnDisable()
        {
            if (onItemExecuteOptionEventChannelSo != null)
                onItemExecuteOptionEventChannelSo.OnEventRaised -= Execute;

            OnDisableOption();
        }

        protected virtual void OnEnableOption()
        {
        }

        protected virtual void OnDisableOption()
        {
        }

        private void Execute(AbstractItem item)
        {
            _currentInventoryItem = item;

            if (audioStateSo != null)
            {
                StaticInventoryContext.AudioStateEventChannelSo.RaiseEvent(audioStateSo);
            }

            OnExecuteOption();
        }

        protected virtual void OnExecuteOption()
        {
        }
    }
}