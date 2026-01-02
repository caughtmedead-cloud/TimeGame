using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using Inventory.Scripts.Core.Items.Metadata;

namespace Inventory.Scripts.Core.Displays.Filler.Primitives
{
    public class GridFiller : AbstractFiller
    {
        private ItemTable _currentItemTable;
        private ContainerGrids _openedContainerGrids;

        public override void Init()
        {
        }

        protected override void OnSet(DisplayData displayData)
        {
            if (IsAlreadyOpened()) return;

            _currentItemTable = displayData.ItemContainer;

            if (_currentItemTable?.InventoryMetadata is not ContainerMetadata containerMetadata)
            {
                return;
            }

            _openedContainerGrids = containerMetadata.OpenInventory(transform);
        }

        protected override void OnReset()
        {
            if (_currentItemTable?.InventoryMetadata is not ContainerMetadata containerMetadata)
            {
                return;
            }

            containerMetadata.CloseInventory(_openedContainerGrids);

            _openedContainerGrids = null;
        }

        protected override bool ShouldFill(DisplayData displayData)
        {
            return displayData is { ItemContainer: not null };
        }

        private bool IsAlreadyOpened()
        {
            return _openedContainerGrids != null;
        }
    }
}