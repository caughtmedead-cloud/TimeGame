namespace Inventory.Scripts.Core.Grids
{
    public enum GridResponse
    {
        Inserted,
        AlreadyInserted,
        InventoryFull,
        NoGridTableSelected,
        OutOfBounds,
        Overlapping,
        InsertInsideYourself
    }
}