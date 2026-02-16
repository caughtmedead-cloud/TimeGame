using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;

namespace TimeGame.Systems.Inventory
{
    public class PlayerItemInteraction : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Transform to raycast from (usually player head/camera)")]
        [SerializeField] private Transform raycastOrigin;

        [Tooltip("Maximum interaction distance")]
        [SerializeField] private float interactionRange = 3f;

        [Tooltip("Layer mask for world items")]
        [SerializeField] private LayerMask itemLayerMask = -1;

        [Tooltip("Scene physics component for multi-scene physics support")]
        [SerializeField] private ScenePhysics scenePhysics;

        [Header("UI References")]
        [Tooltip("Crosshair UI component (shown when looking at item)")]
        [SerializeField] private InteractionCrosshair crosshairUI;

        [Tooltip("Player inventory manager for pickup")]
        [SerializeField] private PlayerInventoryManager inventoryManager;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private WorldItem currentlyLookedAtItem;
        private PlayerInputActions inputActions;

        #region Unity Lifecycle

        private void Awake()
        {
            if (raycastOrigin == null)
            {
                raycastOrigin = transform;
            }

            if (scenePhysics == null)
            {
                scenePhysics = GetComponent<ScenePhysics>();
            }

            inputActions = new PlayerInputActions();

            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (debugMode)
            {
                Debug.Log("[PlayerItemInteraction] === INITIALIZATION CHECK ===");
                Debug.Log($"  Raycast Origin: {(raycastOrigin != null ? raycastOrigin.name : "NULL")}");
                Debug.Log($"  Interaction Range: {interactionRange}");
                Debug.Log($"  Item Layer Mask: {itemLayerMask.value}");
                Debug.Log($"  Inventory Manager: {(inventoryManager != null ? "Assigned" : "NULL - Pickup will fail!")}");
                Debug.Log("===========================================");
            }
        }

        private void OnEnable()
        {
            inputActions.Enable();
            inputActions.Player.Interact.performed += OnPickupPressed;
            inputActions.Player.AltInteract.performed += OnContextMenuPressed;
        }

        private void OnDisable()
        {
            inputActions.Player.Interact.performed -= OnPickupPressed;
            inputActions.Player.AltInteract.performed -= OnContextMenuPressed;
            inputActions.Disable();
        }

        private void Update()
        {
            DetectWorldItems();
        }

        #endregion

        #region Detection

        private void DetectWorldItems()
        {
            if (raycastOrigin == null) return;

            Vector3 origin = raycastOrigin.position;
            Vector3 direction = raycastOrigin.forward;

            RaycastHit hit;

            if (debugMode)
            {
                Debug.DrawRay(origin, direction * interactionRange, Color.cyan);
            }

            // Use scene-aware raycast if available, otherwise fall back to Physics.Raycast
            bool didHit;
            if (scenePhysics != null)
            {
                didHit = scenePhysics.Raycast(origin, direction, out hit, interactionRange, itemLayerMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                didHit = Physics.Raycast(origin, direction, out hit, interactionRange, itemLayerMask, QueryTriggerInteraction.Ignore);
            }

            if (didHit)
            {
                WorldItem item = hit.collider.GetComponent<WorldItem>();

                if (item != null)
                {
                    if (item != currentlyLookedAtItem)
                    {
                        ClearCurrentItem();
                        SetCurrentItem(item);
                    }
                }
                else
                {
                    ClearCurrentItem();
                }
            }
            else
            {
                ClearCurrentItem();
            }
        }

        private void SetCurrentItem(WorldItem item)
        {
            currentlyLookedAtItem = item;

            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(true);
                crosshairUI.SetItem(item);
            }

            if (debugMode)
            {
                Debug.Log($"[PlayerItemInteraction] Looking at: {item.ItemDefinition.ItemName} x{item.Quantity}");
            }
        }

