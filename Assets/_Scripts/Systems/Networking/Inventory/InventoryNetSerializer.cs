using System;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Serializing;
using TimeGame.Systems.GridPlacement;
using TimeGame.Systems.Inventory;
using UnityEngine;

/// <summary>
/// Phase 1 — Custom FishNet V4 serializers for all inventory network types.
///
/// WHY CUSTOM SERIALIZERS?
/// FishNet's code-gen cannot serialize:
///   • System.Guid (no default writer in V4 user land — WriteGuidAllocated exists on Writer
///     internally, but we provide explicit helpers so our RPCs compile cleanly)
///   • ItemInstance  (class with a Guid field; code-gen would recurse into fields but Guid
///     hits the wall above)
///   • NetPlacedItemData / NetContainerSnapshot  (our own network-only structs that reference
///     InventoryItemSO by string name instead of direct SO reference)
///
/// DESIGN RULES
///   1. Never send SO references over the wire — send ItemName strings; the receiver
///      resolves them through InventoryItemRegistry (loaded from Resources).
///   2. Grid positions / rotation ARE sent for world-container snapshots and the initial
///      pickup/drop payload (owner's client needs to reconstruct exactly).
///   3. Server item manifest (Dictionary<Guid, ServerItemRecord>) stores ONLY existence
///      data — no positions — so it never needs to be serialised as a whole.
///   4. All structs are immutable value types; classes use explicit constructors to keep
///      deserialization side-effect-free.
///
/// WIRE LAYOUT (each type annotated inline below)
/// </summary>
namespace TimeGame.Systems.Networking.Inventory
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Network-only data structs  (only live during transmission / as RPC args)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Network-safe mirror of ItemInstance.
    /// Sent whenever the server needs to describe a specific tracked item to a client.
    /// 16 B Guid + 4 B int + 4 B int + 4 B float = 28 B per instance.
    /// </summary>
    [UseGlobalCustomSerializer]
    public struct NetItemInstance
    {
        public Guid InstanceID;     // 16 B
        public int  Durability;     // packed int  (~1-2 B)
        public int  UsesRemaining;  // packed int  (~1-2 B)
        public float Condition;     // 4 B unpacked

        public NetItemInstance(Guid id, int durability, int uses, float condition)
        {
            InstanceID    = id;
            Durability    = durability;
            UsesRemaining = uses;
            Condition     = condition;
        }

        /// <summary>Convert a runtime ItemInstance → NetItemInstance for sending.</summary>
        public static NetItemInstance FromItemInstance(ItemInstance src) =>
            new NetItemInstance(src.InstanceID, src.Durability, src.UsesRemaining, src.Condition);

        /// <summary>Reconstruct a runtime ItemInstance from deserialized data.</summary>
        public ItemInstance ToItemInstance() =>
            new ItemInstance(InstanceID, Durability, UsesRemaining, Condition);
    }

    /// <summary>
    /// Network-safe representation of one placed item inside an inventory grid.
    ///
    /// Used for:
    ///   • Full inventory snapshot on join / respawn  (Phase 2)
    ///   • World container contents broadcast          (Phase 6)
    ///
    /// Wire size (approximate, no nesting):
    ///   16 B Guid  +  2 B string header + N B item name  +  4 B Vector2Int  +  1 B rotation
    ///   + 2 B stack count  + 1 B has-instances flag
    ///   + (28 B × instance count if tracked)
    ///   + 1 B has-container flag  + (recursive if container)
    /// </summary>
    [UseGlobalCustomSerializer]
    public struct NetPlacedItemData
    {
        public Guid        InstanceId;      // server-authoritative item identity — MUST be first in wire order
        public string      ItemDefName;     // InventoryItemRegistry key
        public Vector2Int  AnchorPosition;
        public GridDirection Rotation;
        public int         StackCount;
        public bool        IsInstanceTracked;
        public NetItemInstance[] Instances; // null / empty for homogeneous stacks
        public bool        HasContainer;
        public NetContainerSnapshot ContainerSnapshot; // only valid when HasContainer == true
    }

    /// <summary>
    /// Snapshot of one container compartment for network sync.
    /// Used for world-loot containers and container-item contents broadcasting.
    ///
    /// Wire: 2 B compartment count, then per-compartment:
    ///   4 B grid size + 4 B max weight + 2 B item count + items[]
    /// </summary>
    [UseGlobalCustomSerializer]
    public struct NetContainerSnapshot
    {
        public NetCompartmentSnapshot[] Compartments;
    }

    /// <summary>One compartment inside a NetContainerSnapshot.</summary>
    [UseGlobalCustomSerializer]
    public struct NetCompartmentSnapshot
    {
        public Vector2Int GridSize;
        public float      MaxWeight;
        public NetPlacedItemData[] Items;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Server-manifest record  (server only — never serialised over wire)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-side item ownership record.
    /// The server keeps a  Dictionary{Guid, ServerItemRecord}  per player;
    /// this is NEVER sent over the network — it is authoritative local state only.
    ///
    /// Used to:
    ///   • Confirm a pickup request references an item that still exists in the world.
    ///   • Confirm a drop/use request references an item the player actually owns.
    ///   • Validate use-count / durability before applying damage.
    ///   • Rebuild the player's inventory on reconnect if needed.
    /// </summary>
    public struct ServerItemRecord
    {
        public Guid   InstanceID;
        public string ItemDefName;          // InventoryItemRegistry key
        public int    StackCount;
        public int    Durability;           // aggregate / first-instance for validation
        public int    UsesRemaining;        // aggregate / first-instance for validation

        // Container items (backpacks, crates) may carry a snapshot of their nested
        // contents so the server can restore them when the item is later placed into
        // a world loot container via SvrPutItemIntoContainer.
        // HasContainerSnapshot == false for all ordinary (non-container) items.
        public bool                HasContainerSnapshot;
        public NetContainerSnapshot ContainerSnapshot; // only valid when HasContainerSnapshot == true
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FishNet Writer extension methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Custom FishNet V4 Write extensions for all inventory network types.
    /// Placed in global namespace so FishNet's reflection scanner picks them up automatically.
    /// </summary>
    public static class InventoryWriterExtensions
    {
        // ── NetItemInstance ──────────────────────────────────────────────────

        /// <summary>
        /// Wire: 16 B Guid | packed int durability | packed int uses | float condition
        /// </summary>
        public static void WriteNetItemInstance(this Writer writer, NetItemInstance value)
        {
            writer.WriteGuidAllocated(value.InstanceID);
            writer.WriteInt32(value.Durability);
            writer.WriteInt32(value.UsesRemaining);
            writer.WriteSingle(value.Condition);
        }

        // ── NetItemInstance array (null-safe) ───────────────────────────────

        public static void WriteNetItemInstanceArray(this Writer writer, NetItemInstance[] value)
        {
            if (value == null || value.Length == 0)
            {
                writer.WriteInt16((short)0);
                return;
            }

            writer.WriteInt16((short)value.Length);
            for (int i = 0; i < value.Length; i++)
                writer.WriteNetItemInstance(value[i]);
        }

        // ── NetPlacedItemData array (null-safe) ─────────────────────────────

        /// <summary>
        /// Used for SvrSyncInventory RPC which sends the owner's full item list to the server.
        /// Wire: short count | items[]
        /// </summary>
        public static void WriteNetPlacedItemDataArray(this Writer writer, NetPlacedItemData[] value)
        {
            if (value == null || value.Length == 0)
            {
                writer.WriteInt16((short)0);
                return;
            }

            writer.WriteInt16((short)value.Length);
            for (int i = 0; i < value.Length; i++)
                writer.WriteNetPlacedItemData(value[i]);
        }

        // ── NetPlacedItemData ────────────────────────────────────────────────

        /// <summary>
        /// Wire: Guid instanceId | string itemDefName | Vector2Int anchor | byte rotation
        ///       | packed int stackCount | bool isInstanceTracked
        ///       | (short instanceCount + instances[])
        ///       | bool hasContainer | (NetContainerSnapshot if hasContainer)
        /// </summary>
        public static void WriteNetPlacedItemData(this Writer writer, NetPlacedItemData value)
        {
            writer.WriteGuidAllocated(value.InstanceId); // MUST be first — mirrors Read order
            writer.WriteString(value.ItemDefName);
            writer.WriteVector2Int(value.AnchorPosition);
            writer.WriteUInt8Unpacked((byte)value.Rotation);
            writer.WriteInt32(value.StackCount);
            writer.WriteBoolean(value.IsInstanceTracked);

            // Instance array (only meaningful when IsInstanceTracked)
            writer.WriteNetItemInstanceArray(value.Instances);

            // Optional nested container
            writer.WriteBoolean(value.HasContainer);
            if (value.HasContainer)
                writer.WriteNetContainerSnapshot(value.ContainerSnapshot);
        }

        // ── NetCompartmentSnapshot ───────────────────────────────────────────

        public static void WriteNetCompartmentSnapshot(this Writer writer, NetCompartmentSnapshot value)
        {
            writer.WriteVector2Int(value.GridSize);
            writer.WriteSingle(value.MaxWeight);

            int count = value.Items == null ? 0 : value.Items.Length;
            writer.WriteInt16((short)count);
            if (value.Items != null)
            {
                for (int i = 0; i < value.Items.Length; i++)
                    writer.WriteNetPlacedItemData(value.Items[i]);
            }
        }

        // ── NetContainerSnapshot ─────────────────────────────────────────────

        /// <summary>
        /// Wire: short compartmentCount | compartments[]
        /// Recursive nesting handled naturally by WriteNetPlacedItemData above.
        /// </summary>
        public static void WriteNetContainerSnapshot(this Writer writer, NetContainerSnapshot value)
        {
            int count = value.Compartments == null ? 0 : value.Compartments.Length;
            writer.WriteInt16((short)count);
            if (value.Compartments != null)
            {
                for (int i = 0; i < value.Compartments.Length; i++)
                    writer.WriteNetCompartmentSnapshot(value.Compartments[i]);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FishNet Reader extension methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Custom FishNet V4 Read extensions — must mirror the Write methods exactly.
    /// </summary>
    public static class InventoryReaderExtensions
    {
        // ── NetItemInstance ──────────────────────────────────────────────────

        public static NetItemInstance ReadNetItemInstance(this Reader reader)
        {
            Guid  id         = reader.ReadGuid();
            int   durability = reader.ReadInt32();
            int   uses       = reader.ReadInt32();
            float condition  = reader.ReadSingle();
            return new NetItemInstance(id, durability, uses, condition);
        }

        // ── NetItemInstance array ────────────────────────────────────────────

        public static NetItemInstance[] ReadNetItemInstanceArray(this Reader reader)
        {
            short count = reader.ReadInt16();
            if (count <= 0) return Array.Empty<NetItemInstance>();

            NetItemInstance[] arr = new NetItemInstance[count];
            for (int i = 0; i < count; i++)
                arr[i] = reader.ReadNetItemInstance();
            return arr;
        }

        // ── NetPlacedItemData array ──────────────────────────────────────────

        public static NetPlacedItemData[] ReadNetPlacedItemDataArray(this Reader reader)
        {
            short count = reader.ReadInt16();
            if (count <= 0) return Array.Empty<NetPlacedItemData>();

            NetPlacedItemData[] arr = new NetPlacedItemData[count];
            for (int i = 0; i < count; i++)
                arr[i] = reader.ReadNetPlacedItemData();
            return arr;
        }

        // ── NetPlacedItemData ────────────────────────────────────────────────

        public static NetPlacedItemData ReadNetPlacedItemData(this Reader reader)
        {
            NetPlacedItemData data = new NetPlacedItemData
            {
                InstanceId        = reader.ReadGuid(),  // MUST be first — mirrors Write order
                ItemDefName       = reader.ReadStringAllocated(),
                AnchorPosition    = reader.ReadVector2Int(),
                Rotation          = (GridDirection)reader.ReadUInt8Unpacked(),
                StackCount        = reader.ReadInt32(),
                IsInstanceTracked = reader.ReadBoolean(),
                Instances         = reader.ReadNetItemInstanceArray()
            };

            data.HasContainer = reader.ReadBoolean();
            if (data.HasContainer)
                data.ContainerSnapshot = reader.ReadNetContainerSnapshot();

            return data;
        }

        // ── NetCompartmentSnapshot ───────────────────────────────────────────

        public static NetCompartmentSnapshot ReadNetCompartmentSnapshot(this Reader reader)
        {
            Vector2Int gridSize  = reader.ReadVector2Int();
            float      maxWeight = reader.ReadSingle();
            short      count     = reader.ReadInt16();

            NetPlacedItemData[] items = count > 0
                ? new NetPlacedItemData[count]
                : Array.Empty<NetPlacedItemData>();

            for (int i = 0; i < count; i++)
                items[i] = reader.ReadNetPlacedItemData();

            return new NetCompartmentSnapshot
            {
                GridSize  = gridSize,
                MaxWeight = maxWeight,
                Items     = items
            };
        }

        // ── NetContainerSnapshot ─────────────────────────────────────────────

        public static NetContainerSnapshot ReadNetContainerSnapshot(this Reader reader)
        {
            short count = reader.ReadInt16();

            NetCompartmentSnapshot[] compartments = count > 0
                ? new NetCompartmentSnapshot[count]
                : Array.Empty<NetCompartmentSnapshot>();

            for (int i = 0; i < count; i++)
                compartments[i] = reader.ReadNetCompartmentSnapshot();

            return new NetContainerSnapshot { Compartments = compartments };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Static helpers — convert between runtime types and network types
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Conversion utilities used by NetworkedInventoryComponent and world container systems.
    /// These live server-side (or owner-side for speculative add) and never touch the wire.
    /// </summary>
    public static class InventoryNetConverter
    {
        // ── Runtime → Network ────────────────────────────────────────────────

        /// <summary>
        /// Snapshot an entire ContainerItemData tree into a NetContainerSnapshot.
        /// Call this on the server when a player opens a world container, or when
        /// sending the initial inventory snapshot on join.
        /// </summary>
        public static NetContainerSnapshot ToNetSnapshot(ContainerItemData src)
        {
            if (src == null || src.Compartments == null || src.Compartments.Count == 0)
                return new NetContainerSnapshot { Compartments = Array.Empty<NetCompartmentSnapshot>() };

            NetCompartmentSnapshot[] compartments = new NetCompartmentSnapshot[src.Compartments.Count];
            for (int c = 0; c < src.Compartments.Count; c++)
                compartments[c] = ToNetCompartment(src.Compartments[c]);

            return new NetContainerSnapshot { Compartments = compartments };
        }

        private static NetCompartmentSnapshot ToNetCompartment(CompartmentData src)
        {
            int itemCount = src.Items == null ? 0 : src.Items.Count;
            NetPlacedItemData[] items = itemCount > 0
                ? new NetPlacedItemData[itemCount]
                : Array.Empty<NetPlacedItemData>();

            for (int i = 0; i < itemCount; i++)
                items[i] = ToNetPlacedItem(src.Items[i]);

            return new NetCompartmentSnapshot
            {
                GridSize  = src.GridSize,
                MaxWeight = src.MaxWeight,
                Items     = items
            };
        }

        public static NetPlacedItemData ToNetPlacedItem(PlacedItemData src)
        {
            if (src == null)
                return default;

            // Build instance array
            NetItemInstance[] instances = Array.Empty<NetItemInstance>();
            bool isTracked = src.ItemInstances != null && src.ItemInstances.Count > 0;
            if (isTracked)
            {
                instances = new NetItemInstance[src.ItemInstances.Count];
                for (int i = 0; i < src.ItemInstances.Count; i++)
                    instances[i] = NetItemInstance.FromItemInstance(src.ItemInstances[i]);
            }

            // Nested container
            bool hasContainer = src.ContainerData != null;
            NetContainerSnapshot containerSnapshot = hasContainer
                ? ToNetSnapshot(src.ContainerData)
                : default;

            return new NetPlacedItemData
            {
                InstanceId        = src.InstanceId,     // preserve server-authoritative ID end-to-end
                ItemDefName       = src.ItemDefinition != null ? src.ItemDefinition.ItemName : string.Empty,
                AnchorPosition    = src.AnchorPosition,
                Rotation          = src.Rotation,
                StackCount        = src.StackCount,
                IsInstanceTracked = isTracked,
                Instances         = instances,
                HasContainer      = hasContainer,
                ContainerSnapshot = containerSnapshot
            };
        }

        // ── Network → Runtime ────────────────────────────────────────────────

        /// <summary>
        /// Reconstruct a ContainerItemData from a received NetContainerSnapshot.
        /// Requires access to the InventoryItemRegistry for item lookup.
        /// Call this on the client when receiving a container-open event or join snapshot.
        /// </summary>
        public static ContainerItemData FromNetSnapshot(
            NetContainerSnapshot src,
            InventoryItemRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogError("[InventoryNetConverter] Cannot reconstruct snapshot — registry is null.");
                return null;
            }

            ContainerItemData result = new ContainerItemData();
            if (src.Compartments == null) return result;

            foreach (NetCompartmentSnapshot compartmentNet in src.Compartments)
            {
                CompartmentData compartment = new CompartmentData
                {
                    GridSize  = compartmentNet.GridSize,
                    MaxWeight = compartmentNet.MaxWeight,
                    Items     = new List<PlacedItemData>()
                };

                if (compartmentNet.Items != null)
                {
                    foreach (NetPlacedItemData itemNet in compartmentNet.Items)
                    {
                        PlacedItemData item = FromNetPlacedItem(itemNet, registry);
                        if (item != null)
                            compartment.Items.Add(item);
                    }
                }

                result.Compartments.Add(compartment);
            }

            return result;
        }

        public static PlacedItemData FromNetPlacedItem(
            NetPlacedItemData src,
            InventoryItemRegistry registry)
        {
            if (string.IsNullOrEmpty(src.ItemDefName)) return null;

            InventoryItemSO itemDef = registry.GetItem(src.ItemDefName);
            if (itemDef == null)
            {
                Debug.LogError($"[InventoryNetConverter] Unknown item '{src.ItemDefName}' — skipping.");
                return null;
            }

            PlacedItemData data = new PlacedItemData
            {
                InstanceId     = src.InstanceId,  // carry over server-authoritative ID
                ItemDefinition = itemDef,
                AnchorPosition = src.AnchorPosition,
                Rotation       = src.Rotation,
                StackCount     = src.StackCount
            };

            // Rebuild instance list for tracked items
            if (src.IsInstanceTracked && src.Instances != null && src.Instances.Length > 0)
            {
                data.ItemInstances = new List<ItemInstance>(src.Instances.Length);
                foreach (NetItemInstance ni in src.Instances)
                    data.ItemInstances.Add(ni.ToItemInstance());
            }

            // Recurse into nested container
            if (src.HasContainer)
                data.ContainerData = FromNetSnapshot(src.ContainerSnapshot, registry);

            return data;
        }

        // ── Server manifest helpers ──────────────────────────────────────────

        /// <summary>
        /// Build a ServerItemRecord from a runtime PlacedItem.
        /// Called server-side when an item enters the player's manifest (pickup / spawn).
        /// </summary>
        public static ServerItemRecord ToServerRecord(
            TimeGame.Systems.GridPlacement.PlacedItem src)
        {
            int durability    = 100;
            int usesRemaining = -1;

            // Use first instance for aggregate validation if tracked
            if (src.IsInstanceTracked && src.ItemInstances != null && src.ItemInstances.Count > 0)
            {
                durability    = src.ItemInstances[0].Durability;
                usesRemaining = src.ItemInstances[0].UsesRemaining;
            }

            // Snapshot container contents so the server can restore them if this item is
            // later placed into a world loot container (SvrPutItemIntoContainer path).
            bool                hasContainerSnapshot = false;
            NetContainerSnapshot containerSnapshot   = default;
            if (src.ContainerInventory != null)
            {
                ContainerItemData containerData = ContainerItemData.FromInventorySystem(src.ContainerInventory);
                if (containerData != null)
                {
                    containerSnapshot    = InventoryNetConverter.ToNetSnapshot(containerData);
                    hasContainerSnapshot = true;
                }
            }

            return new ServerItemRecord
            {
                InstanceID           = src.InstanceID,
                ItemDefName          = src.ItemDefinition != null ? src.ItemDefinition.ItemName : string.Empty,
                StackCount           = src.StackCount,
                Durability           = durability,
                UsesRemaining        = usesRemaining,
                HasContainerSnapshot = hasContainerSnapshot,
                ContainerSnapshot    = containerSnapshot
            };
        }
    }
}
