using System;
using UnityEngine;

namespace Inventory.Scripts.Core
{
    [Serializable]
    public class ReplicationUid
    {
        public static string DefaultMessageEmpty = "Set your replicated Holder UID here...";

        public const string FieldName = "replicatorUid";

        [HideInInspector, SerializeField] protected string replicatorUid;

        public string ReplicatorUid => replicatorUid;

        public bool IsSetReplicationUid()
        {
            return !string.IsNullOrEmpty(DefaultMessageEmpty) && !replicatorUid.Equals(DefaultMessageEmpty);
        }
    }
}