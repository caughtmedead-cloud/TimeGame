using UnityEngine;
using System.Collections.Generic;
using TimeGame.Systems.Inventory.UI;

namespace TimeGame.Systems.Inventory
{
    /// <summary>
    /// Represents a loot container in the 3D world (chest, locker, corpse, crate, etc.)
    ///
    /// Design:
    /// - Owns a LootContainer data object for the lifetime of this GameObject.
    /// - PlayerItemInteraction detects this component via raycast and drives open/close.
    /// - First open populates compartments from the assigned LootTable (or Inspector items).
    /// - The InventorySystem inside each compartment is the single source of truth — items
    ///   dragged out by the player are gone; state persists between opens within a session.
    /// - Calls ContainerInteractionManager.Instance.OpenContainer / CloseContainer.
    /// - Calls InventoryUIController.Open() to force the inventory screen up.
    /// </summary>
    public class WorldLootContainer : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Name shown in the crosshair and left panel header when the player looks at this container")]
        [SerializeField] private string displayName = "Container";

        [Tooltip("Interaction range in meters — should match PlayerItemInteraction.interactionRange")]
        [SerializeField] private float interactionRange = 3f;

        [Header("Compartments")]
        [Tooltip("Define one entry per compartment (drawer, shelf, section, etc.)")]
        [SerializeField] private List<CompartmentDefinition> compartments = new List<CompartmentDefinition>
        {
            new CompartmentDefinition { label = "Main", gridWidth = 5, gridHeight = 5 }
        };

        [Header("Loot Population")]
        [Tooltip("Optional: ScriptableObject loot table. Rolled on first open. Leave null for empty or manually pre-filled containers.")]
        [SerializeField] private LootTable lootTable;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // Runtime state
        private LootContainer lootContainer;
        private bool hasBeenOpened = false;
        private bool isCurrentlyOpen = false;

        // Properties read by PlayerItemInteraction
        public string DisplayName => displayName;
        public float InteractionRange => interactionRange;
        public bool IsCurrentlyOpen => isCurrentlyOpen;

        #region Unity Lifecycle

        private void Awake()
        {
            // Build the LootContainer data object from Inspector definitions.
            // This happens once — the InventorySystem inside each compartment persists
            // for the lifetime of this GameObject.
            BuildLootContainer();
        }

        #endregion

        #region Container Construction

        private void BuildLootContainer()
        {
            var compartmentList = new List<ContainerCompartment>();

            foreach (var def in compartments)
            {
                compartmentList.Add(new ContainerCompartment
                {
                    Label      = def.label,
                    GridSize   = new Vector2Int(def.gridWidth, def.gridHeight),
                    MaxWeight  = def.maxWeight,
                    // InventorySystem is null here — SwapInventorySystem in ContainerInteractionManager
                    // will create one if needed, or we create it explicitly on first open below.
                    InventorySystem = new InventorySystem(def.gridWidth, def.gridHeight, 64f, Vector3.zero, def.maxWeight)
                });
            }

            lootContainer = LootContainer.CreateMultiCompartment(displayName, compartmentList);

            if (debugMode)
                Debug.Log($"[WorldLootContainer] Built LootContainer '{displayName}' with {compartmentList.Count} compartment(s)");
        }

        #endregion

        #region Open / Close

        /// <summary>
        /// Called by PlayerItemInteraction when the player presses the interact key.
        /// Populates loot on first open, then shows compartments in the left panel.
        /// The caller (PlayerItemInteraction) is responsible for opening the inventory UI
        /// on the correct local player — WorldLootContainer has no knowledge of UI ownership.
        /// </summary>
        public void Open()
        {
            if (isCurrentlyOpen) return;

            // First open: roll loot table if assigned and not yet populated
            if (!hasBeenOpened)
            {
                PopulateLoot();
                hasBeenOpened = true;
            }

            isCurrentlyOpen = true;

            // Show loot in left panel — pass 'this' so the manager can call Close() when the panel closes
            if (ContainerInteractionManager.Instance != null)
                ContainerInteractionManager.Instance.OpenContainer(lootContainer, this);
            else
                Debug.LogError("[WorldLootContainer] ContainerInteractionManager.Instance is null — is it in the scene?");

            if (debugMode)
                Debug.Log($"[WorldLootContainer] Opened '{displayName}'");
        }

        /// <summary>
        /// Resets this container's open state.
        /// Called by ContainerInteractionManager when the left panel closes (Tab/ESC/open new container).
        /// Does NOT call back into ContainerInteractionManager — the manager is already closing.
        /// </summary>
        public void Close()
        {
            if (!isCurrentlyOpen) return;
            isCurrentlyOpen = false;

            if (debugMode)
                Debug.Log($"[WorldLootContainer] Closed '{displayName}'");
        }

        #endregion

        #region Loot Population

        private void PopulateLoot()
        {
            if (lootTable == null)
            {
                if (debugMode)
                    Debug.Log($"[WorldLootContainer] No loot table assigned — '{displayName}' starts empty");
                return;
            }

            List<ContainerCompartment> compartmentList = lootContainer.GetCompartments();
            lootTable.Populate(compartmentList, debugMode);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }

        #endregion

        /// <summary>
        /// Inspector-friendly compartment definition.
        /// Serialized separately from ContainerCompartment so Inspector layout is clean.
        /// </summary>
        [System.Serializable]
        public class CompartmentDefinition
        {
            [Tooltip("Label shown above this compartment in the left panel")]
            public string label = "Compartment";

            [Tooltip("Grid width in cells")]
            public int gridWidth = 5;

            [Tooltip("Grid height in cells")]
            public int gridHeight = 5;

            [Tooltip("Max weight capacity (0 = unlimited)")]
            public float maxWeight = 0f;
        }
    }
}
