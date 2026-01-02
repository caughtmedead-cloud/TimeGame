using System.Collections.Generic;
using Inventory.Scripts.Core.Grids;
using Inventory.Scripts.Core.Items;
using UnityEngine;

namespace Inventory.Samples.Examples
{
    public class InsertItemsInASpecificPosition : MonoBehaviour
    {
        public void InsertItemsInASpecificPositionUsingSupplier(List<ItemTable> items, GridTable gridTable)
        {
            
            // Make sure you have saved the OnGridPositionX and OnGridPositionY, because we use this values to insert the
            // item on that position.
            
            foreach (var itemTable in items)
            {
                var gridResponse = gridTable.PlaceItem(itemTable, itemTable.Position.x, itemTable.Position.y);

                if (gridResponse != GridResponse.Inserted)
                {
                    Debug.LogWarning("Couldn't insert item in the grid... Error: " + gridResponse);
                }
            }
        }
    }
}