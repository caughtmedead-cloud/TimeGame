using Inventory.Scripts.Core.Controllers;
using UnityEngine;

namespace Inventory.Samples.Examples
{
    public class PickItemFromEnvironmentExample : MonoBehaviour
    {
        /**
         * This item you will have the reference through your interactable system... Here for example purpose we put as a Serializable field
         */
        [SerializeField] private GameObject itemOnEnvironment;

        public void PickItemFromEnvironment()
        {
            if (itemOnEnvironment == null) return;

            StaticInventoryContext.InventorySupplierSo.PickEnvironmentItem(itemOnEnvironment);
        }
    }
}