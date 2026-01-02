using Inventory.Scripts.Core.Controllers;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Holders;
using Inventory.Scripts.Core.Items.Helper;
using Inventory.Scripts.Core.ScriptableObjects.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Items
{
    public abstract class AbstractItem : MonoBehaviour
    {
        [SerializeField] protected Rotation rotationType = Rotation.MinusNinety;

        [field: SerializeReference] public ItemTable ItemTable { get; private set; }

        private void OnDisable()
        {
            if (ItemTable == null) return;

            ItemTable.OnUpdateUI -= HandleUpdateUI;
        }

        private void OnDestroy()
        {
            if (ItemTable == null) return;

            ItemTable.OnUpdateUI -= HandleUpdateUI;
        }

        private void HandleUpdateUI(ItemUIUpdater updater)
        {
            OnSetProperties(ItemTable);

            if (updater.Background != null)
            {
                SetBackground(updater.Background.Item1, updater.Background.Item2);
            }
        }

        protected abstract void OnSetProperties(ItemTable item);

        public abstract void ResizeIcon(bool isDragging = false);

        public void Rotate()
        {
            ItemTable.Rotate();

            RotateUI();
        }

        public abstract void RotateUI();

        public void SetDragProps(ItemDataSo itemDataSo, GridTable gridTable, Position position, HolderData holder)
        {
            ItemTable = StaticInventoryContext.CreateItemTable(itemDataSo, true);
            ItemTable.SetGridProps(gridTable, position, holder);
            transform.SetAsFirstSibling();
            SetDragRepresentationStyle();
        }

        protected abstract void SetDragRepresentationStyle();

        public abstract void SetDraggingStyle();

        // TODO: Validate in the future if Color is the best fits.
        public abstract void SetBackground(bool setBackground, Color? color = null);

        public void RefreshItem(ItemTable item)
        {
            ClearPreviousState(ItemTable);

            ItemTable = item;
            ItemTable.OnUpdateUI += HandleUpdateUI;

            OnSetProperties(ItemTable);
            
            if (ItemTable.IsRotated)
            {
                RotateUI();
            }
        }

        private void ClearPreviousState(ItemTable previousItemTable)
        {
            if (previousItemTable == null) return;

            previousItemTable.OnUpdateUI -= HandleUpdateUI;
        }
    }
}