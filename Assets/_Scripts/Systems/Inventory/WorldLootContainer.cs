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
    /// - Loot population and UI opening are intentionally separated:
    ///     EnsureLootPopulated() — rolls the loot table exactly once, safe to call anywhere.
    ///     Open()               — UI concern only; calls EnsureLootPopulated() then shows the panel.
    ///     GetLootContainer()   — data accessor; calls EnsureLootPopulated() then returns the data.
    /// - The InventorySystem inside each compartment is the single source of truth — items
    ///   dragged out by the player are gone; state persists between opens within a session.
    /// - Calls ContainerInteractionManager.Instance.OpenContainer / CloseContainer.
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
        private bool lootPopulated = false;
        private bool isCurrentlyOpen = false;

        // Properties read by PlayerItemInteraction
        public string DisplayName    => displayName;
        public float  InteractionRange => interactionRange;
        public bool   IsCurrentlyOpen  => isCurrentlyOpen;

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
                    Label           = def.label,
                    GridSize        = new Vector2Int(def.gridWidth, def.gridHeight),
                    MaxWeight       = def.maxWeight,
                    InventorySystem = new InventorySystem(def.gridWidth, def.gridHeight, 64f, Vector3.zero, def.maxWeight)
                });
            }

            lootContainer = LootContainer.CreateMultiCompartment(displayName, compartmentList);

            if (debugMode)
                Debug.Log($"[WorldLootContainer] Built LootContainer '{displayName}' with {compartmentList.Count} compartment(s)");
        }

        #endregion

        #region Loot Population

        /// <summary>
        /// Rolls the loot table into the compartments exactly once.
        /// Safe to call from anywhere — server, client, or UI code.
        /// Subsequent calls are no-ops.
        /// </summary>
        public void EnsureLootPopulated()
        {
            if (lootPopulated) return;
            lootPopulated = true;

            if (lootTable == null)
            {
                if (debugMode)
                    Debug.Log($"[WorldLootContainer] No loot table assigned — '{displayName}' starts empty");
                return;
            }

            lootTable.Populate(lootContainer.GetCompartments(), debugMode);

            if (debugMode)
                Debug.Log($"[WorldLootContainer] Loot populated for '{displayName}'");
        }

        #endregion

        #region Data Accessor

        /// <summary>
        /// Returns the internal LootContainer, ensuring loot has been rolled first.
        /// Use this whenever you need the data without opening the UI —
        /// for example, the server calling this on OnStartServer to build its manifest.
        /// </summary>
        public LootContainer GetLootContainer()
        {
            EnsureLootPopulated();
            return lootContainer;
        }

        #endregion

        #region Open / Close

        /// <summary>
        /// Called by PlayerItemInteraction (solo path) when the player presses interact.
        /// Ensures loot is populated, then shows the compartments in the left UI panel.
        /// The caller is responsible for opening the inventory UI on the correct local player.
        ///
        /// In multiplayer, PlayerItemInteraction sends SvrRequestContainerOpen instead and
        /// this method is not called — the client populates its UI from the server snapshot.
        /// </summary>
        public void Open()
        {
            if (isCurrentlyOpen) return;

            EnsureLootPopulated();

            isCurrentlyOpen = true;

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
