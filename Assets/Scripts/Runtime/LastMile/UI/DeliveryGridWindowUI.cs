using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TimeGame.Inventory;
using TimeGame.Systems.Inventory;
using TimeGame.Systems.Inventory.UI;
using TimeGame.Systems.GridPlacement;

namespace LastMile.UI
{
    /// <summary>
    /// Opens the delivery scratch grid through the project's real world-loot-container
    /// pipeline (<see cref="ContainerInteractionManager"/> + <see cref="InventoryUIController"/>)
    /// so pressing F to deliver looks and behaves exactly like opening a chest: pockets/equipment
    /// come up on the right, the scratch grid appears in the same left loot panel.
    ///
    /// Delivery only commits when the player clicks Confirm — closing the inventory
    /// (Tab/ESC, or opening another container) without confirming is a safe cancel: whatever
    /// is still sitting in the scratch grid is simply handed back untouched, nothing is
    /// delivered. This mirrors backing out of a trade rather than auto-committing on close.
    /// </summary>
    public class DeliveryGridWindowUI : MonoBehaviour
    {
        // Tag used project-wide to locate the player GameObject (see ShiftEndStateUI).
        private const string PlayerTag = "Player";

        // Cosmetic-only cell size for the scratch InventorySystem's internal math —
        // the actual on-screen cell size is driven by InventoryGridFactory.defaultCellSize.
        private const float DeliveryGridCellSize = 64f;

        // How long an NPC's reaction line stays on screen after the delivery box closes.
        private const float ReactionDisplaySeconds = 4f;

        [Header("Grid Setup")]
        [SerializeField] private int gridWidth = 6;
        [SerializeField] private int gridHeight = 6;
        [SerializeField] private float maxWeight = 0f;

        [Header("Player")]
        [Tooltip("Auto-resolved from the \"Player\"-tagged GameObject if left unassigned. Opening the delivery window opens this exactly like opening a loot container, so the player's pockets/equipment are visible to drag from.")]
        [SerializeField] private InventoryUIController inventoryUIController;

        [Header("Confirm")]
        [Tooltip("Shown only while the delivery window is open. Commits whatever cargo is currently in the scratch grid.")]
        [SerializeField] private Button confirmButton;

        [Header("Reactions")]
        [SerializeField] private DeliveryReactionSO defaultReactions;
        [Tooltip("Transient text shown after the delivery box closes to display the recipient's reaction line.")]
        [SerializeField] private TextMeshProUGUI reactionText;

        private InventorySystem deliveryInventory;
        private DeliveryStop currentStop;
        private Coroutine reactionCoroutine;

        public static DeliveryGridWindowUI Instance { get; private set; }

        /// <summary>True while the delivery grid window is currently open.</summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DeliveryGridWindowUI] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (reactionText != null)
            {
                reactionText.gameObject.SetActive(false);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(false);
            }
        }

        private void ResolvePlayerReferences()
        {
            if (inventoryUIController != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player != null)
            {
                inventoryUIController = player.GetComponent<InventoryUIController>();
            }
        }

        /// <summary>
        /// Opens the delivery window for the given stop, spawning a fresh scratch delivery
        /// compartment through the same pipeline a real world loot container uses.
        /// </summary>
        public void Open(DeliveryStop stop)
        {
            if (IsOpen || stop == null || stop.IsFullyDelivered)
            {
                return;
            }

            ResolvePlayerReferences();

            if (inventoryUIController == null)
            {
                Debug.LogError("[DeliveryGridWindowUI] No InventoryUIController resolved — cannot open the delivery window.");
                return;
            }

            if (ContainerInteractionManager.Instance == null)
            {
                Debug.LogError("[DeliveryGridWindowUI] ContainerInteractionManager.Instance is null — is it in the scene?");
                return;
            }

            currentStop = stop;
            deliveryInventory = new InventorySystem(gridWidth, gridHeight, DeliveryGridCellSize, Vector3.zero, maxWeight);

            string itemName = stop.Definition != null && stop.Definition.requiredItem != null
                ? stop.Definition.requiredItem.ItemName
                : "unknown item";
            string recipientName = stop.Definition != null ? stop.Definition.recipientName : "Recipient";
            string label = $"Deliver {stop.RemainingQuantity}x {itemName} to {recipientName}";

            ContainerCompartment compartment = new ContainerCompartment
            {
                Label = label,
                GridSize = new Vector2Int(gridWidth, gridHeight),
                MaxWeight = maxWeight,
                InventorySystem = deliveryInventory
            };

            LootContainer deliveryLootContainer = LootContainer.CreateMultiCompartment(
                recipientName,
                new List<ContainerCompartment> { compartment });

            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
                reactionCoroutine = null;
            }

            if (reactionText != null)
            {
                reactionText.gameObject.SetActive(false);
            }

