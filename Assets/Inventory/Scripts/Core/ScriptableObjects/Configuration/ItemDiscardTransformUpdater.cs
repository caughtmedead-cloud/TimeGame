using Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors;
using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Configuration
{
    public class ItemDiscardTransformUpdater : MonoBehaviour
    {
        [Header("Player Drop Transform Anchor So")] [SerializeField]
        private TransformAnchorSo itemDiscardTransformSo;

        private void Update()
        {
            itemDiscardTransformSo.Value = transform;
        }
    }
}