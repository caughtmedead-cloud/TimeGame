using System;
using System.Collections.Generic;
using UnityEngine;
using NewThelos.Inventory.Data;
using NewThelos.Systems.Inventory.Utils;

namespace NewThelos.Inventory.Runtime
{
    /// <summary>
    /// Represents a single inventory grid with placement logic.
    /// Based on UGI's GridTable but rewritten for our architecture.
    /// 
    /// Uses AABB (Axis-Aligned Bounding Box) collision detection.
    /// </summary>
    public class InventoryGrid
    {
        // Grid dimensions
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        // Items in this grid
        private List<InventoryItem> _items = new List<InventoryItem>();
        
        // Events
        public event Action<InventoryItem> OnItemAdded;
        public event Action<InventoryItem> OnItemRemoved;
        public event Action<InventoryItem> OnItemMoved;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public InventoryGrid(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Grid dimensions must be positive");
            
            Width = width;
            Height = height;
        }
        
        /// <summary>
        /// Try to place an item at the specified position.
        /// Returns true if successful, false if invalid placement.
        /// </summary>
        public bool TryPlaceItem(InventoryItem item, ItemDefinitionSO definition, int posX, int posY, bool isRotated = false)
        {
            // Validation
            if (definition == null)
            {
                Debug.LogError("[InventoryGrid] Cannot place item - definition is null");
                return false;
            }
            
            // Update item position and rotation
            item.posX = posX;
            item.posY = posY;
            item.isRotated = isRotated;
            
            // Check if placement is valid
            if (!IsValidPlacement(item, definition))
            {
                return false;
            }
            
            // Add item to grid
            _items.Add(item);
            OnItemAdded?.Invoke(item);
            
            return true;
        }
        
        /// <summary>
        /// Remove an item from the grid by instance ID
        /// </summary>
        public bool TryRemoveItem(string instanceId)
        {
            InventoryItem item = _items.Find(i => i.instanceId == instanceId);
            if (item == null)
                return false;
            
            _items.Remove(item);
            OnItemRemoved?.Invoke(item);
            return true;
        }
        
        /// <summary>
        /// Try to move an item to a new position
        /// </summary>
        public bool TryMoveItem(string instanceId, ItemDefinitionSO definition, int newPosX, int newPosY, bool newRotation)
        {
            InventoryItem item = _items.Find(i => i.instanceId == instanceId);
            if (item == null)
                return false;
            
            // Store old position in case we need to revert
            int oldPosX = item.posX;
            int oldPosY = item.posY;
            bool oldRotation = item.isRotated;
            
            // Temporarily remove item for collision check
            _items.Remove(item);
            
            // Update position
            item.posX = newPosX;
            item.posY = newPosY;
            item.isRotated = newRotation;
            
            // Check if new position is valid
            if (!IsValidPlacement(item, definition))
            {
                // Revert position
                item.posX = oldPosX;
                item.posY = oldPosY;
                item.isRotated = oldRotation;
                _items.Add(item);
                return false;
            }
            
            // Valid - add back to grid
            _items.Add(item);
            OnItemMoved?.Invoke(item);
            return true;
        }
        
