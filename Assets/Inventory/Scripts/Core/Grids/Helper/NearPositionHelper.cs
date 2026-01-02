using System;
using System.Collections.Generic;
using System.Linq;
using Inventory.Scripts.Core.Items;

namespace Inventory.Scripts.Core.Grids.Helper
{
    public static class NearPositionHelper
    {
        public static Position FindNearestPosition(ItemTable itemToPlace, Position originalPosition, ItemTable[,] slots)
        {
            try
            {
                // Define the directions to validate based on the position of the nearPosition
                var directions = GetValidationDirections(originalPosition, slots);

                foreach (var (dx, dy) in directions)
                {
                    var newPosition = TryPlaceInDirection(itemToPlace, dx, dy, slots);
                    if (newPosition != null)
                    {
                        return newPosition;
                    }
                }

                // If no available position found, return null
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Position TryPlaceInDirection(ItemTable itemToPlace, int dx, int dy,
            ItemTable[,] slots)
        {
            for (var x = 0; x < itemToPlace.Width; x++)
            {
                for (var y = 0; y < itemToPlace.Height; y++)
                {
                    if (slots[x + dx, y + dy] != null)
                    {
                        return null;
                    }
                }
            }

            return new Position(dx, dy);
        }

        private static IEnumerable<(int dx, int dy)> GetValidationDirections(Position originalPosition,
            ItemTable[,] slots)
        {
            var columns = slots.GetLength(0);
            var rows = slots.GetLength(1);

            var directions = new List<(int dx, int dy)>();

            var x = originalPosition.x;
            var y = originalPosition.y;

            // Right
            for (var i = x + 1; i < columns; i++)
            {
                if (slots[i, y] == null)
                {
                    directions.Add((i, y));
                }
            }

            // Left
            for (var i = x; i > 0; i--)
            {
                if (slots[i, y] == null)
                {
                    directions.Add((i, y));
                }
            }

            // Top
            for (var i = y; i > 0; i--)
            {
                if (slots[x, i] == null)
                {
                    directions.Add((x, i));
                }
            }

            // Bottom
            for (var i = y + 1; i < rows; i++)
            {
                if (slots[x, i] == null)
                {
                    directions.Add((x, i));
                }
            }

            // Modify the return statement to sort the directions list based on distance
            return directions.OrderBy(p => Math.Abs(originalPosition.x - p.dx) + Math.Abs(originalPosition.y - p.dy))
                .ToList();
        }
    }
}