            // Mirrors PlayerItemInteraction.TryOpenContainer exactly: force the inventory
            // screen up first (pockets/equipment become visible), then populate the left
            // loot panel — so the delivery box looks and behaves identically to a real chest.
            inventoryUIController.Open();
            ContainerInteractionManager.Instance.OpenContainer(deliveryLootContainer, this);

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
                confirmButton.gameObject.SetActive(true);
            }

            IsOpen = true;
        }

        /// <summary>
        /// Commits whatever cargo is currently in the scratch grid. Consumes matching stock up
        /// to the stop's remaining requirement, returns everything else to the player, and shows
        /// the recipient's reaction. Safe to click more than once per visit — e.g. to top up a
        /// partial delivery after dragging in more cargo.
        /// </summary>
        private void OnConfirmClicked()
        {
            if (currentStop == null || deliveryInventory == null)
            {
                return;
            }

            DeliveryStop.DeliveryOutcome outcome = currentStop.ResolveDelivery(deliveryInventory, out List<PlacedItem> itemsToReturn);
            ReturnItems(itemsToReturn);
            ShowReaction(outcome);

            // ResolveDelivery empties the scratch grid's InventorySystem directly — refresh
            // the visible grid so consumed/returned items disappear immediately.
            ContainerInteractionManager.Instance?.RefreshCompartmentGrid(0);

            Debug.Log($"[DeliveryGridWindowUI] Confirmed ResolveDelivery({currentStop.StopId}) => {outcome}");

            // Fully satisfied — close the box for the player, nothing left to do here.
            if (currentStop.IsFullyDelivered)
            {
                ContainerInteractionManager.Instance?.CloseContainer();
            }
        }

        // Returns every leftover/wrong item back to the player's inventory, wherever it
        // fits across their currently-open top-level grids — mirrors the project's
        // existing pickup-return semantics. Preserves per-instance identity/durability
        // and nested container contents via ContainerItemData.
        private void ReturnItems(List<PlacedItem> items)
        {
            if (items == null || items.Count == 0 || PlayerInventoryManager.Instance == null)
            {
                return;
            }

            IEnumerable<InventoryGridVisual> playerGrids = PlayerInventoryManager.Instance.GetAllGrids();

            foreach (PlacedItem item in items)
            {
                if (item == null)
                {
                    continue;
                }

                InventoryItemSO itemDef = item.ItemDefinition as InventoryItemSO;
                if (itemDef == null)
                {
                    Debug.LogWarning($"[DeliveryGridWindowUI] Skipped returning item — ItemDefinition '{item.ItemDefinition}' is not an InventoryItemSO.");
                    continue;
                }

                ContainerItemData containerData = null;
                if (ContainerHelper.IsContainer(item))
                {
                    containerData = ContainerItemData.FromInventorySystem(item.ContainerInventory);
                }

                bool allPlaced = true;

                if (item.IsFullyTracked)
                {
                    foreach (ItemInstance instance in item.ItemInstances)
                    {
                        bool placed = InventoryPlacementHelper.TryAddToAnyGrid(playerGrids, itemDef, 1, instance, containerData);
                        allPlaced &= placed;
                    }
                }
                else
                {
                    allPlaced = InventoryPlacementHelper.TryAddToAnyGrid(playerGrids, itemDef, item.StackCount, null, containerData);
                }

                if (!allPlaced)
                {
                    Debug.LogWarning($"[DeliveryGridWindowUI] Could not return all of '{itemDef.ItemName}' to the player's inventory — grids are full.");
                }
            }
        }

        private void ShowReaction(DeliveryStop.DeliveryOutcome outcome)
        {
            if (reactionText == null || defaultReactions == null)
            {
                return;
            }

            string line = defaultReactions.GetReactionLine(outcome);
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            reactionText.text = line;
            reactionText.gameObject.SetActive(true);

            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
            }
            reactionCoroutine = StartCoroutine(HideReactionAfterDelay());
        }

        private IEnumerator HideReactionAfterDelay()
        {
            yield return new WaitForSeconds(ReactionDisplaySeconds);

            if (reactionText != null)
            {
                reactionText.gameObject.SetActive(false);
            }
            reactionCoroutine = null;
        }

        /// <summary>
        /// Closes the delivery window. Called automatically by
        /// <see cref="ContainerInteractionManager"/> — via the same source.Close() reflection
        /// call it uses for WorldLootContainer — whenever the player closes the inventory
        /// (Tab/ESC) or opens a different container. Anything still sitting in the scratch
        /// grid at this point was never confirmed, so it's handed back to the player untouched;
        /// only Confirm commits a delivery.
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            InventorySystem inventory = deliveryInventory;

            currentStop = null;
            deliveryInventory = null;
            IsOpen = false;

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.gameObject.SetActive(false);
            }

            if (inventory == null)
            {
                return;
            }

            List<PlacedItem> uncommittedItems = new List<PlacedItem>(inventory.GetAllItems());
            ReturnItems(uncommittedItems);
        }
    }
}
