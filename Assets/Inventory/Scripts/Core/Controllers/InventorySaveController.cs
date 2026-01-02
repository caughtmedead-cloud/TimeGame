using Inventory.Scripts.Core.Controllers.Save;
using Inventory.Scripts.Core.ScriptableObjects;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers
{
    public class InventorySaveController : MonoBehaviour
    {
        [SerializeField] private SaveStrategySo saveStrategySo;

        [SerializeField] private InventorySo[] saveableInventories;

        public void SaveAll()
        {
            foreach (var inventorySo in saveableInventories)
            {
                var saveStrategy = inventorySo.SaveStrategySo
                    ? inventorySo.SaveStrategySo
                    : saveStrategySo;

                inventorySo.Save(saveStrategy);
            }
        }

        public void LoadAll()
        {
            foreach (var inventorySo in saveableInventories)
            {
                var saveStrategy = inventorySo.SaveStrategySo
                    ? inventorySo.SaveStrategySo
                    : saveStrategySo;

                inventorySo.Load(saveStrategy);
            }
        }
    }
}