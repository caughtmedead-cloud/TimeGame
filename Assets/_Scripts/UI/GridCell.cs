using UnityEngine;
using UnityEngine.UI;

namespace NewThelos.UI.Inventory
{
    /// <summary>
    /// Represents a single cell in the inventory grid.
    /// Changes visual state when occupied by an item.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class GridCell : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        [SerializeField] private Color occupiedColor = new Color(0.4f, 0.4f, 0.2f, 0.5f);
        [SerializeField] private Color borderColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        [SerializeField] private float borderWidth = 2f;
        
        private Image _background;
        private Image _border;
        private bool _isOccupied;
        
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        
        private void Awake()
        {
            _background = GetComponent<Image>();
            CreateBorder();
            SetOccupied(false);
        }
        
        /// <summary>
        /// Initialize cell with grid coordinates
        /// </summary>
        public void Initialize(int gridX, int gridY)
        {
            GridX = gridX;
            GridY = gridY;
            gameObject.name = $"Cell_{gridX}_{gridY}";
        }
        
        /// <summary>
        /// Set the occupied state of this cell
        /// </summary>
        public void SetOccupied(bool occupied)
        {
            _isOccupied = occupied;
            _background.color = occupied ? occupiedColor : emptyColor;
        }
        
        /// <summary>
        /// Create border around cell
        /// </summary>
        private void CreateBorder()
        {
            // Create border GameObject
            GameObject borderObj = new GameObject("Border");
            borderObj.transform.SetParent(transform, false);
            
            // Add Image component
            _border = borderObj.AddComponent<Image>();
            _border.color = borderColor;
            _border.raycastTarget = false;
            
            // Create border outline using Outline component
            Outline outline = borderObj.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(borderWidth, borderWidth);
            
            // Set RectTransform to fill parent
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderRect.anchoredPosition = Vector2.zero;
            
            // Make border transparent, only outline visible
            Color transparent = borderColor;
            transparent.a = 0f;
            _border.color = transparent;
        }
        
        /// <summary>
        /// Get the world position of this cell's center
        /// </summary>
        public Vector2 GetCenterPosition()
        {
            RectTransform rect = GetComponent<RectTransform>();
            return rect.position;
        }
    }
}