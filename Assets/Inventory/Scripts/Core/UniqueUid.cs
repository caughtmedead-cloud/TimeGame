using System;
using UnityEngine;

namespace Inventory.Scripts.Core
{
    [Serializable]
    public class UniqueUid
    {
        public const string FieldName = "uid";
        
        [HideInInspector, SerializeField] protected string uid;

        public string Uid => uid;
    }
}