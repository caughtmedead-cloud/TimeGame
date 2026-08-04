using UnityEngine;
using UnityEngine.InputSystem;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;
using TimeGame.Inventory;

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

        [Tooltip("Inventory UI controller — used to force-open inventory when a loot container is opened")]
        [SerializeField] private InventoryUIController inventoryUIController;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private WorldItem currentlyLookedAtItem;
        private WorldLootContainer currentlyLookedAtContainer;
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
            // Auto-find InventoryUIController by walking up this player's own hierarchy only.
            // Never use FindObjectOfType — in multiplayer each player has their own controller
            // and a global search would grab a random player's instance.
            if (inventoryUIController == null)
            {
                inventoryUIController = GetComponentInParent<InventoryUIController>(true);
                if (inventoryUIController == null)
                    Debug.LogWarning("[PlayerItemInteraction] InventoryUIController not found in parent hierarchy — assign it in the Inspector on the player prefab.");
            }

            if (debugMode)
            {
                Debug.Log("[PlayerItemInteraction] === INITIALIZATION CHECK ===");
                Debug.Log($"  Raycast Origin: {(raycastOrigin != null ? raycastOrigin.name : "NULL")}");
                Debug.Log($"  Interaction Range: {interactionRange}");
                Debug.Log($"  Item Layer Mask: {itemLayerMask.value}");
                Debug.Log($"  Inventory Manager: {(inventoryManager != null ? "Assigned" : "NULL - Pickup will fail!")}");
                Debug.Log($"  InventoryUIController: {(inventoryUIController != null ? "Assigned" : "NULL - Container open will not force inventory")}");
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

            Vector3 origin    = raycastOrigin.position;
            Vector3 direction = raycastOrigin.forward;

            RaycastHit hit;

            if (debugMode)
                Debug.DrawRay(origin, direction * interactionRange, Color.cyan);

            // Use scene-aware raycast if available, otherwise fall back to Physics.Raycast
            bool didHit;
            if (scenePhysics != null)
                didHit = scenePhysics.Raycast(origin, direction, out hit, interactionRange, itemLayerMask, QueryTriggerInteraction.Ignore);
            else
                didHit = Physics.Raycast(origin, direction, out hit, interactionRange, itemLayerMask, QueryTriggerInteraction.Ignore);

            if (didHit)
            {
                // Use GetComponentInParent so colliders on child mesh objects still find the
                // component on the root — common with prefabs where the mesh/collider is a child.

                // Priority 1: WorldItem (loose pickup)
                WorldItem item = hit.collider.GetComponentInParent<WorldItem>();
                if (item != null)
                {
                    if (item != currentlyLookedAtItem)
                    {
                        ClearCurrentTarget();
                        SetCurrentItem(item);
                    }
                    return;
                }

                // Priority 2: WorldLootContainer
                WorldLootContainer container = hit.collider.GetComponentInParent<WorldLootContainer>();
                if (container != null)
                {
                    if (container != currentlyLookedAtContainer)
                    {
                        ClearCurrentTarget();
                        SetCurrentContainer(container);
                    }
                    return;
                }

                // Hit something on the layer but it has neither component
                ClearCurrentTarget();
            }
            else
            {
                ClearCurrentTarget();
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
                Debug.Log($"[PlayerItemInteraction] Looking at item: {item.ItemDefinition.ItemName} x{item.Quantity}");
        }

        private void SetCurrentContainer(WorldLootContainer container)
        {
            currentlyLookedAtContainer = container;

            if (crosshairUI != null)
            {
                crosshairUI.gameObject.SetActive(true);
                crosshairUI.SetLootContainer(container);
            }

            if (debugMode)
                Debug.Log($"[PlayerItemInteraction] Looking at container: {container.DisplayName}");
        }

        /// <summary>
        /// Clears whichever target (item or container) the player was looking at.
        /// Hides the crosshair and closes the world context menu if open.
        /// </summary>
        private void ClearCurrentTarget()
        {
            bool hadTarget = currentlyLookedAtItem != null || currentlyLookedAtContainer != null;

            currentlyLookedAtItem      = null;
            currentlyLookedAtContainer = null;

            if (hadTarget)
                InventoryContextMenu.HideMenu();

            if (crosshairUI != null)
                crosshairUI.gameObject.SetActive(false);
        }

        #endregion

        #region Input Handlers

        private void OnPickupPressed(InputAction.CallbackContext context)
        {
            // WorldItem — pick up
            if (currentlyLookedAtItem != null)
            {
                if (debugMode)
                    Debug.Log($"[PlayerItemInteraction] Interact: picking up {currentlyLookedAtItem.ItemDefinition.ItemName}");
                TryPickupItem(currentlyLookedAtItem);
                return;
            }

            // WorldLootContainer — open
            if (currentlyLookedAtContainer != null)
            {
                if (debugMode)
                    Debug.Log($"[PlayerItemInteraction] Interact: opening container {currentlyLookedAtContainer.DisplayName}");
                TryOpenContainer(currentlyLookedAtContainer);
                return;
            }
        }

        private void OnContextMenuPressed(InputAction.CallbackContext context)
        {
            // WorldItem — full options menu
            if (currentlyLookedAtItem != null)
            {
                if (debugMode)
                    Debug.Log($"[PlayerItemInteraction] Context menu for item: {currentlyLookedAtItem.ItemDefinition.ItemName}");
                ShowWorldItemContextMenu(currentlyLookedAtItem);
                return;
            }

            // WorldLootContainer — future options (Lock, Unlock, Bash, etc.)
            if (currentlyLookedAtContainer != null)
            {
                if (debugMode)
                    Debug.Log($"[PlayerItemInteraction] Context menu for container: {currentlyLookedAtContainer.DisplayName}");
                ShowWorldContainerContextMenu(currentlyLookedAtContainer);
                return;
            }
        }

        private void ShowWorldItemContextMenu(WorldItem worldItem)
        {
            // Build context menu options for world item
            var options = new System.Collections.Generic.List<ContextMenuOption>();

            // Equip option (first) — only shown if item has an equipment type and a compatible empty slot exists
            InventoryItemSO itemDef = worldItem.ItemDefinition;
            if (itemDef != null && itemDef.EquipmentType != ItemType.None)
            {
                EquipmentSlot targetSlot = FindEmptyCompatibleSlot(itemDef);
                if (targetSlot != null)
                {
                    options.Add(new ContextMenuOption(
                        "Equip",
                        () => TryEquipFromWorld(worldItem, targetSlot)
                    ));
                }
            }

            // Open option (second) — for dropped containers (backpacks, bags, etc.)
            WorldContainer worldContainer = worldItem.GetComponent<WorldContainer>();
            if (worldContainer != null)
            {
                options.Add(new ContextMenuOption(
                    "Open",
                    () => OpenWorldContainer(worldContainer)
                ));
            }

            // Pick Up option (third - always available)
            options.Add(new ContextMenuOption(
                "Pick Up",
                () => TryPickupItem(worldItem)
            ));

            // Inspect option (last)
            options.Add(new ContextMenuOption(
                "Inspect",
                () => InspectWorldItem(worldItem)
            ));

            // Show menu at screen center (no cursor in world mode)
            InventoryContextMenu.ShowMenuInWorld(Input.mousePosition, options);
        }

        /// <summary>
        /// Open a world loot container — forces this player's inventory screen up, then populates left panel.
        /// InventoryUIController lives on the player prefab, so this is always the local player's UI.
        /// </summary>
        private void TryOpenContainer(WorldLootContainer container)
        {
            if (container == null) return;

            // Always open the inventory UI immediately
            if (inventoryUIController != null)
                inventoryUIController.Open();

            container.Open();
        }

        /// <summary>
        /// Context menu for world loot containers.
        /// Currently a stub for future options (Lock, Unlock, Bash, Pick Lock, etc.)
        /// </summary>
        private void ShowWorldContainerContextMenu(WorldLootContainer container)
        {
            if (container == null) return;

            var options = new System.Collections.Generic.List<ContextMenuOption>();

            options.Add(new ContextMenuOption(
                "Open",
                () => TryOpenContainer(container)
            ));

            // Future options stubbed here:
            // options.Add(new ContextMenuOption("Lock",      () => { }));
            // options.Add(new ContextMenuOption("Unlock",    () => { }));
            // options.Add(new ContextMenuOption("Bash Open", () => { }));
            // options.Add(new ContextMenuOption("Pick Lock", () => { }));

            InventoryContextMenu.ShowMenuInWorld(Input.mousePosition, options);
        }

        /// <summary>
        /// Find the first unoccupied equipment slot that accepts this item's type.
        /// Mirrors InventoryContextMenu.FindEmptyCompatibleSlot.
        /// </summary>
        private EquipmentSlot FindEmptyCompatibleSlot(InventoryItemSO itemDef)
        {
            // includeInactive: true — inventory panel is hidden while in world, slots are on inactive GameObjects
            EquipmentSlot[] allSlots = FindObjectsOfType<EquipmentSlot>(true);
            foreach (EquipmentSlot slot in allSlots)
            {
                if (!slot.IsOccupied && slot.CanAcceptItem(itemDef, GridDirection.Down, Vector2.zero))
                    return slot;
            }
            return null;
        }

        /// <summary>
        /// Pick up a world item and route it directly into an equipment slot,
        /// preserving ItemInstance and ContainerData lineage.
        /// </summary>
        private void TryEquipFromWorld(WorldItem worldItem, EquipmentSlot targetSlot)
        {
            if (worldItem == null || targetSlot == null) return;

            InventoryContextMenu.HideMenu();

            // Re-validate: slot may have become occupied since menu opened
            if (targetSlot.IsOccupied)
            {
                Debug.LogWarning("[PlayerItemInteraction] Equip slot became occupied — falling back to Pick Up");
                TryPickupItem(worldItem);
                return;
            }

            InventoryItemSO itemDef    = worldItem.ItemDefinition;
            ItemInstance    instance   = worldItem.ItemInstance;
            ContainerItemData containerData = worldItem.ContainerData;

            // Build a PlacedItem that carries the world item's full lineage
            PlacedItem placedItem = new PlacedItem(
                System.Guid.NewGuid(),
                itemDef,
                Vector2Int.zero,
                GridDirection.Down
            );

            // Restore instance tracking (durability, uses, etc.)
            if (instance != null)
                placedItem.AddInstances(new System.Collections.Generic.List<ItemInstance> { instance });

            // Restore container inventory if this item provides storage
            if (containerData != null && itemDef.ProvidesStorage)
            {
                InventorySystem containerInv = new InventorySystem(
                    itemDef.StorageGridSize.x,
                    itemDef.StorageGridSize.y,
                    64f,
                    Vector3.zero,
                    itemDef.StorageMaxWeight
                );
                containerData.LoadIntoInventorySystem(containerInv, 0);
                placedItem.ContainerInventory = containerInv;
            }

            bool equipped = targetSlot.TryPlaceExistingItem(placedItem, GridDirection.Down, Vector2.zero, out _);

            if (equipped)
            {
                Debug.Log($"[PlayerItemInteraction] Equipped {itemDef.ItemName} from world directly into slot");
                worldItem.OnPickedUp();
                ClearCurrentTarget();
            }
            else
            {
                // Slot rejected the item (e.g. wrong type check failed) — fall back to regular pickup
                Debug.LogWarning($"[PlayerItemInteraction] Equip failed for {itemDef.ItemName} — falling back to Pick Up");
                TryPickupItem(worldItem);
            }
        }

        private void OpenWorldContainer(WorldContainer worldContainer)
        {
            if (worldContainer == null) return;

            InventoryContextMenu.HideMenu();

            // Open the container in the left panel
            worldContainer.Open();

            // Force open inventory UI so player can see both panels
            if (inventoryUIController != null)
            {
                inventoryUIController.OpenInventory();
            }
            else
            {
                Debug.LogWarning("[PlayerItemInteraction] InventoryUIController is null - cannot open inventory UI!");
            }

            if (debugMode)
            {
                Debug.Log($"[PlayerItemInteraction] Opened world container: {worldContainer.DisplayName}");
            }
        }

        private void InspectWorldItem(WorldItem worldItem)
        {
            if (worldItem == null || worldItem.ItemDefinition == null) return;

            InventoryContextMenu.HideMenu();

            if (ItemInspectPanel.Instance != null)
            {
                // Show inspect panel with player control management
                ItemInspectPanel.Instance.Show(worldItem.ItemDefinition, null, disablePlayerControls: true);

                if (debugMode)
                {
                    Debug.Log($"[PlayerItemInteraction] Inspecting world item: {worldItem.ItemDefinition.ItemName}");
                }
            }
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
                    Debug.Log($"[PlayerItemInteraction] Picked up: {itemDef.ItemName} x{quantity}");

                worldItem.OnPickedUp();
                ClearCurrentTarget();
            }
            else
            {
                if (debugMode)
                    Debug.LogWarning($"[PlayerItemInteraction] Inventory full! Cannot pickup {itemDef.ItemName}");
            }
        }

        private bool TryAddToAnyInventoryGrid(InventoryItemSO item, int quantity, ItemInstance instance, ContainerItemData containerData = null)
        {
            return InventoryPlacementHelper.TryAddToAnyGrid(
                inventoryManager.GetAllGrids(), item, quantity, instance, containerData);
        }

        private Vector2Int? FindFirstAvailablePosition(InventoryGridVisual grid, InventoryItemSO item, GridPlacement.GridDirection rotation)
        {
            return InventoryPlacementHelper.FindFirstAvailablePosition(grid, item, rotation);
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
