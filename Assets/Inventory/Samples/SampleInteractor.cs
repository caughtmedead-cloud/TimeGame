using System;
using Inventory.Scripts.Core.Character;
using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Environment;
using UnityEngine;

namespace Inventory.Samples
{
    /// <summary>
    /// This is a sample interactor... You can replace this based on your game scripts.
    /// </summary>
    public class SampleInteractor : MonoBehaviour
    {
        public SampleCameraMove sampleCameraMove; // Reference to the camera
        public float maxDistance = 2.5f; // Maximum distance for raycasting
        public LayerMask interactableMask; // LayerMask to filter interactable objects

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StaticInventoryContext.CloseInventoryUI();
                OnCloseCallBack();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                StaticInventoryContext.ToggleInventoryUI(OnOpenCallBack(), OnCloseCallBack());
                return;
            }

            if (StaticInventoryContext.IsInventoryUIOpened)
            {
                return;
            }

            // Perform a raycast from the camera's position in the forward direction
            var ray = new Ray(sampleCameraMove.transform.position, sampleCameraMove.transform.forward);

            if (!Physics.Raycast(ray, out var hit, maxDistance, interactableMask))
            {
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red);
                return;
            }

            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.yellow);

            if (!Input.GetKeyDown(KeyCode.E)) return;

            // Check for each component and perform actions accordingly
            if (hit.collider.TryGetComponent(out CharacterInventory inventory))
            {
                HandleInventory(inventory);
            }
            else if (hit.collider.TryGetComponent(out EnvironmentItemHolder itemHolder))
            {
                HandleItemHolder(itemHolder);
            }
            else if (hit.collider.TryGetComponent(out EnvironmentContainerHolder containerHolder))
            {
                HandleContainerHolder(containerHolder);
            }

            Debug.DrawRay(ray.origin, ray.direction * hit.distance, Color.green);
        }

        private void HandleInventory(CharacterInventory inventory)
        {
            Debug.Log($"Interacting with {inventory.gameObject.name}");

            // Will open the Inventory UI.
            StaticInventoryContext.OpenInventoryUI(OnCloseCallBack());
            sampleCameraMove.Freeze();
            inventory.OpenCharacterInventory();
        }

        private static void HandleItemHolder(EnvironmentItemHolder itemHolder)
        {
            Debug.Log($"Interacting with {itemHolder.gameObject.name}");

            // Will pickup the item and put directly into the player inventory.
            StaticInventoryContext.InventorySupplierSo.PickEnvironmentItem(itemHolder);
        }

        private void HandleContainerHolder(EnvironmentContainerHolder containerHolder)
        {
            Debug.Log($"Interacting with {containerHolder.gameObject.name}");

            // Will open the Inventory UI.
            StaticInventoryContext.OpenInventoryUI(OnCloseCallBack());
            sampleCameraMove.Freeze();
            containerHolder.OpenContainer();
        }

        private Action OnOpenCallBack()
        {
            return () =>
            {
                Debug.Log("Executing on open inventory event...");
                sampleCameraMove.Freeze();
            };
        }

        private Action OnCloseCallBack()
        {
            return () =>
            {
                Debug.Log("Executing on close inventory event...");
                sampleCameraMove.Unfreeze();
            };
        }
    }
}