        private void ClearCurrentItem()
        {
            if (currentlyLookedAtItem != null)
            {
                currentlyLookedAtItem = null;
            }

            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Input Handlers

        private void OnPickupPressed(InputAction.CallbackContext context)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerItemInteraction] OnPickupPressed called. Looking at item: {(currentlyLookedAtItem != null ? currentlyLookedAtItem.ItemDefinition.ItemName : "null")}");
            }

            if (currentlyLookedAtItem == null) return;

            TryPickupItem(currentlyLookedAtItem);
        }

        private void OnContextMenuPressed(InputAction.CallbackContext context)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerItemInteraction] OnContextMenuPressed called. Looking at item: {(currentlyLookedAtItem != null ? currentlyLookedAtItem.ItemDefinition.ItemName : "null")}");
            }

            if (currentlyLookedAtItem == null) return;

            ShowWorldItemContextMenu(currentlyLookedAtItem);
        }

        private void ShowWorldItemContextMenu(WorldItem worldItem)
        {
            // Build context menu options for world item
            var options = new System.Collections.Generic.List<ContextMenuOption>();

            // Pick Up option
            options.Add(new ContextMenuOption(
                "Pick Up",
                () => TryPickupItem(worldItem)
            ));

            // Inspect option
            options.Add(new ContextMenuOption(
                "Inspect",
                () => Debug.Log($"[WorldItem] Inspecting: {worldItem.ItemDefinition.ItemName}")
            ));

            // Show menu at screen center (no cursor in world mode)
            InventoryContextMenu.ShowMenuInWorld(Input.mousePosition, options);
        }

        #endregion

        #region Pickup Logic

        private void TryPickupItem(WorldItem worldItem)
        {
            if (worldItem == null || inventoryManager == null)
            {
                Debug.LogWarning("[PlayerItemInteraction] Cannot pickup - missing references!");
                return;
            }

            // Close context menu if it's open
            InventoryContextMenu.HideMenu();

            InventoryItemSO itemDef = worldItem.ItemDefinition;
            int quantity = worldItem.Quantity;
            ItemInstance instance = worldItem.ItemInstance;
            ContainerItemData containerData = worldItem.ContainerData;

            bool success = TryAddToAnyInventoryGrid(itemDef, quantity, instance, containerData);

            if (success)
            {
                if (debugMode)
                {
                    Debug.Log($"[PlayerItemInteraction] Picked up: {itemDef.ItemName} x{quantity}");
                }

                worldItem.OnPickedUp();
                ClearCurrentItem();
            }
            else
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[PlayerItemInteraction] Inventory full! Cannot pickup {itemDef.ItemName}");
                }
            }
        }

        private bool TryAddToAnyInventoryGrid(InventoryItemSO item, int quantity, ItemInstance instance, ContainerItemData containerData = null)
        {
            if (inventoryManager == null) return false;

            var allGrids = inventoryManager.GetAllGrids();

            foreach (var grid in allGrids)
            {
                if (grid == null) continue;

                // For items with instances, try to merge with existing stacks first
                if (instance != null && item.IsStackable && item.TrackIndividualItems)
                {
                    // Look for existing stacks we can merge into
                    foreach (GridPlacement.PlacedItem existingItem in grid.InventorySystem.GetAllItems())
                    {
                        if (existingItem.ItemDefinition == item && existingItem.StackCount < item.MaxStackSize)
                        {
                            // Found a stack with space - add our instance directly
                            existingItem.AddInstances(new System.Collections.Generic.List<ItemInstance> { instance });
                            grid.RefreshAllItemVisuals();
                            return true;
                        }
                    }
                }

                // No existing stack to merge into, or item doesn't have instance - create new stack
                Vector2Int? position = FindFirstAvailablePosition(grid, item);

                if (position.HasValue)
                {
                    // When we have an instance to provide, pass stackCount=0 to prevent
                    // InitializeStack from creating a pristine instance. We'll add the real instance after.
                    int stackCountToCreate = (instance != null) ? 0 : quantity;

                    bool success = grid.InventorySystem.TryAddItem(
                        item,
                        position.Value,
                        GridPlacement.GridDirection.Down,
                        out GridPlacement.PlacedItem placedItem,
                        stackCountToCreate,
                        allowAutoStack: false // Don't auto-stack - we already tried merging above
                    );

                    if (success)
                    {
                        // Add the actual instance from the world item
                        if (instance != null && placedItem != null)
                        {
                            placedItem.AddInstances(new System.Collections.Generic.List<ItemInstance> { instance });
                        }

                        // Restore container inventory data if this is a container item
                        if (containerData != null && item.ProvidesStorage && placedItem != null)
                        {
                            // Create inventory system for this container
                            InventorySystem containerInv = new InventorySystem(
                                item.StorageGridSize.x,
                                item.StorageGridSize.y,
                                10f, // Cell size (doesn't matter for data storage)
                                Vector3.zero, // Origin (doesn't matter for data storage)
                                item.StorageMaxWeight
                            );

                            // Load saved items into container
                            containerData.LoadIntoInventorySystem(containerInv, 0);

                            // Attach container inventory to the placed item
                            placedItem.ContainerInventory = containerInv;
                        }

                        grid.RefreshAllItemVisuals();
                        return true;
                    }
                }
            }

            return false;
        }

        private Vector2Int? FindFirstAvailablePosition(InventoryGridVisual grid, InventoryItemSO item)
        {
            if (grid == null || item == null) return null;

            int width = grid.InventorySystem.Width;
            int height = grid.InventorySystem.Height;
            GridPlacement.GridDirection rotation = GridPlacement.GridDirection.Down;

            int itemWidth = item.GetRotatedWidth(rotation);
            int itemHeight = item.GetRotatedHeight(rotation);

            for (int y = 0; y <= height - itemHeight; y++)
            {
                for (int x = 0; x <= width - itemWidth; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    if (grid.InventorySystem.CanAddItem(item, pos, rotation))
                    {
                        return pos;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Public API

        public System.Collections.Generic.IEnumerable<InventoryGridVisual> GetAllGrids()
        {
            if (inventoryManager != null)
            {
                return inventoryManager.GetAllGrids();
            }
            return new InventoryGridVisual[0];
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugMode || raycastOrigin == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(raycastOrigin.position, raycastOrigin.forward * interactionRange);
        }

        #endregion
    }
}
