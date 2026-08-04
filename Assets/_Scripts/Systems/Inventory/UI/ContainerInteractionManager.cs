using System;
using System.Collections.Generic;
using UnityEngine;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages the left panel loot container grids.
    /// Handles opening/closing containers and spawning their inventory grids.
    /// </summary>
    public class ContainerInteractionManager : MonoBehaviour
    {
        // Singleton — O(1) access, avoids FindObjectOfType at runtime
        public static ContainerInteractionManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private ScrollableInventoryPanel containerPanel;
        [SerializeField] private GameObject leftPanelRoot;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // Current open container
        private LootContainer currentContainer;
        private object currentContainerSource; // The world object that owns currentContainer (WorldLootContainer or WorldContainer)
        private List<InventoryGridVisual> currentContainerGrids = new List<InventoryGridVisual>();

        /// <summary>
        /// ObjectId of the world container that supplied the current container data.
        /// -1 when the container was opened locally (default singleplayer path) or is closed.
        /// Set by OpenContainer(container, source, containerNetId).
        /// </summary>
        private int _currentContainerNetId = -1;

        /// <summary>
        /// Is a container currently open?
        /// </summary>
        public bool IsContainerOpen => currentContainer != null;

        /// <summary>
        /// Get the currently open container
        /// </summary>
        public LootContainer CurrentContainer => currentContainer;

        /// <summary>
        /// ObjectId of the currently open container, or -1 when no container with a tracked ID is
        /// open (container closed, or the 2-arg OpenContainer was used).
        /// </summary>
        public int CurrentContainerNetId => _currentContainerNetId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ContainerInteractionManager] Duplicate instance detected — destroying self.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Start with left panel hidden
            if (leftPanelRoot != null)
            {
                leftPanelRoot.SetActive(false);
            }

            Log("ContainerInteractionManager initialized");
        }

        /// <summary>
        /// Open a loot container and spawn its grids.
        /// Pass the source object (WorldLootContainer or WorldContainer) so it can be notified when the panel closes.
        /// </summary>
        public void OpenContainer(LootContainer container, object source = null)
        {
            if (container == null)
            {
                Debug.LogError("[ContainerInteractionManager] Cannot open null container");
                return;
            }

            // Close any existing container first
            if (IsContainerOpen)
            {
                CloseContainer();
            }

            currentContainer      = container;
            currentContainerSource = source;

            // Show left panel
            if (leftPanelRoot != null)
                leftPanelRoot.SetActive(true);

            // Spawn grids for each compartment in the container
            SpawnContainerGrids(container);

            Log($"Opened container: {container.ContainerName}");
        }

        /// <summary>
        /// Open a container, tracking a source-specific ID (e.g. for future save/persistence
        /// systems) so other systems can reference it when items are dragged out.
        ///
        /// IMPORTANT: _currentContainerNetId must be set AFTER OpenContainer(container, source)
        /// because that inner call may invoke CloseContainer() first (if a container was already
        /// open), which resets _currentContainerNetId to -1.  Setting it afterwards guarantees
        /// TryGetContainerContext() sees the correct ID during the subsequent drag session.
        /// </summary>
        public void OpenContainer(LootContainer container, object source, int containerNetId)
        {
            OpenContainer(container, source);        // may call CloseContainer() → resets to -1, that's fine
            _currentContainerNetId = containerNetId; // set AFTER so the reset above doesn't wipe it
        }

        /// <summary>
        /// If the given grid is one of the currently open container's compartment grids,
        /// returns true and outputs the container's tracked ID and compartment index.
        /// </summary>
        public bool TryGetContainerContext(
            InventoryGridVisual grid,
            out int             containerNetId,
            out int             compartmentIndex)
        {
            containerNetId    = -1;
            compartmentIndex  = -1;

            if (grid == null || _currentContainerNetId < 0) return false;

            for (int i = 0; i < currentContainerGrids.Count; i++)
            {
                if (currentContainerGrids[i] == grid)
                {
                    containerNetId   = _currentContainerNetId;
                    compartmentIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Refresh only the visual of a specific container compartment grid without
        /// closing and reopening the whole container.
        /// </summary>
        public void RefreshCompartmentGrid(int compartmentIndex)
        {
            if (compartmentIndex < 0 || compartmentIndex >= currentContainerGrids.Count) return;
            currentContainerGrids[compartmentIndex]?.RefreshAllItemVisuals();
        }

        /// <summary>
        /// Add an item to a specific container compartment grid.
        /// If the item is already present in the compartment's InventorySystem, this is a
        /// no-op except for refreshing the visual so the grid stays in sync.
        /// </summary>
        public void AddItemToCompartmentGrid(
            int             compartmentIndex,
            Guid            instanceId,
            InventoryItemSO itemDef,
            Vector2Int      gridPos,
            GridDirection   rotation,
            int             stackCount)
        {
            if (compartmentIndex < 0 || compartmentIndex >= currentContainerGrids.Count) return;

            InventoryGridVisual grid = currentContainerGrids[compartmentIndex];
            if (grid?.InventorySystem == null) return;

            // Speculative path: placing client already has the item here from the drag.
            // Skip the TryAddItem call (would fail on duplicate ID) and just refresh.
            if (grid.InventorySystem.GetItemByID(instanceId) != null)
            {
                grid.RefreshAllItemVisuals();
                return;
            }

            // Other clients: item is not yet in their local InventorySystem — add it now.
            grid.InventorySystem.TryAddItem(instanceId, itemDef, gridPos, rotation, out _, stackCount);
            grid.RefreshAllItemVisuals();

            Log($"AddItemToCompartmentGrid — '{itemDef.ItemName}' " +
                $"id={instanceId.ToString("N").Substring(0, 8)} at {gridPos} compartment {compartmentIndex}.");
        }

        /// <summary>
        /// Close the currently open container and notify the source object.
        /// </summary>
        public void CloseContainer()
        {
            if (!IsContainerOpen)
            {
                Log("No container to close");
                return;
            }

            _currentContainerNetId = -1;

            // Clear all spawned grids
            if (containerPanel != null)
                containerPanel.ClearAllGrids();

            currentContainerGrids.Clear();
            currentContainer = null;

            // Notify the world object so it resets isCurrentlyOpen and saves data
            // Use dynamic invocation to call Close() on either WorldLootContainer or WorldContainer
            object source = currentContainerSource;
            currentContainerSource = null;

            if (source != null)
            {
                // Try to call Close() via reflection - works for both WorldLootContainer and WorldContainer
                var closeMethod = source.GetType().GetMethod("Close", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                closeMethod?.Invoke(source, null);
            }

            // Hide left panel
            if (leftPanelRoot != null)
                leftPanelRoot.SetActive(false);

            Log("Closed container");
        }

        private void SpawnContainerGrids(LootContainer container)
        {
            if (containerPanel == null)
            {
                Debug.LogError("[ContainerInteractionManager] No container panel assigned!");
                return;
            }

            currentContainerGrids.Clear();

            // Get compartments from container
            List<ContainerCompartment> compartments = container.GetCompartments();

            for (int i = 0; i < compartments.Count; i++)
            {
                ContainerCompartment compartment = compartments[i];

                string gridName = $"{container.ContainerName}_Compartment_{i}";
                string labelText = string.IsNullOrEmpty(compartment.Label) 
                    ? $"Compartment {i + 1}" 
                    : compartment.Label;

                InventoryGridVisual grid = containerPanel.SpawnGrid(
                    gridName,
                    compartment.GridSize.x,
                    compartment.GridSize.y,
                    compartment.MaxWeight,
                    null,
                    withLabel: true,
                    labelText: labelText
                );

                if (grid != null)
                {
                    // ITEM LINEAGE: Swap the grid's InventorySystem to the compartment's existing one directly.
                    // Never copy items between systems — that would duplicate them if a floating window is open.
                    // Items always live in exactly one InventorySystem object.
                    if (compartment.InventorySystem != null)
                    {
                        grid.SwapInventorySystem(compartment.InventorySystem);
                        grid.RefreshAllItemVisuals();

                        int itemCount = compartment.InventorySystem.GetAllItems().Count;
                        Log($"Restored {itemCount} items to {gridName} via system swap");
                    }

                    currentContainerGrids.Add(grid);
                    Log($"Spawned container grid: {gridName} ({compartment.GridSize.x}x{compartment.GridSize.y})");
                }
            }

            // Scroll to top
            containerPanel.ScrollToTop();
        }

        private void Log(string message)
        {
            if (verboseLogging)
            {
                Debug.Log($"[ContainerInteractionManager] {message}");
            }
        }
    }

    /// <summary>
    /// Represents a loot container (chest, cabinet, corpse, etc.)
    /// Contains multiple compartments (drawers, sections, pockets)
    /// </summary>
    [System.Serializable]
    public class LootContainer
    {
        [SerializeField] private string containerName = "Container";
        [SerializeField] private List<ContainerCompartment> compartments = new List<ContainerCompartment>();

        public string ContainerName => containerName;

        public List<ContainerCompartment> GetCompartments() => compartments;

        /// <summary>
        /// Add a compartment to this container
        /// </summary>
        public void AddCompartment(ContainerCompartment compartment)
        {
            if (compartment != null)
            {
                compartments.Add(compartment);
            }
        }

        /// <summary>
        /// Create a simple container with one compartment
        /// </summary>
        public static LootContainer CreateSimple(string name, int width, int height, float maxWeight = 0f)
        {
            LootContainer container = new LootContainer();
            container.containerName = name;
            container.compartments.Add(new ContainerCompartment
            {
                Label = name,
                GridSize = new Vector2Int(width, height),
                MaxWeight = maxWeight
            });
            return container;
        }

        /// <summary>
        /// Create a multi-compartment container (like a dresser with drawers)
        /// </summary>
        public static LootContainer CreateMultiCompartment(string name, List<ContainerCompartment> compartments)
        {
            LootContainer container = new LootContainer();
            container.containerName = name;
            container.compartments = compartments;
            return container;
        }
    }

    /// <summary>
    /// Represents one compartment/section of a loot container
    /// </summary>
    [System.Serializable]
    public class ContainerCompartment
    {
        [Tooltip("Display label for this compartment")]
        public string Label = "Compartment";

        [Tooltip("Grid dimensions")]
        public Vector2Int GridSize = new Vector2Int(5, 5);

        [Tooltip("Max weight capacity (0 = unlimited)")]
        public float MaxWeight = 0f;

        [Tooltip("Pre-existing inventory system (if loading from save)")]
        [System.NonSerialized]
        public InventorySystem InventorySystem;
    }
}
