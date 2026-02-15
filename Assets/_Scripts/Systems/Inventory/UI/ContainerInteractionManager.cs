using UnityEngine;
using System.Collections.Generic;

namespace TimeGame.Systems.Inventory.UI
{
    /// <summary>
    /// Manages the left panel loot container grids.
    /// Handles opening/closing containers and spawning their inventory grids.
    /// </summary>
    public class ContainerInteractionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ScrollableInventoryPanel containerPanel;
        [SerializeField] private GameObject leftPanelRoot;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // Current open container
        private LootContainer currentContainer;
        private List<InventoryGridVisual> currentContainerGrids = new List<InventoryGridVisual>();

        /// <summary>
        /// Is a container currently open?
        /// </summary>
        public bool IsContainerOpen => currentContainer != null;

        /// <summary>
        /// Get the currently open container
        /// </summary>
        public LootContainer CurrentContainer => currentContainer;

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
        /// Open a loot container and spawn its grids
        /// </summary>
        public void OpenContainer(LootContainer container)
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

            currentContainer = container;

            // Show left panel
            if (leftPanelRoot != null)
            {
                leftPanelRoot.SetActive(true);
            }

            // Spawn grids for each compartment in the container
            SpawnContainerGrids(container);

            Log($"Opened container: {container.ContainerName}");
        }

        /// <summary>
        /// Close the currently open container
        /// </summary>
        public void CloseContainer()
        {
            if (!IsContainerOpen)
            {
                Log("No container to close");
                return;
            }

            // Clear all spawned grids
            if (containerPanel != null)
            {
                containerPanel.ClearAllGrids();
            }

            currentContainerGrids.Clear();
            currentContainer = null;

            // Hide left panel
            if (leftPanelRoot != null)
            {
                leftPanelRoot.SetActive(false);
            }

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
                    // Initialize with existing items if container has saved data
                    if (compartment.InventorySystem != null)
                    {
                        // Grid already has its own InventorySystem, we need to swap it
                        // This is a bit tricky - for now, let's just note this as a TODO
                        // TODO: Support passing existing InventorySystem to factory
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
