using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors
{
    public abstract class AnchorSo<T> : ScriptableObject
    {
        [HideInInspector] public bool
            isSet; // Any script can check if the transform is null before using it, by just checking this bool

        [SerializeField] private T value;

        [Header("Anchor Description")] [SerializeField, TextArea]
        // ReSharper disable once NotAccessedField.Local
        private string description;

        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                isSet = this.value != null;
            }
        }

        private void OnEnable()
        {
            isSet = Value != null;
        }

        public void OnDisable()
        {
            value = default;
            isSet = false;
        }
    }
}