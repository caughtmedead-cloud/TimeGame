using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors
{
    public class PrefabAnchorSo<T> : ScriptableObject where T : Object
    {
        [SerializeField] protected T prefab;

        private T _value;

        public T Value
        {
            get
            {
                if (_value == null)
                {
                    _value = InstantiatePrefab();
                }

                return _value;
            }
        }

        protected virtual T InstantiatePrefab()
        {
            return Instantiate(prefab);
        }

        private void OnDisable()
        {
            // Sets to null when stops the game in the editor
            _value = null;
        }
    }
}