        /// <summary>
        /// Check if an item placement is valid at the given position.
        /// Checks bounds and collisions with other items.
        /// </summary>
        private bool IsValidPlacement(InventoryItem item, ItemDefinitionSO definition)
        {
            int itemWidth = definition.GetWidth(item.isRotated);
            int itemHeight = definition.GetHeight(item.isRotated);
            
            // Check grid boundaries
            if (!IsWithinBounds(item.posX, item.posY, itemWidth, itemHeight))
            {
                return false;
            }
            
            // Check collision with other items using AABB
            if (HasCollision(item, itemWidth, itemHeight))
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if position is within grid bounds
        /// </summary>
        private bool IsWithinBounds(int posX, int posY, int itemWidth, int itemHeight)
        {
            // Top-left must be in grid
            if (posX < 0 || posY < 0)
                return false;
            
            // Bottom-right must be in grid
            if (posX + itemWidth > Width || posY + itemHeight > Height)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Check if item collides with any existing items using AABB collision.
        /// Axis-Aligned Bounding Box collision detection (from UGI).
        /// </summary>
        private bool HasCollision(InventoryItem itemToPlace, int itemWidth, int itemHeight)
        {
            int x1 = itemToPlace.posX;
            int y1 = itemToPlace.posY;
            int x2 = x1 + itemWidth;
            int y2 = y1 + itemHeight;
            
            foreach (var existingItem in _items)
            {
                // Skip self-check
                if (existingItem.instanceId == itemToPlace.instanceId)
                    continue;
                
                // Get existing item definition to know its size
                // NOTE: We'll need ItemDefinitionRegistry for this
                // For now, we'll add a helper method
                ItemDefinitionSO existingDef = GetItemDefinition(existingItem.itemDefinitionId);
                if (existingDef == null)
                    continue;
                
                int existingWidth = existingDef.GetWidth(existingItem.isRotated);
                int existingHeight = existingDef.GetHeight(existingItem.isRotated);
                
                int ex1 = existingItem.posX;
                int ey1 = existingItem.posY;
                int ex2 = ex1 + existingWidth;
                int ey2 = ey1 + existingHeight;
                
                // AABB collision check
                // Rectangles collide if they overlap on both X and Y axes
                bool xOverlap = x1 < ex2 && x2 > ex1;
                bool yOverlap = y1 < ey2 && y2 > ey1;
                
                if (xOverlap && yOverlap)
                {
                    return true; // Collision detected
                }
            }
            
            return false; // No collision
        }
        
        /// <summary>
        /// Get item definition by ID.
        /// Uses ItemDefinitionRegistry.
        /// </summary>
        private ItemDefinitionSO GetItemDefinition(string itemDefinitionId)
        {
            return ItemDefinitionRegistry.GetItemDefinition(itemDefinitionId);
        }
        
        /// <summary>
        /// Get all items in this grid (read-only)
        /// </summary>
        public IReadOnlyList<InventoryItem> GetAllItems()
        {
            return _items.AsReadOnly();
        }
        
        /// <summary>
        /// Get item at specific position (top-left cell)
        /// </summary>
        public InventoryItem GetItemAt(int posX, int posY)
        {
            return _items.Find(item => item.posX == posX && item.posY == posY);
        }
        
        /// <summary>
        /// Get item by instance ID
        /// </summary>
        public InventoryItem GetItemById(string instanceId)
        {
            return _items.Find(item => item.instanceId == instanceId);
        }
        
        /// <summary>
        /// Clear all items from grid
        /// </summary>
        public void Clear()
        {
            var itemsCopy = new List<InventoryItem>(_items);
            _items.Clear();
            
            foreach (var item in itemsCopy)
            {
                OnItemRemoved?.Invoke(item);
            }
        }
        
        /// <summary>
        /// Find empty space that can fit an item of given dimensions.
        /// Based on UGI's FindSpaceForObject algorithm.
        /// Returns true and sets outPosX/outPosY if space found.
        /// </summary>
        public bool TryFindEmptySpace(ItemDefinitionSO definition, out int outPosX, out int outPosY, bool tryRotated = false)
        {
            outPosX = -1;
            outPosY = -1;
            
            int itemWidth = definition.GetWidth(tryRotated);
            int itemHeight = definition.GetHeight(tryRotated);
            
            Debug.Log($"[InventoryGrid] Finding space for {definition.itemId}: size={itemWidth}x{itemHeight}, current items={_items.Count}");
            
            // Try every position in grid
            for (int y = 0; y <= Height - itemHeight; y++)
            {
                for (int x = 0; x <= Width - itemWidth; x++)
                {
                    // Create temporary item at this position
                    var tempItem = new InventoryItem(definition.itemId, x, y, tryRotated);
                    
                    // Check if valid placement
                    bool withinBounds = IsWithinBounds(x, y, itemWidth, itemHeight);
                    bool hasCollision = HasCollision(tempItem, itemWidth, itemHeight);
                    
                    Debug.Log($"[InventoryGrid]   Testing ({x},{y}): bounds={withinBounds}, collision={hasCollision}");
                    
                    if (withinBounds && !hasCollision)
                    {
                        outPosX = x;
                        outPosY = y;
                        Debug.Log($"[InventoryGrid] ✅ Found space at ({x},{y})");
                        return true;
                    }
                }
            }
            
            Debug.LogWarning($"[InventoryGrid] ❌ No space found for {definition.itemId}");
            return false; // No space found
        }
    }
}