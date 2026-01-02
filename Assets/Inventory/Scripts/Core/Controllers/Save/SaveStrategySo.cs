using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.Save
{
    public abstract class SaveStrategySo : ScriptableObject
    {
        // TODO: Add metadata dictionary in order to add more content to the save.
        public abstract void Save(string key, string jsonEquippedItems);

        /**
         * Return the json data based on the key.
         */
        public abstract string Load(string key);
    }
}