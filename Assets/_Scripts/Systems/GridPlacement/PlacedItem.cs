using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimeGame.Systems.GridPlacement
{
    /// <summary>
    /// Represents a runtime instance of a placed item in the grid.
    /// This is the "physical" object that occupies cells.
    ///
    /// STACK MASTER PATTERN:
    /// When items are stacked, the PlacedItem at that grid position becomes the "Stack Master".
    /// It contains an array of ItemInstance objects, each representing a physical item in the stack.
    /// This allows tracking individual item properties (durability, uses, etc.) while maintaining
    /// lightweight stack representation.
    /// </summary>
    public class PlacedItem
    {
        /// <summary>
        /// Unique ID for this placed item instance (the Stack Master).
        /// Used for networking and removal operations.
        /// </summary>
        public Guid InstanceID { get; private set; }

        /// <summary>
        /// The item definition (ScriptableObject) this placement represents.
        /// </summary>
        public PlacableItemSO ItemDefinition { get; private set; }

        /// <summary>
        /// Anchor position in the grid (typically bottom-left corner).
        /// </summary>
        public Vector2Int AnchorPosition { get; private set; }

        /// <summary>
        /// Current rotation direction.
        /// </summary>
        public GridDirection Rotation { get; private set; }

        /// <summary>
        /// List of all grid cells this item occupies.
        /// Cached for performance.
        /// </summary>
        public List<Vector2Int> OccupiedCells { get; private set; }

        /// <summary>
        /// Optional: Reference to visual representation (MonoBehaviour).
        /// Can be null if this is a data-only placement.
        /// </summary>
        public MonoBehaviour VisualRepresentation { get; set; }

        /// <summary>
        /// STACK MASTER: Array of individual item instances in this stack.
        /// Null = Homogeneous stack (old system - items are identical, just count them)
        /// Non-null = Tracked stack (each item has individual properties)
        ///
        /// When TrackIndividualItems = true on the ItemSO, this array is populated.
        /// Each ItemInstance represents one physical item with its own durability, uses, etc.
        /// 
        /// NOTE: For non-tracked items with HasLimitedUses = true, this may contain a single
        /// "representative" ItemInstance that stores the uses state for the entire stack.
        /// </summary>
        public List<ItemInstance> ItemInstances { get; private set; }

        /// <summary>
        /// For non-tracked items with limited uses, tracks whether ItemInstances is fully-tracked
        /// or just a uses-representative. When true, StackCount is separate from ItemInstances.Count.
        /// False = ItemInstances represents the full stack (fully tracked)
        /// True = ItemInstances is just a uses representative
        /// </summary>
        private bool isUsesRepresentative = false;

        /// <summary>
        /// Stack count for stackable items.
        /// If ItemInstances is null: This is the count (homogeneous stack, no uses tracking)
        /// If ItemInstances is not null and isUsesRepresentative = false: This returns ItemInstances.Count (fully-tracked stack)
        /// If ItemInstances is not null and isUsesRepresentative = true: This returns stackCount (non-tracked with uses)
        /// Always >= 1.
        /// </summary>
        public int StackCount
        {
            get
            {
                // Non-tracked stack with uses representative: use stored count
                if (isUsesRepresentative && ItemInstances != null)
                    return stackCount;

                // Fully-tracked stack: count is derived from array
                if (ItemInstances != null)
                    return ItemInstances.Count;

                // Homogeneous stack: use simple count
                return stackCount;
            }
            private set
            {
                stackCount = Mathf.Max(1, value);
            }
        }
        private int stackCount = 1; // Backing field for homogeneous stacks

        /// <summary>
        /// Check if this stack tracks individual item instances (has non-null ItemInstances).
        /// Used for networking and determining if we have instance data to serialize.
        /// True = ItemInstances is populated (whether fully-tracked or uses-representative)
        /// False = ItemInstances is null (pure homogeneous stack, no instance data)
        /// </summary>
        public bool IsInstanceTracked => ItemInstances != null;

        /// <summary>
        /// Check if this stack is FULLY tracked (each item has individual properties).
        /// True = Each of the N items in the stack is individually tracked
        /// False = Stack is homogeneous (all items identical) or uses-representative
        /// </summary>
        public bool IsFullyTracked => ItemInstances != null && !isUsesRepresentative;

        /// <summary>
        /// Check if this stack has a uses representative (non-tracked item with limited uses).
        /// True = ItemInstances contains a single representative for uses display
        /// False = ItemInstances is either null or fully-tracked
        /// </summary>
        public bool HasUsesRepresentative => ItemInstances != null && isUsesRepresentative;

        /// <summary>
        /// Optional: Instance-specific data for items with durability, modifications, etc.
        /// DEPRECATED: Use ItemInstances array instead for tracked items.
        /// Kept for backward compatibility with old homogeneous stack system.
        /// </summary>
        [Obsolete("Use ItemInstances array for tracked items")]
        public object ItemData { get; set; }

        /// <summary>
        /// Optional: InstanceID of the item this was split from.
        /// Used for tracking item lineage and merging behavior.
        /// Null/Empty if this is an original item (not split from another).
        /// </summary>
        public Guid? SplitFromInstanceID { get; set; }

        /// <summary>
        /// Fired when ContainerInventory is assigned or replaced.
        /// InventorySystem subscribes to this so it can re-wire the nested
        /// OnWeightChanged bubble whenever a container item's inventory is swapped in.
        /// (old system, new system) — either may be null.
        /// </summary>
        public event Action<Inventory.InventorySystem, Inventory.InventorySystem> OnContainerInventoryChanged;

        /// <summary>
        /// NESTED INVENTORY: Container inventory system for items that provide storage.
        /// Null for non-container items.
        /// When this item is a backpack/chest/container, this holds all items inside it.
        /// This enables recursive nesting (backpacks within backpacks, etc.)
        ///
        /// NOTE: This is NOT serialized automatically - use ContainerItemData for serialization.
        /// </summary>
        [System.NonSerialized]
        private Inventory.InventorySystem containerInventory;

        public Inventory.InventorySystem ContainerInventory
        {
            get => containerInventory;
            set
            {
                Inventory.InventorySystem old = containerInventory;
                containerInventory = value;
                if (old != value)
                    OnContainerInventoryChanged?.Invoke(old, value);
            }
        }

        public PlacedItem(PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation, int stackCount = 1)
        {
            InstanceID = Guid.NewGuid();
            ItemDefinition = itemDefinition;
            AnchorPosition = anchorPosition;
            Rotation = rotation;

            // Calculate and cache occupied cells
            OccupiedCells = itemDefinition.GetGridPositionList(anchorPosition, rotation);

            // STACK MASTER: Initialize based on whether item tracks individual instances
            InitializeStack(stackCount);

            VisualRepresentation = null;
        }

        /// <summary>
        /// Constructor with specific instance ID (used for network replication).
        /// </summary>
        public PlacedItem(Guid instanceID, PlacableItemSO itemDefinition, Vector2Int anchorPosition, GridDirection rotation, int stackCount = 1)
        {
            InstanceID = instanceID;
            ItemDefinition = itemDefinition;
            AnchorPosition = anchorPosition;
            Rotation = rotation;

            // Calculate and cache occupied cells
            OccupiedCells = itemDefinition.GetGridPositionList(anchorPosition, rotation);

            // STACK MASTER: Initialize based on whether item tracks individual instances
            InitializeStack(stackCount);

            VisualRepresentation = null;
        }

        /// <summary>
        /// Initialize the stack based on item definition settings.
        /// Creates ItemInstances array if item tracks individual instances.
        /// If count is 0, creates an empty tracked list but does NOT create instances.
        /// This allows external systems to provide instances after construction.
        /// </summary>
        private void InitializeStack(int count)
        {
            // Check if this item type tracks individual instances
            Inventory.InventoryItemSO inventoryItem = ItemDefinition as Inventory.InventoryItemSO;
            if (inventoryItem != null && inventoryItem.TrackIndividualItems)
            {
                // TRACKED STACK: Create individual item instances
                ItemInstances = new List<ItemInstance>();
                isUsesRepresentative = false;

                // If count is 0, create empty list (instances will be provided externally)
                if (count == 0)
                {
                    // Empty tracked list - caller will add instances
                    return;
                }

                // Otherwise create pristine instances
                count = Mathf.Max(1, count);
                for (int i = 0; i < count; i++)
                {
                    // Create instance with uses based on item definition
                    ItemInstance instance = new ItemInstance();
                    if (inventoryItem.HasLimitedUses)
                    {
                        instance.UsesRemaining = inventoryItem.MaxUses;
                    }
                    else
                    {
                        instance.UsesRemaining = -1; // Infinite uses
                    }
                    ItemInstances.Add(instance);
                }
            }
            else if (inventoryItem != null && inventoryItem.HasLimitedUses)
            {
                // NON-TRACKED STACK WITH LIMITED USES: Create a single representative instance for uses
                // All items in this non-tracked stack have the same uses (they're homogeneous)
                ItemInstances = new List<ItemInstance>();
                isUsesRepresentative = true;

                // If count is 0, leave the list empty — the caller (e.g. network snapshot load)
                // will supply the representative instance via AddInstances().
                if (count == 0)
                    return;

                // Create single representative instance with uses
                ItemInstance representative = new ItemInstance();
                representative.UsesRemaining = inventoryItem.MaxUses;
                ItemInstances.Add(representative);

                // Store the actual stack count separately
                count = Mathf.Max(1, count);
                StackCount = count;
            }
            else
            {
                // HOMOGENEOUS STACK (no uses tracking): Just track count
                ItemInstances = null;
                isUsesRepresentative = false;
                count = Mathf.Max(1, count);
                StackCount = count;
            }
        }

        /// <summary>
        /// Check if this placement contains a specific grid position.
        /// </summary>
        public bool ContainsPosition(Vector2Int position)
        {
            return OccupiedCells.Contains(position);
        }

        /// <summary>
        /// Mutates anchor, rotation, and occupied-cell cache in-place.
        /// Called exclusively by <see cref="GridPlacementSystem.RepositionItem"/> after
        /// grid cells have been updated.  Never call from outside the grid system —
        /// this does NOT update the grid; the grid system is responsible for that.
        /// </summary>
        internal void UpdatePlacement(Vector2Int newAnchor, GridDirection newRotation)
        {
            AnchorPosition = newAnchor;
            Rotation       = newRotation;
            OccupiedCells  = ItemDefinition.GetGridPositionList(newAnchor, newRotation);
        }

        /// <summary>
        /// Set the stack count. Used when modifying stack size (split/merge operations).
        /// For homogeneous stacks and uses-representative stacks: updates the count.
        /// For fully-tracked stacks: use Add/RemoveInstances instead.
        /// </summary>
        public void SetStackCount(int count)
        {
            if (ItemInstances != null && !isUsesRepresentative)
            {
                Debug.LogWarning("[PlacedItem] SetStackCount called on fully-tracked stack! Use Add/RemoveInstances instead.");
                return;
            }

            StackCount = Mathf.Max(1, count);
        }

        /// <summary>
        /// Add to the stack count. Returns the actual amount added.
        /// For fully-tracked stacks: creates new pristine item instances.
        /// For homogeneous stacks and uses-representative: increments count.
        /// </summary>
        public int AddToStack(int amount)
        {
            if (ItemInstances != null && !isUsesRepresentative)
            {
                // FULLY-TRACKED STACK: Create new pristine instances with proper initialization
                Inventory.InventoryItemSO inventoryItem = ItemDefinition as Inventory.InventoryItemSO;
                for (int i = 0; i < amount; i++)
                {
                    ItemInstance instance = new ItemInstance();

                    // Initialize uses based on item definition
                    if (inventoryItem != null && inventoryItem.HasLimitedUses)
                    {
                        instance.UsesRemaining = inventoryItem.MaxUses;
                    }
                    else
                    {
                        instance.UsesRemaining = -1; // Infinite uses
                    }

                    ItemInstances.Add(instance);
                }
                return amount;
            }
            else
            {
                // HOMOGENEOUS STACK or USES-REPRESENTATIVE: Just increment count
                StackCount += amount;
                return amount;
            }
        }

        /// <summary>
        /// Remove from the stack count. Returns the actual amount removed.
        /// Stack count will never go below 1.
        /// For tracked stacks: removes instances from the end of the array.
        /// For homogeneous stacks: decrements count.
        /// </summary>
        public int RemoveFromStack(int amount)
        {
            if (ItemInstances != null && !isUsesRepresentative)
            {
                // FULLY-TRACKED STACK: Remove instances from end
                int actualRemoved = Mathf.Min(amount, ItemInstances.Count - 1);
                for (int i = 0; i < actualRemoved; i++)
                {
                    ItemInstances.RemoveAt(ItemInstances.Count - 1);
                }
                return actualRemoved;
            }
            else
            {
                // HOMOGENEOUS STACK or USES-REPRESENTATIVE: Decrement count
                int actualRemoved = Mathf.Min(amount, StackCount - 1);
                StackCount -= actualRemoved;
                return actualRemoved;
            }
        }

        /// <summary>
        /// Add specific item instances to this stack (for merging tracked stacks or equipping items with uses).
        /// Appends instances to the END of the array to maintain chronological order.
        /// Combined with RemoveInstances (which takes from end), this creates LIFO behavior
        /// where the most recently added items are split off first.
        /// Works for fully-tracked stacks and uses-representative stacks.
        /// </summary>
        public void AddInstances(List<ItemInstance> instances)
        {
            if (ItemInstances == null)
            {
                Debug.LogWarning("[PlacedItem] AddInstances called on homogeneous stack!");
                return;
            }

            if (instances != null && instances.Count > 0)
            {
                // Append to end to maintain chronological order [A, B, C, D]
                // RemoveInstances takes from end (LIFO), so split gives most recent items
                ItemInstances.AddRange(instances);
            }
        }

        /// <summary>
        /// Remove and return specific item instances from this stack (for splitting tracked stacks).
        /// Removes from the END of the array (LIFO - last in, first out).
        /// This ensures that when you split a stack, you get the most recently added items.
        /// Stack will never go below 1 instance.
        /// Only works for fully-tracked stacks. Returns null for homogeneous or uses-representative stacks.
        /// </summary>
        public List<ItemInstance> RemoveInstances(int count)
        {
            // Only works for fully-tracked stacks (not for uses-representative)
            if (ItemInstances == null || isUsesRepresentative)
            {
                Debug.LogWarning("[PlacedItem] RemoveInstances called on non-fully-tracked stack!");
                return null;
            }

            if (count <= 0)
                return new List<ItemInstance>();

            // Never remove all instances - keep at least 1
            count = Mathf.Min(count, ItemInstances.Count - 1);

            List<ItemInstance> removed = new List<ItemInstance>();

            // CRITICAL: Remove from END (LIFO) to get most recently added items
            // This makes sense for splits: if you merge a used item onto pristine items,
            // then split, you want to split off the pristine ones (which were added last)
            for (int i = 0; i < count; i++)
            {
                int lastIndex = ItemInstances.Count - 1;
                removed.Add(ItemInstances[lastIndex]);
                ItemInstances.RemoveAt(lastIndex);
            }

            return removed;
        }

        /// <summary>
        /// Check if this item was split from another item.
        /// </summary>
        public bool IsSplitStack()
        {
            return SplitFromInstanceID.HasValue;
        }

        /// <summary>
        /// Get the root/original InstanceID in the split chain.
        /// If not split, returns this item's ID.
        /// </summary>
        public Guid GetRootInstanceID()
        {
            return SplitFromInstanceID ?? InstanceID;
        }

        /// <summary>
        /// Check if two items can be merged (same item type and compatible instances).
        /// For tracked stacks: items can always merge (individual instances are preserved).
        /// For homogeneous stacks: items must be from same split chain or have compatible data.
        /// </summary>
        public virtual bool CanMergeWith(PlacedItem other)
        {
            if (other == null) return false;
            if (ItemDefinition != other.ItemDefinition) return false;

            // Can't merge tracked with non-tracked stacks
            if (IsInstanceTracked != other.IsInstanceTracked)
            {
                Debug.LogWarning("[PlacedItem] Cannot merge tracked stack with homogeneous stack!");
                return false;
            }

            // TRACKED STACKS: Can always merge (individual instances are preserved)
            if (IsInstanceTracked)
            {
                return true; // Just combine the instance arrays
            }

            // HOMOGENEOUS STACKS: Use old merge rules
            // Items from the same split chain can always merge
            if (GetRootInstanceID() == other.GetRootInstanceID())
                return true;

            // For items with data, game systems should override this method
            // Default: allow merging if both have no data or same data reference
            #pragma warning disable CS0618 // ItemData is obsolete but still supported for backward compatibility
            if (ItemData == null && other.ItemData == null)
                return true;

            return ItemData == other.ItemData;
            #pragma warning restore CS0618
        }

        public override string ToString()
        {
            string stackInfo = StackCount > 1 ? $" x{StackCount}" : "";
            string splitInfo = IsSplitStack() ? $" (split from {SplitFromInstanceID})" : "";
            string trackingInfo = IsInstanceTracked ? " [TRACKED]" : "";
            return $"{ItemDefinition.ItemName}{stackInfo} at {AnchorPosition} facing {Rotation} (ID: {InstanceID}){splitInfo}{trackingInfo}";
        }
    }
}
