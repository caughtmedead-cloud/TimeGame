using UnityEngine;
using FishNet.Object;

namespace NewThelos.UI
{
    /// <summary>
    /// Hides the player canvas for non-owned players.
    /// Works with Screen Space - Overlay mode.
    /// </summary>
    public class PlayerCanvasSetup : NetworkBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        
        private void Awake()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            
            // Add CanvasGroup if it doesn't exist
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            if (!IsOwner)
            {
                // Hide for non-owners using CanvasGroup
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                // Show for owner
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
}