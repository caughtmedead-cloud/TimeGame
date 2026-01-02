using Inventory.Scripts.Core.Helper;
using UnityEngine;

namespace Inventory.Scripts.Core.Holders
{
    /// <summary>
    /// Replicator Item Holder are used for replicate the same item but in another UI Holder. If you equip the HolderData on this Holder and the replicationUid match some of other holder id. Will replicate the holder.
    /// </summary>
    public class ReplicatorItemHolder : ItemHolder
    {
        [Header("Replication Holder UID")] public ReplicationUid replicationUid;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!replicationUid.IsSetReplicationUid())
            {
                Debug.LogWarning(
                    $"Replication Uid not configured correctly. Please set your {nameof(replicationUid)} property in holder {gameObject.name}. If you not configure, some items might disappear if equipped"
                        .Configuration());
            }

            if (EquippedAbstractItemUi == null) return;

            ResizeItem(EquippedAbstractItemUi);
        }

        public void StopReplicate()
        {
            holderData = null;
        }
    }
}