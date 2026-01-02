using Inventory.Scripts.Core.Controllers;
using UnityEngine;

namespace Inventory.Scripts.Core
{
    public class InventoryUIHandler : MonoBehaviour
    {
        private void OnDisable()
        {
            StaticInventoryContext.DisableUI();
        }
    }
}