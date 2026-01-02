using UnityEngine;

namespace Inventory.Scripts.Core.ScriptableObjects.Configuration.Anchors
{
    [CreateAssetMenu(menuName = "Inventory/Configuration/Runtime Anchors/Highlighter")]
    public class HighlighterPrefabAnchorSo : PrefabAnchorSo<Highlighter>
    {
        protected override Highlighter InstantiatePrefab()
        {
            var foundCanvas = FindObjectOfType<Canvas>(true);

            return foundCanvas ? Instantiate(prefab, foundCanvas.transform) : Instantiate(prefab);
        }
    }
}