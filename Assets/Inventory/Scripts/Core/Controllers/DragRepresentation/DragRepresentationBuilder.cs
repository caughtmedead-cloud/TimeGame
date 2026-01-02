using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Scripts.Core.Controllers.DragRepresentation
{
    public static class DragRepresentationBuilder
    {
        public static AbstractItem PlaceDragRepresentation(ItemTable item, Transform dragParentTransform)
        {
            var itemDragRepresentation = StaticInventoryContext.GetDragRepresentationInstance(dragParentTransform);

            itemDragRepresentation.SetDragProps(
                item.ItemDataSo,
                item.CurrentGridTable,
                item.Position,
                item.CurrentHolder
            );

            if (CheckIfShouldRotate(item, itemDragRepresentation))
            {
                itemDragRepresentation.Rotate();
            }

            itemDragRepresentation.ResizeIcon();
            itemDragRepresentation.gameObject.SetActive(true);

            return itemDragRepresentation;
        }

        private static bool CheckIfShouldRotate(ItemTable item, AbstractItem itemDragRepresentation)
        {
            return item.IsRotated && !itemDragRepresentation.ItemTable.IsRotated ||
                   !item.IsRotated && itemDragRepresentation.ItemTable.IsRotated;
        }

        public static void RemoveDragRepresentation(AbstractItem itemDragRepresentation)
        {
            if (itemDragRepresentation.ItemTable.IsRotated)
            {
                itemDragRepresentation.Rotate();
            }

            itemDragRepresentation.ItemTable.SetGridProps(null, null, null);
            itemDragRepresentation.gameObject.SetActive(false);
        }
    }
}