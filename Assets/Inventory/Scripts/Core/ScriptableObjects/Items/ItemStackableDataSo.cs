using UnityEngine;
using UnityEngine.Serialization;

namespace Inventory.Scripts.Core.ScriptableObjects.Items
{
    [CreateAssetMenu(menuName = "Inventory/Items/New Stackable Item")]
    public class ItemStackableDataSo : ItemDataSo
    {
        [Header("Stackable Settings")]
        [SerializeField, Tooltip("Max quantity of items can be stackable. -1 for unlimited stackable items.")]
        private double maxStack = -1;

        [FormerlySerializedAs("minValue")]
        [SerializeField,
         Tooltip("The min quantity of item can be. Will no be able to split if the item reach this threshold.")]
        private double minStack = 1;

        [SerializeField, Tooltip("If the split can only generate integer numbers.")]
        private bool treatAsInteger;

        [Header("Stackable Display Settings")]
        [SerializeField, Tooltip("If will show the max stack on the item. Like: 1/8")]
        private bool showMaxStack;

        public double MaxStack => maxStack;

        public bool ShowMaxStack => showMaxStack;

        public double MinStack => minStack;

        public bool TreatAsInteger => treatAsInteger;
    }
}