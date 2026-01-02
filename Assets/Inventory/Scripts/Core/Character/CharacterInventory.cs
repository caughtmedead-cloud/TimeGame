using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core.Character
{
    public class CharacterInventory : MonoBehaviour
    {
        [SerializeField] private InventorySo characterInventorySo;

        public void OpenCharacterInventory()
        {
            characterInventorySo.OpenInventory();
        }

        public void CloseCharacterInventory()
        {
            characterInventorySo.CloseInventory();
        }

        public void ToggleCharacterInventory()
        {
            characterInventorySo.ToggleInventory();
        }
    }
}